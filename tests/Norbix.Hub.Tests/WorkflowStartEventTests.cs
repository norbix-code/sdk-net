using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The event that starts a workflow (<c>PUT /{version}/code/workflows/{Id}/start-event</c>) and the test run
/// (<c>POST /{version}/code/workflows/{Id}/test</c>): the id goes in the path, the start / the test event in
/// the body. A fake response only — no gateway is contacted.
/// </summary>
[TestFixture]
public sealed class WorkflowStartEventTests
{
    private const string WorkflowId = "wf_3kQ9mZ2vXc8LpR4wT7yH1n";

    [Test]
    public async Task SetCodeWorkflowStartEvent_puts_the_id_in_the_path_and_the_start_in_the_body()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond($"/code/workflows/{WorkflowId}/start-event", new
        {
            workflow = new
            {
                workflowId = WorkflowId,
                env = "TEST",
                version = 1,
                hasDraft = true,
                definition = new
                {
                    name = "Review new listings",
                    trigger = new { source = "database", @event = "database.record.inserted", schema = "moto_listings" },
                },
            },
        });

        var response = await fixture.Client.Code.SetCodeWorkflowStartEventAsync(new SetCodeWorkflowStartEvent
        {
            Id = WorkflowId,
            Start = new WorkflowStartEventDto
            {
                Source = "database",
                Event = "database.record.inserted",
                Schema = "moto_listings",
                Condition = "return New.status == \"review\";",
            },
        });

        await Verifier.Verify(new { Sent = fixture.LastRequest, Response = response }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task TestCodeWorkflow_sends_the_test_record_and_reads_the_run()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond($"/code/workflows/{WorkflowId}/test", new
        {
            runId = "wfr_4hT2b9QkLm3XcT1yRfGh7a",
            status = "running",
        });

        var response = await fixture.Client.Code.TestCodeWorkflowAsync(new TestCodeWorkflow
        {
            Id = WorkflowId,
            Source = "database",
            Event = "database.record.updated",
            Collection = "moto_listings",
            RecordId = "rec_1",
            Edits = new Dictionary<string, object> { ["status"] = "review" },
        });

        await Verifier.Verify(new { Sent = fixture.LastRequest, Response = response }, VerifyConfig.VerifySettings);
    }
}
