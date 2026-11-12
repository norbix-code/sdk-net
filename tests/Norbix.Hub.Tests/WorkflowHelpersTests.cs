using System.Text.Json;

using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Workflows beta, the hand-written helpers (Code.Workflows.md §15):
/// <c>WaitForWorkflowRunAsync</c> polls GET /{version}/code/workflows/runs/{RunId} until the run
/// is succeeded / failed / cancelled / waiting, and <c>StepOutput&lt;T&gt;</c> reads the output of a
/// step's latest succeeded execution. Also the signed resume links (<c>INorbixOptionalAuth</c>):
/// they go out with no credentials. Nothing leaves the process; the poll
/// interval is a millisecond so the tests stay fast.
/// </summary>
[TestFixture]
public sealed class WorkflowHelpersTests
{
    private const string RunId = "wfr_7Qk2abc";
    private const string Token = "signed-resume-link-abc";

    private static readonly WaitForWorkflowRunOptions Fast = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        PollInterval = TimeSpan.FromMilliseconds(1),
    };

    private static readonly string[] Approvers = ["usr_ann", "usr_bob", "usr_cid"];

    private sealed record Greeting(string Message, int Count);

    private static NorbixTestFixture NewFixture() =>
        NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });

    private static object RunBody(string status, params object[] steps) =>
        new { run = new { runId = RunId, workflowId = "wf_1", status, steps } };

    [Test]
    public async Task Wait_polls_until_succeeded_and_StepOutput_reads_the_step()
    {
        using var fixture = NewFixture();
        fixture
            .RespondNext(RunBody("running"))
            .RespondNext(RunBody("running"))
            .RespondNext(RunBody(
                "succeeded",
                new
                {
                    stepId = "greet",
                    kind = "code",
                    status = "succeeded",
                    attempt = 1,
                    pass = 1,
                    output = new { message = "hello", count = 3 },
                }));

        var run = await fixture.Client.WaitForWorkflowRunAsync(RunId, Fast);

        await Verifier.Verify(
            new
            {
                run.Status,
                Output = run.StepOutput<Greeting>("greet"),
                Requests = fixture.RecordedRequests.Select(r => $"{r.Method} {r.Path}{r.Query}"),
            },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Wait_returns_a_waiting_run_at_once()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(RunBody("waiting"));

        var run = await fixture.Client.WaitForWorkflowRunAsync(RunId, Fast);

        await Verifier.Verify(
            new { run.Status, Requests = fixture.RecordedRequests.Count },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Wait_throws_the_timeout_with_the_last_run()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(RunBody("running"));

        WorkflowRunTimeoutException? thrown = null;
        try
        {
            await fixture.Client.WaitForWorkflowRunAsync(
                RunId,
                new WaitForWorkflowRunOptions { Timeout = TimeSpan.Zero, PollInterval = TimeSpan.FromMilliseconds(1) });
        }
        catch (WorkflowRunTimeoutException ex)
        {
            thrown = ex;
        }

        await Verifier.Verify(
            new
            {
                thrown?.Message,
                thrown?.LastRun?.RunId,
                thrown?.LastRun?.Status,
                Requests = fixture.RecordedRequests.Count,
            },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Wait_throws_a_NorbixException_when_the_answer_has_no_run()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(new { });

        NorbixException? thrown = null;
        try
        {
            await fixture.Client.WaitForWorkflowRunAsync(RunId, Fast);
        }
        catch (NorbixException ex)
        {
            thrown = ex;
        }

        await Verifier.Verify(new { thrown?.Message }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task StepOutput_picks_the_highest_pass_then_attempt_among_succeeded()
    {
        static WorkflowRunStepDto Step(string id, string status, int pass, int attempt, object? output) =>
            new()
            {
                StepId = id,
                Kind = "code",
                Status = status,
                Pass = pass,
                Attempt = attempt,
                Output = output is null ? null : JsonSerializer.SerializeToElement(output),
            };

        var run = new WorkflowRunDto
        {
            RunId = RunId,
            Status = "succeeded",
            Steps =
            [
                Step("loop", "succeeded", pass: 1, attempt: 1, new { message = "pass 1", count = 1 }),
                Step("loop", "succeeded", pass: 2, attempt: 1, new { message = "pass 2 attempt 1", count = 2 }),
                Step("loop", "succeeded", pass: 2, attempt: 2, new { message = "pass 2 attempt 2", count = 3 }),
                // A later pass that failed: the succeeded filter comes before the max.
                Step("loop", "failed", pass: 3, attempt: 1, new { message = "pass 3 failed", count = 4 }),
                Step("other", "succeeded", pass: 9, attempt: 9, new { message = "other step", count = 9 }),
                Step("never", "failed", pass: 1, attempt: 1, null),
                Step("never", "running", pass: 2, attempt: 1, null),
            ],
        };

        await Verifier.Verify(
            new
            {
                Loop = run.StepOutput<Greeting>("loop"),
                // Verify drops null members, so the defaults are pinned as booleans.
                NeverSucceededIsDefault = run.StepOutput<Greeting>("never") is null,
                MissingStepIsDefault = run.StepOutput<Greeting>("missing") is null,
                NeverSucceededIntIsDefault = run.StepOutput<int>("never") == 0,
            },
            VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// Group approval (Code.Workflows.md §3.3b): a resume records the caller's answer and
    /// answers the run still <c>waiting</c> until enough people answered. The waiting record
    /// carries <c>require</c> and the answers so far; both land in the typed fields.
    /// </summary>
    [Test]
    public async Task Resume_of_a_group_approval_answers_the_run_still_waiting_with_the_answers()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(new
        {
            run = new
            {
                runId = RunId,
                workflowId = "wf_1",
                status = "waiting",
                waitingOn = new[]
                {
                    new
                    {
                        stepId = "approve",
                        kind = "approval",
                        assignees = Approvers,
                        require = 2,
                        answers = new[]
                        {
                            new { userId = "usr_ann", decision = "approved", at = "2026-11-12T09:30:15Z" },
                        },
                    },
                },
            },
        });

        var answer = await fixture.Client.Code.ResumeCodeWorkflowRunAsync(
            new ResumeCodeWorkflowRun { RunId = RunId, StepId = "approve", Decision = "approved" });

        var sent = fixture.LastRequest!;
        Assert.That(sent.Method, Is.EqualTo("POST"));
        Assert.That(sent.Path, Does.EndWith($"/code/workflows/runs/{RunId}/resume"));

        var run = answer!.Run!;
        Assert.That(run.Status, Is.EqualTo("waiting"));
        Assert.That(run.WaitingOn, Has.Count.EqualTo(1));

        var wait = run.WaitingOn[0];
        Assert.That(wait.StepId, Is.EqualTo("approve"));
        Assert.That(wait.Assignees, Is.EqualTo(Approvers));
        Assert.That(wait.Require, Is.EqualTo(2));
        Assert.That(wait.Answers, Has.Count.EqualTo(1));
        Assert.That(wait.Answers![0].UserId, Is.EqualTo("usr_ann"));
        Assert.That(wait.Answers[0].Decision, Is.EqualTo("approved"));
        Assert.That(
            wait.Answers[0].At.ToUniversalTime(),
            Is.EqualTo(new DateTime(2026, 11, 12, 9, 30, 15, DateTimeKind.Utc)));
    }

    /// <summary>
    /// The signed resume links carry <c>INorbixOptionalAuth</c>: a client with no API key and no
    /// bearer token still sends them, with no <c>Authorization</c> header (a normal route throws
    /// <c>NORBIX_NOT_AUTHENTICATED</c>). The client constructor still asks for a project id.
    /// </summary>
    [Test]
    public async Task Signed_resume_links_go_out_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondNext(new { runId = RunId, mode = "approve", decision = "approved" });
        fixture.RespondNext(RunBody("running"));

        var link = await fixture.Client.Code.OpenCodeWorkflowResumeLinkAsync(
            new OpenCodeWorkflowResumeLink { Token = Token });
        var used = await fixture.Client.Code.UseCodeWorkflowResumeLinkAsync(
            new UseCodeWorkflowResumeLink { Token = Token });

        await Verifier.Verify(
            new { Sent = fixture.RecordedRequests, link?.Mode, used?.Run?.Status },
            VerifyConfig.VerifySettings);
    }
}
