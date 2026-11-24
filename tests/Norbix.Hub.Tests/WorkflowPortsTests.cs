using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// What goes into and comes out of each step (<c>POST /{version}/code/workflows/validate</c>): a step names the
/// earlier steps it also <c>Uses</c>, an Ask AI step takes a <c>context</c> in its config, and the answer carries
/// the <c>Ports</c> of the definition — what each step takes and gives. A fake response only — no gateway is contacted.
/// </summary>
[TestFixture]
public sealed class WorkflowPortsTests
{
    [Test]
    public async Task ValidateCodeWorkflow_sends_uses_and_the_ai_context_and_reads_the_ports()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/code/workflows/validate", new
        {
            isValid = true,
            ports = new
            {
                start = new { format = "record", collection = "ads", choices = new List<string> { "record" } },
                startEvent = "database.record.inserted",
                steps = new object[]
                {
                    new
                    {
                        stepId = "checkAd",
                        inputs = new[] { new { from = "start", exit = "start", format = "record", collection = "ads" } },
                        output = new { format = "json", choices = new List<string> { "text", "json" }, value = "json" },
                        takes = new[] { new { field = "config.context", accepts = new List<string> { "text", "json", "record", "records", "event" } } },
                    },
                    new
                    {
                        stepId = "approve",
                        inputs = new[] { new { from = "decide", exit = "then", format = "boolean" } },
                        output = new { format = "json", choices = new List<string> { "json" } },
                        uses = new[] { new { stepId = "checkAd", format = "json" } },
                    },
                },
            },
        });

        var response = await fixture.Client.Code.ValidateCodeWorkflowAsync(new ValidateCodeWorkflow
        {
            Definition = new WorkflowDefinitionDto
            {
                Name = "Ad check",
                Trigger = new WorkflowStartEventDto { Source = "database", Event = "database.record.inserted", Schema = "ads" },
                Steps =
                [
                    new WorkflowStepDto
                    {
                        Id = "checkAd",
                        Kind = "ai.invoke",
                        Next = "decide",
                        Config = new Dictionary<string, object>
                        {
                            ["prompt"] = "Is this ad fine?",
                            ["context"] = "@Model.Input",
                            ["contextExclude"] = new List<string> { "ownerPhone" },
                            ["reask"] = 1,
                        },
                    },
                    new WorkflowStepDto { Id = "approve", Kind = "database.update", Uses = ["checkAd"] },
                ],
            },
        });

        await Verifier.Verify(new { Sent = fixture.LastRequest, Response = response }, VerifyConfig.VerifySettings);
    }
}
