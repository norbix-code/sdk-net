using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The Logs endpoints of the Hub that a reader cares about beyond the coverage
/// snapshot (<c>EndpointCoverageTests.Hub.Logs</c>): one log entry by its id,
/// and the logging integration test — nothing is delivered to an integration
/// until its last test passed.
/// </summary>
[TestFixture]
public sealed class HubLogsEndpointTests
{
    private const string LogId = "6716f2a0c3b1e4d5f6a7b8c9";
    private const string IntegrationId = "33333333-3333-3333-3333-333333333333";

    [Test]
    public async Task GetLog_puts_the_id_in_the_path_and_reads_the_entry()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/logs/" + LogId,
            new
            {
                item = new
                {
                    id = LogId,
                    timestamp = "2026-10-21T08:00:00Z",
                    module = "app",
                    level = "information",
                    eventCode = "app:order:paid",
                    title = "Order paid",
                    message = "Order ord_1042 paid by card",
                    correlationId = "checkout-1042",
                    env = "PROD",
                    origin = "app",
                    meta = new Dictionary<string, string> { ["orderId"] = "ord_1042" },
                },
            });

        var response = await fixture.Client.Logs.GetLogAsync(new GetLog { Id = LogId });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, Entry = response?.Item },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task TestLoggingIntegration_posts_the_integration_id()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/logs/integrations/test", new { });

        await fixture.Client.Logs.TestLoggingIntegrationAsync(
            new TestLoggingIntegration { IntegrationId = IntegrationId });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }
}
