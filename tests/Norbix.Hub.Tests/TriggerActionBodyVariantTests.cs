using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// A trigger's save body is polymorphic twice: <c>trigger</c> is declared as
/// the base <see cref="SaveTriggerRequest"/> (one subclass per trigger type)
/// and its <c>action</c> as the base <see cref="TriggerActionDto"/> (one
/// subclass per action: email, push, SMS, …). The server requires the
/// provider <c>integrationId</c> on email, push and SMS actions, and accepts
/// an optional <c>language</c> and <c>initiatorId</c>. These tests assert that
/// those fields reach the wire through both base-typed slots.
///
/// Nothing leaves the process and no provider is contacted.
/// </summary>
[TestFixture]
public sealed class TriggerActionBodyVariantTests
{
    private static async Task<object> SendAsync(
        Func<NorbixHubClient, Task> call,
        [System.Runtime.CompilerServices.CallerMemberName] string caller = ""
    )
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });
        fixture.RespondNoContentDefault();

        await call(fixture.Client);

        return fixture.LastRequest
            ?? throw new InvalidOperationException($"{caller} sent no request.");
    }

    public static IEnumerable<TestCaseData> MessageActions()
    {
        yield return new TestCaseData(
            (TriggerActionDto)
                new TriggerActionEmailDto
                {
                    Type = TriggerActionType.Email,
                    IntegrationId = "email-int-1",
                    TemplateId = "tpl-welcome",
                    Language = "lt",
                    InitiatorId = "usr_service_1",
                    DeliverySettings = new EmailCampaignDeliverySettingsDto
                    {
                        RecipientsSourceType = EmailCampaignRecipientsSourceTypes.AllUsers,
                    },
                }
        ).SetName("Email");

        yield return new TestCaseData(
            (TriggerActionDto)
                new TriggerActionPushDto
                {
                    Type = TriggerActionType.Push,
                    IntegrationId = "push-int-1",
                    TemplateId = "tpl-welcome",
                    Language = "lt",
                    InitiatorId = "usr_service_1",
                    DeliverySettings = new PushCampaignDeliverySettingsDto
                    {
                        RecipientsSourceType = PushCampaignRecipientsSourceTypes.AllUsers,
                    },
                }
        ).SetName("Push");

        yield return new TestCaseData(
            (TriggerActionDto)
                new TriggerActionSmsDto
                {
                    Type = TriggerActionType.Sms,
                    IntegrationId = "sms-int-1",
                    TemplateId = "tpl-welcome",
                    Language = "lt",
                    InitiatorId = "usr_service_1",
                    DeliverySettings = new SmsCampaignDeliverySettingsDto
                    {
                        RecipientsSourceType = SmsCampaignRecipientsSourceTypes.AllUsers,
                    },
                }
        ).SetName("Sms");
    }

    [TestCaseSource(nameof(MessageActions))]
    public async Task Save_membership_trigger_carries_the_action_integration_language_and_initiator(
        TriggerActionDto action
    )
    {
        var sent = await SendAsync(client =>
            client.Membership.SaveMembershipTriggerAsync(
                new SaveMembershipTrigger
                {
                    Trigger = new MembershipTriggerRequest
                    {
                        Type = TriggerType.Membership,
                        When = MembershipTriggerType.OnRegistered,
                        Name = "Welcome message",
                        IsEnabled = true,
                        Action = action,
                    },
                }
            )
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"TriggerActionBodyVariantTests.Action.{action.Type}");
        await Verifier.Verify(sent, settings);
    }

    /// <summary>
    /// "Run a cloud function" (<see cref="TriggerActionCodeDto"/>: function
    /// reference, invocation type, config) and "Run a workflow"
    /// (<see cref="TriggerActionWorkflowDto"/>: workflow reference, payload).
    /// Neither carries a run-as user: the trigger's
    /// <c>executionIdentityId</c> says who runs. Same values as the Go twin
    /// <c>TestTriggerSaveSendsCodeAndWorkflowActions</c>.
    /// </summary>
    public static IEnumerable<TestCaseData> CodeAndWorkflowActions()
    {
        yield return new TestCaseData(
            (TriggerActionDto)
                new TriggerActionCodeDto
                {
                    Type = TriggerActionType.Code,
                    FunctionRef = new FunctionRefDto
                    {
                        IntegrationId = "int_lambda",
                        FunctionId = "notify-sales",
                        Qualifier = "live",
                    },
                    InvocationType = CodeInvocationType.Async,
                    Config = "{\"source\":\"net\"}",
                }
        ).SetName("Code");

        yield return new TestCaseData(
            (TriggerActionDto)
                new TriggerActionWorkflowDto
                {
                    Type = TriggerActionType.Workflow,
                    WorkflowRef = "wf_onboard",
                    Payload = new Dictionary<string, string> { ["source"] = "net" },
                }
        ).SetName("Workflow");
    }

    private static Task VerifyActionAsync(object sent, string host, TriggerActionDto action)
    {
        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"TriggerActionBodyVariantTests.{host}.{action.Type}");
        return Verifier.Verify(sent, settings);
    }

    [TestCaseSource(nameof(CodeAndWorkflowActions))]
    public async Task Save_schema_trigger_sends_the_code_or_workflow_action(TriggerActionDto action)
    {
        var sent = await SendAsync(client =>
            client.Database.SaveSchemaTriggerAsync(
                new SaveSchemaTrigger
                {
                    Trigger = new SchemaTriggerRequest
                    {
                        Type = TriggerType.Schema,
                        SchemaId = "sch_orders",
                        When = SchemaTriggerType.OnInserted,
                        Name = $"run {action.Type}",
                        IsEnabled = true,
                        ExecutionIdentityId = "usr_1",
                        Action = action,
                    },
                }
            )
        );

        await VerifyActionAsync(sent, "Database", action);
    }

    [TestCaseSource(nameof(CodeAndWorkflowActions))]
    public async Task Save_files_trigger_sends_the_code_or_workflow_action(TriggerActionDto action)
    {
        var sent = await SendAsync(client =>
            client.Files.SaveFilesTriggerAsync(
                new SaveFilesTrigger
                {
                    Trigger = new FilesTriggerRequest
                    {
                        Type = TriggerType.Files,
                        When = FilesTriggerType.OnFileUploaded,
                        Name = $"run {action.Type}",
                        IsEnabled = true,
                        ExecutionIdentityId = "usr_1",
                        Action = action,
                    },
                }
            )
        );

        await VerifyActionAsync(sent, "Files", action);
    }

    [TestCaseSource(nameof(CodeAndWorkflowActions))]
    public async Task Save_membership_trigger_sends_the_code_or_workflow_action(TriggerActionDto action)
    {
        var sent = await SendAsync(client =>
            client.Membership.SaveMembershipTriggerAsync(
                new SaveMembershipTrigger
                {
                    Trigger = new MembershipTriggerRequest
                    {
                        Type = TriggerType.Membership,
                        When = MembershipTriggerType.OnRegistered,
                        Name = $"run {action.Type}",
                        IsEnabled = true,
                        ExecutionIdentityId = "usr_1",
                        Action = action,
                    },
                }
            )
        );

        await VerifyActionAsync(sent, "Membership", action);
    }

    [TestCaseSource(nameof(CodeAndWorkflowActions))]
    public async Task Save_payments_trigger_sends_the_code_or_workflow_action(TriggerActionDto action)
    {
        var sent = await SendAsync(client =>
            client.Payments.SavePaymentsTriggerAsync(
                new SavePaymentsTrigger
                {
                    Trigger = new PaymentTriggerRequest
                    {
                        Type = TriggerType.Payments,
                        When = PaymentTriggerType.OnOrderPaid,
                        Name = $"run {action.Type}",
                        IsEnabled = true,
                        ExecutionIdentityId = "usr_1",
                        Action = action,
                    },
                }
            )
        );

        await VerifyActionAsync(sent, "Payments", action);
    }

    /// <summary>
    /// The generated types have no <c>AiTriggerRequest</c> (typegen gap), so
    /// the AI save carries the base <see cref="SaveTriggerRequest"/>, as the
    /// generated <see cref="SaveAiProjectTrigger"/> declares it.
    /// </summary>
    [TestCaseSource(nameof(CodeAndWorkflowActions))]
    public async Task Save_ai_trigger_sends_the_code_or_workflow_action(TriggerActionDto action)
    {
        var sent = await SendAsync(client =>
            client.Ai.SaveAiProjectTriggerAsync(
                new SaveAiProjectTrigger
                {
                    Trigger = new SaveTriggerRequest
                    {
                        Type = TriggerType.Ai,
                        Name = $"run {action.Type}",
                        IsEnabled = true,
                        ExecutionIdentityId = "usr_1",
                        Action = action,
                    },
                }
            )
        );

        await VerifyActionAsync(sent, "Ai", action);
    }
}
