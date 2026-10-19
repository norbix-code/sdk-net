using System.Text.Json;

using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// <see cref="CodeFunctionEvent"/> reads the envelope a cloud function
/// receives (gateway <c>CodeFunctionEventDto</c>, written camelCase with
/// camelCase enums and nulls left out) and hands out the typed views. One
/// sample per source below, shaped like <c>GET /code/functions/event-sample</c>
/// answers; the snapshots hold the parsed view so a renamed slot or a
/// changed enum spelling shows up as a diff.
/// <para>
/// Nothing leaves the process: the client test sends the run-as token to an
/// in-memory handler.
/// </para>
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class CodeFunctionEventTests
{
    private static readonly string[] EnvironmentVariables =
    [
        "NORBIX_API_KEY",
        "NORBIX_BEARER_TOKEN",
        "NORBIX_ACCOUNT_ID",
        "NORBIX_ENV",
        "NORBIX_REGION",
        "NORBIX_LANGUAGE",
        "NORBIX_API_URL",
        "NORBIX_HUB_URL",
    ];

    private readonly Dictionary<string, string?> _savedVariables = new();

    [SetUp]
    public void ClearEnvironmentVariables()
    {
        foreach (var name in EnvironmentVariables)
        {
            _savedVariables[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TearDown]
    public void RestoreEnvironmentVariables()
    {
        foreach (var (name, value) in _savedVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private sealed record Order(string _id, string status, decimal total, string customerEmail);

    private sealed record Person(string id, string email, string? displayName);

    private sealed record Payout(string orderId, bool dryRun);

    private sealed record Settings(string acceptStatus, int imageWidth);

    private static object Describe(CodeFunctionEvent evt) =>
        new
        {
            evt.SpecVersion,
            evt.Id,
            evt.EventId,
            evt.Event,
            Source = evt.Source.ToString(),
            evt.CreatedOn,
            Context = new
            {
                evt.Context.AccountId,
                evt.Context.ProjectId,
                evt.Context.Environment,
                evt.Context.Region,
                evt.Context.HubUrl,
                evt.Context.ApiUrl,
                evt.Context.EchoUrl,
                evt.Context.TriggerId,
                evt.Context.TaskId,
                evt.Context.CorrelationId,
                Caller = evt.Context.Caller is null
                    ? null
                    : new { evt.Context.Caller.UserId, evt.Context.Caller.Email, Kind = evt.Context.Caller.Kind.ToString() },
                RunAs = evt.Context.RunAs is null
                    ? null
                    : new { evt.Context.RunAs.UserId, evt.Context.RunAs.AccessToken, evt.Context.RunAs.ExpiresAt },
            },
            Slots = new
            {
                Database = evt.Data.Database is not null,
                User = evt.Data.User is not null,
                File = evt.Data.File is not null,
                Payment = evt.Data.Payment is not null,
                Order = evt.Data.Order is not null,
                Ai = evt.Data.Ai is not null,
                Task = evt.Data.Task is not null,
                Payload = evt.Data.Payload is not null,
            },
            Config = evt.Config?.Keys.ToArray(),
        };

    [Test]
    public async Task Database_update_reads_old_and_new_as_the_record_type()
    {
        var evt = CodeFunctionEvent.Parse(Samples.DatabaseUpdated);

        var change = evt.AsDatabaseRecordChange<Order>();

        await Verifier.Verify(
            new { Envelope = Describe(evt), Change = change, Config = evt.ConfigAs<Settings>() },
            VerifyConfig.VerifySettings
        );
    }

    [Test]
    public async Task Database_insert_has_no_old_document()
    {
        var evt = CodeFunctionEvent.Parse(Samples.DatabaseInserted);

        var change = evt.AsDatabaseRecordChange<Order>();

        await Verifier.Verify(new { evt.Event, Change = change }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Database_truncated_change_carries_the_refs_instead_of_the_documents()
    {
        var evt = CodeFunctionEvent.Parse(Samples.DatabaseTruncated);

        var change = evt.AsDatabaseRecordChange<Order>();

        await Verifier.Verify(new { evt.Event, Change = change }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Membership_reads_the_user_change_raw_and_typed()
    {
        var evt = CodeFunctionEvent.Parse(Samples.MembershipUpdated);

        await Verifier.Verify(
            new { Envelope = Describe(evt), Raw = evt.AsUserChange(), Typed = evt.AsUserChange<Person>() },
            VerifyConfig.VerifySettings
        );
    }

    [Test]
    public async Task Files_reads_the_file_change()
    {
        var evt = CodeFunctionEvent.Parse(Samples.FileUploaded);

        await Verifier.Verify(
            new { Envelope = Describe(evt), Raw = evt.AsFileChange(), Typed = evt.AsFileChange<Dictionary<string, string>>() },
            VerifyConfig.VerifySettings
        );
    }

    [Test]
    public async Task Payments_reads_the_webhook_fact_with_no_caller()
    {
        var evt = CodeFunctionEvent.Parse(Samples.PaymentReceived);

        await Verifier.Verify(
            new { Envelope = Describe(evt), Payment = evt.AsPayment() },
            VerifyConfig.VerifySettings
        );
    }

    [Test]
    public async Task Payments_order_reads_the_stored_order_and_keeps_the_callback()
    {
        var evt = CodeFunctionEvent.Parse(Samples.PaymentOrderPaid);

        await Verifier.Verify(
            new { Envelope = Describe(evt), Order = evt.AsPaymentOrder(), Callback = evt.AsPayment() },
            VerifyConfig.VerifySettings
        );
    }

    [Test]
    public void A_webhook_payment_event_has_no_order_slot()
    {
        var evt = CodeFunctionEvent.Parse(Samples.PaymentReceived);

        var ex = Assert.Throws<InvalidOperationException>(() => evt.AsPaymentOrder());

        Assert.That(ex!.Message, Is.EqualTo(
            "AsPaymentOrder() needs data.order, but this 'payments.webhook.received' event (source 'payments') carries no such slot."));
    }

    [Test]
    public async Task Ai_reads_the_usage_event()
    {
        var evt = CodeFunctionEvent.Parse(Samples.AiCreditsWarning);

        await Verifier.Verify(
            new { Envelope = Describe(evt), Ai = evt.AsAiUsage() },
            VerifyConfig.VerifySettings
        );
    }

    [Test]
    public async Task Scheduler_reads_the_task_and_its_static_payload()
    {
        var evt = CodeFunctionEvent.Parse(Samples.SchedulerFired);

        await Verifier.Verify(
            new { Envelope = Describe(evt), Task = evt.AsScheduledTask(), Payload = evt.Payload<Payout>() },
            VerifyConfig.VerifySettings
        );
    }

    [Test]
    public async Task Manual_reads_the_payload_and_has_no_config()
    {
        var evt = CodeFunctionEvent.Parse(Samples.ManualInvoke);

        await Verifier.Verify(
            new
            {
                Envelope = Describe(evt),
                Payload = evt.Payload<Payout>(),
                RawPayload = evt.Data.Payload,
                Config = evt.ConfigAs<Settings>(),
            },
            VerifyConfig.VerifySettings
        );
    }

    [Test]
    public void A_view_of_another_source_names_the_slot_and_the_event()
    {
        var evt = CodeFunctionEvent.Parse(Samples.MembershipUpdated);

        var ex = Assert.Throws<InvalidOperationException>(() => evt.AsDatabaseRecordChange<Order>());

        Assert.That(ex!.Message, Is.EqualTo(
            "AsDatabaseRecordChange() needs data.database, but this 'membership.user.updated' event (source 'membership') carries no such slot."));
    }

    [Test]
    public void A_newer_spec_version_is_refused()
    {
        var ex = Assert.Throws<NorbixException>(() =>
            CodeFunctionEvent.Parse(Samples.DatabaseUpdated.Replace("\"specVersion\": \"1\"", "\"specVersion\": \"2\"", StringComparison.Ordinal)));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Code, Is.EqualTo(NorbixErrorCodes.UnsupportedFunctionEvent));
            Assert.That(ex.Message, Does.Contain("specVersion '2'"));
        });
    }

    [Test]
    public void A_body_that_is_not_an_envelope_is_refused()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Assert.Throws<NorbixException>(() => CodeFunctionEvent.Parse("not json"))!.Code,
                Is.EqualTo(NorbixErrorCodes.InvalidFunctionEvent));
            Assert.That(
                Assert.Throws<NorbixException>(() => CodeFunctionEvent.Parse("{\"event\":\"x\"}"))!.Code,
                Is.EqualTo(NorbixErrorCodes.InvalidFunctionEvent));
            Assert.That(
                Assert.Throws<NorbixException>(() => CodeFunctionEvent.Parse("[1,2]"))!.Code,
                Is.EqualTo(NorbixErrorCodes.InvalidFunctionEvent));
        });
    }

    [Test]
    public void A_JsonElement_outlives_the_document_it_came_from()
    {
        CodeFunctionEvent evt;
        using (var document = JsonDocument.Parse(Samples.DatabaseUpdated))
        {
            evt = CodeFunctionEvent.Parse(document.RootElement);
        }

        var change = evt.AsDatabaseRecordChange<Order>();

        Assert.Multiple(() =>
        {
            Assert.That(evt.Event, Is.EqualTo("database.record.updated"));
            Assert.That(change.New?.status, Is.EqualTo("processing"));
            Assert.That(evt.Raw.GetProperty("specVersion").GetString(), Is.EqualTo("1"));
        });
    }

    [Test]
    public async Task Client_options_send_the_run_as_token_to_the_event_hub_url()
    {
        var evt = CodeFunctionEvent.Parse(Samples.DatabaseUpdated);
        var options = evt.CreateClientOptions();

        var handler = new MockHttpHandler().RespondNoContentDefault();
        using (var client = new NorbixHubClient(options, handler))
        {
            await client.EchoAsync();
        }

        await Verifier.Verify(
            new
            {
                Options = new
                {
                    options.BearerToken,
                    options.ApiKey,
                    options.ProjectId,
                    options.AccountId,
                    options.Env,
                    options.Region,
                    options.HubBaseUrl,
                    options.ApiBaseUrl,
                },
                Host = handler.RequestUris[^1]?.GetLeftPart(UriPartial.Authority),
                Sent = handler.Requests[^1],
            },
            VerifyConfig.VerifySettings
        );
    }

    [Test]
    public void Client_options_need_a_run_as_token()
    {
        var evt = CodeFunctionEvent.Parse(Samples.PaymentReceived.Replace(
            "\"runAs\": { \"userId\": \"usr_service_1\", \"accessToken\": \"eyJ.run-as.token\", \"expiresAt\": \"2026-10-12T08:15:00Z\" }",
            "\"runAs\": { \"userId\": \"usr_service_1\" }",
            StringComparison.Ordinal));

        var ex = Assert.Throws<InvalidOperationException>(() => evt.CreateClientOptions());

        Assert.That(ex!.Message, Does.Contain("context.runAs.accessToken"));
    }

    /// <summary>
    /// One envelope per source, as the gateway's <c>CodeFunctionEnvelopeBuilder</c>
    /// writes them (camelCase, camelCase enums, nulls left out).
    /// </summary>
    private static class Samples
    {
        private const string Context =
            """
              "context": {
                "accountId": "acc_1",
                "projectId": "proj_1",
                "environment": "TEST",
                "region": "nb-eu-germany",
                "hubUrl": "https://hub.example.test",
                "apiUrl": "https://api.example.test",
                "echoUrl": "https://api.example.test/v3/echo",
                "triggerId": "trg_1",
                "correlationId": "corr-1",
                "caller": { "userId": "usr_caller_1", "email": "ada@example.test", "kind": "user" },
                "runAs": { "userId": "usr_service_1", "accessToken": "eyJ.run-as.token", "expiresAt": "2026-10-12T08:15:00Z" }
              }
            """;

        public const string DatabaseUpdated =
            $$"""
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000001",
              "eventId": "7a2e0000-0000-4000-8000-000000000002",
              "event": "database.record.updated",
              "source": "database",
              "createdOn": "2026-10-12T08:00:00Z",
            {{Context}},
              "data": {
                "database": {
                  "collection": "orders",
                  "schemaId": "sch_1",
                  "schemaVersion": 3,
                  "recordId": "6ac54a59212c29912eaddd81",
                  "old": { "_id": "6ac54a59212c29912eaddd81", "status": "new", "total": 42.5, "customerEmail": "ada@example.test" },
                  "new": { "_id": "6ac54a59212c29912eaddd81", "status": "processing", "total": 42.5, "customerEmail": "ada@example.test" },
                  "truncated": false
                }
              },
              "config": { "acceptStatus": "processing", "imageWidth": 568 }
            }
            """;

        public const string DatabaseInserted =
            $$"""
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000003",
              "eventId": "7a2e0000-0000-4000-8000-000000000004",
              "event": "database.record.inserted",
              "source": "database",
              "createdOn": "2026-10-12T08:00:00Z",
            {{Context}},
              "data": {
                "database": {
                  "collection": "orders",
                  "schemaId": "sch_1",
                  "schemaVersion": 3,
                  "recordId": "6ac54a59212c29912eaddd82",
                  "new": { "_id": "6ac54a59212c29912eaddd82", "status": "new", "total": 10, "customerEmail": "bob@example.test" },
                  "truncated": false
                }
              }
            }
            """;

        public const string DatabaseTruncated =
            $$"""
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000005",
              "eventId": "7a2e0000-0000-4000-8000-000000000006",
              "event": "database.record.replaced",
              "source": "database",
              "createdOn": "2026-10-12T08:00:00Z",
            {{Context}},
              "data": {
                "database": {
                  "collection": "orders",
                  "schemaId": "sch_1",
                  "schemaVersion": 3,
                  "recordId": "6ac54a59212c29912eaddd83",
                  "truncated": true,
                  "oldRef": { "kind": "document", "projectId": "proj_1", "integrationId": "int_db_1", "databaseIntegrationId": "int_db_1", "schemaId": "sch_1", "externalId": "6ac54a59212c29912eaddd83" },
                  "newRef": { "kind": "document", "projectId": "proj_1", "integrationId": "int_db_1", "databaseIntegrationId": "int_db_1", "schemaId": "sch_1", "externalId": "6ac54a59212c29912eaddd83" }
                }
              }
            }
            """;

        public const string MembershipUpdated =
            $$"""
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000007",
              "eventId": "7a2e0000-0000-4000-8000-000000000008",
              "event": "membership.user.updated",
              "source": "membership",
              "createdOn": "2026-10-12T08:00:00Z",
            {{Context}},
              "data": {
                "user": {
                  "userId": "usr_member_1",
                  "old": { "id": "usr_member_1", "email": "ada@example.test", "displayName": "Ada" },
                  "new": { "id": "usr_member_1", "email": "ada@example.test", "displayName": "Ada Lovelace" }
                }
              }
            }
            """;

        public const string FileUploaded =
            $$"""
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000009",
              "eventId": "7a2e0000-0000-4000-8000-000000000010",
              "event": "files.file.uploaded",
              "source": "files",
              "createdOn": "2026-10-12T08:00:00Z",
            {{Context}},
              "data": {
                "file": {
                  "new": { "id": "fil_1", "fileName": "invoice.pdf", "contentType": "application/pdf" },
                  "path": "invoices/2026/invoice.pdf"
                }
              }
            }
            """;

        public const string PaymentReceived =
            """
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000011",
              "eventId": "7a2e0000-0000-4000-8000-000000000012",
              "event": "payments.webhook.received",
              "source": "payments",
              "createdOn": "2026-10-12T08:00:00Z",
              "context": {
                "accountId": "acc_1",
                "projectId": "proj_1",
                "environment": "PROD",
                "hubUrl": "https://hub.example.test",
                "apiUrl": "https://api.example.test",
                "echoUrl": "https://api.example.test/v3/echo",
                "triggerId": "trg_2",
                "correlationId": "corr-2",
                "runAs": { "userId": "usr_service_1", "accessToken": "eyJ.run-as.token", "expiresAt": "2026-10-12T08:15:00Z" }
              },
              "data": {
                "payment": {
                  "integrationId": "int_pay_1",
                  "provider": "Stripe",
                  "providerEventId": "evt_stripe_1",
                  "eventName": "payment_intent.succeeded",
                  "status": "succeeded",
                  "amountMinor": 4250,
                  "currency": "EUR",
                  "externalIds": { "paymentIntent": "pi_1", "customer": "cus_1" },
                  "occurredOn": "2026-10-12T07:59:58Z",
                  "receivedOn": "2026-10-12T08:00:00Z"
                }
              }
            }
            """;

        public const string PaymentOrderPaid =
            """
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000021",
              "eventId": "7a2e0000-0000-4000-8000-000000000022",
              "event": "payments.order.paid",
              "source": "payments",
              "createdOn": "2026-10-19T08:05:00Z",
              "context": {
                "accountId": "acc_1",
                "projectId": "proj_1",
                "environment": "PROD",
                "hubUrl": "https://hub.example.test",
                "apiUrl": "https://api.example.test",
                "triggerId": "trg_3",
                "correlationId": "corr-3",
                "runAs": { "userId": "usr_service_1", "accessToken": "eyJ.run-as.token", "expiresAt": "2026-10-19T08:20:00Z" }
              },
              "data": {
                "payment": {
                  "integrationId": "int_montonio",
                  "provider": "Montonio",
                  "providerEventId": "order:mnt-1:PAID",
                  "orderId": "ord_2ToIiRM03X63TZaJ2dJdv2",
                  "eventName": "order.paid",
                  "status": "Succeeded",
                  "amountMinor": 630,
                  "currency": "EUR",
                  "externalIds": { "montonioOrder": "mnt-1" },
                  "occurredOn": "2026-10-19T08:04:58Z",
                  "receivedOn": "2026-10-19T08:05:00Z"
                },
                "order": {
                  "orderEventId": "ord_2ToIiRM03X63TZaJ2dJdv2:2",
                  "event": "payments.order.paid",
                  "from": "CheckoutStarted",
                  "to": "Paid",
                  "reason": "paid in attempt 1",
                  "order": {
                    "id": "ord_2ToIiRM03X63TZaJ2dJdv2",
                    "env": "PROD",
                    "buyerUserId": "usr_1szWVIyZFBV8HfvC4SfVw",
                    "merchantReference": "ad-1042-promo-1",
                    "currency": "EUR",
                    "priceVersion": "2026-10-01",
                    "lines": [
                      { "sku": "star-day", "title": "Star × day", "quantity": 21, "unitAmountMinor": 30, "amountMinor": 630 }
                    ],
                    "totalMinor": 630,
                    "metadata": { "adId": "ad_1042" },
                    "status": "Paid",
                    "createdOnUtc": "2026-10-19T08:00:00Z",
                    "updatedOnUtc": "2026-10-19T08:05:00Z",
                    "checkouts": [],
                    "settledMinor": 630,
                    "refundedMinor": 0,
                    "paidOnUtc": "2026-10-19T08:04:58Z",
                    "providerPaymentId": "mnt-1",
                    "mismatches": []
                  },
                  "payment": {
                    "integrationId": "int_montonio",
                    "provider": "Montonio",
                    "providerEventId": "order:mnt-1:PAID",
                    "orderId": "ord_2ToIiRM03X63TZaJ2dJdv2",
                    "eventName": "order.paid",
                    "status": "Succeeded",
                    "amountMinor": 630,
                    "currency": "EUR",
                    "receivedOn": "2026-10-19T08:05:00Z"
                  }
                }
              }
            }
            """;

        public const string AiCreditsWarning =
            $$"""
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000013",
              "eventId": "7a2e0000-0000-4000-8000-000000000014",
              "event": "ai.credits.warning",
              "source": "ai",
              "createdOn": "2026-10-12T08:00:00Z",
            {{Context}},
              "data": {
                "ai": {
                  "payload": { "creditsLeft": 120, "threshold": 0.1, "plan": "starter" },
                  "targetUserId": "usr_member_1"
                }
              }
            }
            """;

        public const string SchedulerFired =
            """
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000015",
              "eventId": "7a2e0000-0000-4000-8000-000000000016",
              "event": "scheduler.task.fired",
              "source": "scheduler",
              "createdOn": "2026-10-12T03:00:00Z",
              "context": {
                "accountId": "acc_1",
                "projectId": "proj_1",
                "environment": "PROD",
                "hubUrl": "https://hub.example.test",
                "apiUrl": "https://api.example.test",
                "echoUrl": "https://api.example.test/v3/echo",
                "taskId": "tsk_1",
                "correlationId": "corr-3",
                "caller": { "userId": "usr_service_1", "kind": "system" },
                "runAs": { "userId": "usr_service_1", "accessToken": "eyJ.run-as.token", "expiresAt": "2026-10-12T03:15:00Z" }
              },
              "data": {
                "task": { "taskId": "tsk_1", "name": "Nightly payouts", "firedAt": "2026-10-12T03:00:00Z" },
                "payload": { "orderId": "6ac54a59212c29912eaddd81", "dryRun": false }
              },
              "config": { "acceptStatus": "paid", "imageWidth": 0 }
            }
            """;

        public const string ManualInvoke =
            """
            {
              "specVersion": "1",
              "id": "9f1c0000-0000-4000-8000-000000000017",
              "eventId": "9f1c0000-0000-4000-8000-000000000017",
              "event": "manual.invoke",
              "source": "manual",
              "createdOn": "2026-10-12T08:00:00Z",
              "context": {
                "accountId": "acc_1",
                "projectId": "proj_1",
                "environment": "TEST",
                "hubUrl": "https://hub.example.test",
                "apiUrl": "https://api.example.test",
                "echoUrl": "https://api.example.test/v3/echo",
                "correlationId": "corr-4",
                "caller": { "userId": "usr_caller_1", "email": "ada@example.test", "kind": "apiKey" },
                "runAs": { "userId": "usr_caller_1", "accessToken": "eyJ.run-as.token", "expiresAt": "2026-10-12T08:15:00Z" }
              },
              "data": {
                "payload": { "orderId": "6ac54a59212c29912eaddd81", "dryRun": true }
              }
            }
            """;
    }
}
