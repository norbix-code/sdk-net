using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Received payment webhooks as records (gateway 86f24ecd2):
/// GET /{version}/payments/webhooks/runs (filters + cursor paging on the query, no body)
/// and GET /{version}/payments/webhooks/runs/{Id} (the id in the path, the run and its
/// masked raw call read back from the generated response). Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class PaymentsWebhookRunsTests
{
    private const string RunId = "pwr_1p4R3gTobGZpD6fXvHT4rd";

    private static object RunRow() => new
    {
        id = RunId,
        callId = "pwc_7Hq2mN4bV8cX1zL6kP3rT9",
        env = "PROD",
        integrationId = "int_stripe",
        source = "Stripe",
        receivedOn = "2026-10-26T08:00:00Z",
        statusCode = 200,
        outcome = "Applied",
        reason = "processed",
        description = "Processed.",
        eventName = "payment_intent.succeeded",
        providerEventId = "evt_1",
        order = new { orderId = "ord_1", kind = "Applied", from = "Pending", to = "Paid", reason = "paid" },
        actions = new object[]
        {
            new { triggerId = "trg_email", triggerName = "Receipt", action = "Email", status = "Sent", norbixEvent = "payments.order.paid", at = "2026-10-26T08:00:01Z" },
            new { triggerId = "trg_code", action = "Code", status = "Skipped", reason = "condition false", norbixEvent = "payments.webhook.received", at = "2026-10-26T08:00:01Z" },
        },
        part = 1,
        parts = 1,
    };

    [Test]
    public async Task Runs_send_every_filter_and_the_paging_on_the_query_with_no_body()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(new
        {
            list = new { items = new[] { RunRow() }, hasMore = true, hasPrevious = false, startingAfter = RunId },
        });

        var response = await fixture.Client.Payments.GetPaymentsWebhookRunsAsync(new GetPaymentsWebhookRuns
        {
            IntegrationId = "int_stripe",
            EventName = "payment_intent.succeeded",
            TriggerId = "trg_email",
            Outcome = "applied",
            FromUtc = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
            PageSize = 20,
            StartingAfter = "pwr_previous",
        });

        await Verifier.Verify(new { Sent = fixture.LastRequest, response!.List }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Run_puts_the_id_in_the_path_and_reads_the_raw_call()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(new
        {
            item = new
            {
                run = RunRow(),
                raw = new
                {
                    callId = "pwc_7Hq2mN4bV8cX1zL6kP3rT9",
                    receivedOn = "2026-10-26T08:00:00Z",
                    contentType = "application/json",
                    body = "{\"id\":\"evt_1\",\"type\":\"payment_intent.succeeded\"}",
                    truncated = false,
                    maskedFields = 2,
                    headers = new Dictionary<string, string> { ["Stripe-Signature"] = "t=…" },
                    answerStatus = 200,
                    parts = 1,
                },
            },
        });

        var response = await fixture.Client.Payments.GetPaymentsWebhookRunAsync(new GetPaymentsWebhookRun { Id = RunId });

        await Verifier.Verify(new { Sent = fixture.LastRequest, response!.Item }, VerifyConfig.VerifySettings);
    }
}
