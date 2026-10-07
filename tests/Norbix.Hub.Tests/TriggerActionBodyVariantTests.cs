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
}
