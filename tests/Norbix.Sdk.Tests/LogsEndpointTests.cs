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

    private static object QuotaRefusal => new
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
    };

    private static NorbixException WriteOneAndCatch(NorbixTestFixture fixture) =>
        Assert.ThrowsAsync<NorbixException>(
            async () => await fixture.Client.Logs.WriteLogsAsync(new WriteLogs
            {
                Entries = [new WriteLogEntry { Title = "Order paid" }],
            }))!;

    [Test]
    public void WriteLogs_429_carries_the_Retry_After_seconds()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/logs",
            QuotaRefusal,
            HttpStatusCode.TooManyRequests,
            new Dictionary<string, string> { ["Retry-After"] = "37" });

        var ex = WriteOneAndCatch(fixture);

        Assert.That(ex.HttpStatus, Is.EqualTo(429));
        Assert.That(ex.ErrorCode, Is.EqualTo("CM-ERRORS-LOGGING-003"));
        Assert.That(ex.RetryAfterSeconds, Is.EqualTo(37));
        // A POST is never resent by the SDK: one call, one request.
        Assert.That(fixture.RecordedRequests, Has.Count.EqualTo(1));
        Assert.That(fixture.LastRequest!.Method, Is.EqualTo("POST"));
    }

    [Test]
    public void WriteLogs_429_with_an_HTTP_date_gives_the_seconds_until_it()
    {
        using var fixture = NorbixTestFixture.Create();
        var at = DateTimeOffset.UtcNow.AddSeconds(120).ToString("R");
        fixture.Respond(
            "/logs",
            QuotaRefusal,
            HttpStatusCode.TooManyRequests,
            new Dictionary<string, string> { ["Retry-After"] = at });

        var ex = WriteOneAndCatch(fixture);

        Assert.That(ex.RetryAfterSeconds, Is.InRange(115, 121));
    }

    [Test]
    public void WriteLogs_429_with_a_past_HTTP_date_gives_zero()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/logs",
            QuotaRefusal,
            HttpStatusCode.TooManyRequests,
            new Dictionary<string, string> { ["Retry-After"] = "Wed, 21 Oct 2015 07:28:00 GMT" });

        var ex = WriteOneAndCatch(fixture);

        Assert.That(ex.RetryAfterSeconds, Is.EqualTo(0));
    }

    [Test]
    public void WriteLogs_429_without_Retry_After_leaves_it_null()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/logs", QuotaRefusal, HttpStatusCode.TooManyRequests);

        var ex = WriteOneAndCatch(fixture);

        Assert.That(ex.HttpStatus, Is.EqualTo(429));
        Assert.That(ex.RetryAfterSeconds, Is.Null);
    }

    [Test]
    public void WriteLogs_429_with_an_unreadable_Retry_After_leaves_it_null()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/logs",
            QuotaRefusal,
            HttpStatusCode.TooManyRequests,
            new Dictionary<string, string> { ["Retry-After"] = "soon" });

        var ex = WriteOneAndCatch(fixture);

        Assert.That(ex.RetryAfterSeconds, Is.Null);
    }

    [Test]
    public void WriteLogs_500_leaves_Retry_After_null()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/logs",
            new { responseStatus = new { isSuccess = false, errorCode = "CM-ERRORS-500", message = "Boom" } },
            HttpStatusCode.InternalServerError);

        var ex = WriteOneAndCatch(fixture);

        Assert.That(ex.HttpStatus, Is.EqualTo(500));
        Assert.That(ex.RetryAfterSeconds, Is.Null);
        Assert.That(fixture.RecordedRequests, Has.Count.EqualTo(1));
    }
}
