using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Workflows beta: the signed resume links on the project API
/// (GET / POST /{version}/code/workflows/resume/{Token}) carry <c>INorbixOptionalAuth</c>.
/// The token in the link is the key, so a client with no API key and no bearer token still
/// sends them — with no <c>Authorization</c> header — instead of failing with
/// <c>NORBIX_NOT_AUTHENTICATED</c>. The transport never demands a project id for a request (the
/// header goes only when one is configured); the client constructor still asks for one
/// (<c>NorbixClientOptions.Validate</c>), as for every client. Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class WorkflowResumeLinkTests
{
    private const string Token = "signed-resume-link-abc";

    [Test]
    public async Task Signed_resume_links_go_out_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondNext(new { runId = "wfr_7Qk2abc", mode = "approve", decision = "approved" });
        fixture.RespondNext(new { run = new { runId = "wfr_7Qk2abc", status = "running" } });

        var link = await fixture.Client.Code.OpenCodeWorkflowResumeLinkAsync(
            new OpenCodeWorkflowResumeLink { Token = Token });
        var used = await fixture.Client.Code.UseCodeWorkflowResumeLinkAsync(
            new UseCodeWorkflowResumeLink { Token = Token });

        await Verifier.Verify(
            new { Sent = fixture.RecordedRequests, link?.Mode, used?.Run?.Status },
            VerifyConfig.VerifySettings);
    }
}
