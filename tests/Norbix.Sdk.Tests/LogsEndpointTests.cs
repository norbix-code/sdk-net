using System.Net;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Public-API logs: an app writes its own log entries to the project's logs
/// (<c>POST /{version}/logs</c>).
/// <para>
/// Hand-written on purpose, like <see cref="PaymentOrderEndpointTests"/>: the
/// coverage snapshot (<c>EndpointCoverageTests.Api.Logs</c>) proves the group
/// is wired up; these prove the entries travel in the body as the gateway reads
/// them, the accepted count comes back typed, and a quota refusal reaches the
/// caller with its status and code.
/// </para>
/// </summary>
[TestFixture]
public sealed class LogsEndpointTests
{
    [Test]
    public async Task WriteLogs_posts_the_entries_and_reads_the_accepted_count()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/logs", new { accepted = 2 });

        var response = await fixture.Client.Logs.WriteLogsAsync(new WriteLogs
        {
            Entries =
            [
                new WriteLogEntry
                {
                    Title = "Order paid",
                    Message = "Order ord_1042 paid by card",
                    EventCode = "order:paid",
                    CorrelationId = "checkout-1042",
                    Meta = new Dictionary<string, string> { ["orderId"] = "ord_1042" },
                },
                new WriteLogEntry
                {
                    Level = "warning",
                    Title = "Stock low",
                    Timestamp = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc),
                },
            ],
        });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response?.Accepted },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public void WriteLogs_over_the_minute_quota_throws_429_with_the_logging_code()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/logs",
            new
            {
                responseStatus = new
                {
                    isSuccess = false,
                    errorCode = "CM-ERRORS-LOGGING-003",
                    message = "The project's log quota for this minute is used up",
                    errors = new[]
                    {
                        new
                        {
                            message = "The project's log quota for this minute is used up",
                            errorCode = "CM-ERRORS-LOGGING-003",
                        },
                    },
                },
            },
            HttpStatusCode.TooManyRequests);

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () => await fixture.Client.Logs.WriteLogsAsync(new WriteLogs
            {
                Entries = [new WriteLogEntry { Title = "Order paid" }],
            }))!;

        Assert.That(ex.HttpStatus, Is.EqualTo(429));
        Assert.That(ex.ErrorCode, Is.EqualTo("CM-ERRORS-LOGGING-003"));
    }
}
