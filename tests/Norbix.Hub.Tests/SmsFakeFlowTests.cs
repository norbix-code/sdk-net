using Norbix.Sdk.Tests;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The whole SMS path from one client, using only the Fake provider:
/// enable SMS → save a Fake integration → read it back → make it default →
/// test it → confirm delivery → create a template → list its tokens → render
/// it → create a campaign to phone numbers → read the campaign → list its
/// messages → read one message → read the stats → stop it.
///
/// Each step gets the JSON reply the gateway would send, written the way the
/// gateway writes it — enums as camelCase names (`"fake"`, `"phoneNumbers"`).
/// The snapshot holds every request that left the client and the values the
/// client read back, so it proves both directions of the wire. Nothing leaves
/// the process; no SMS service is contacted. Same shape as
/// <see cref="PushFakeFlowTests"/>.
/// </summary>
[TestFixture]
public sealed class SmsFakeFlowTests
{
    [Test]
    public async Task Fake_provider_flow_from_enable_to_campaign_stop()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });
        var sms = fixture.Client.Notifications;

        fixture.RespondNext(new { });
        await sms.EnableSmsAsync(new EnableSms());

        // The Fake needs only the provider: the server fixes its name
        // ("Test Me") and settings itself.
        fixture.RespondNext(new { id = "int-fake" });
        var saved = await sms.SaveSmsIntegrationAsync(
            new SaveSmsIntegration
            {
                Integration = new SmsIntegrationRequest { Provider = SmsProvider.Fake },
            }
        );

        fixture.RespondNext(
            new
            {
                item = new
                {
                    viewId = saved!.Id,
                    integrationName = "Test Me",
                    isEnabled = true,
                    provider = "fake",
                    requiresHumanDeliveryConfirmation = true,
                },
            }
        );
        var integration = await sms.GetSmsIntegrationAsync(
            new GetSmsIntegration { Id = saved.Id! }
        );

        fixture.RespondNext(new { });
        await sms.SetSmsIntegrationAsDefaultAsync(
            new SetSmsIntegrationAsDefaultRequest { Id = saved.Id! }
        );

        fixture.RespondNext(
            new { items = new[] { new { operation = "send", result = "ok" } } }
        );
        var test = await sms.TestSmsIntegrationAsync(
            new TestSmsIntegration { IntegrationId = saved.Id!, To = "+37060000000" }
        );

        fixture.RespondNext(new { });
        await sms.ConfirmSmsIntegrationHumanDeliveryAsync(
            new ConfirmSmsIntegrationHumanDeliveryRequest { IntegrationId = saved.Id! }
        );

        fixture.RespondNext(new { id = "tpl-1" });
        var template = await sms.CreateSmsTemplateAsync(
            new CreateSmsTemplateRequest
            {
                TemplateName = "welcome",
                CommunicationChannel = CommunicationChannel.Transactional,
                Translations = new HashSet<SmsMessageTranslationDto>
                {
                    new()
                    {
                        Language = "en",
                        Content = new SmsMessageContentDto
                        {
                            Subject = "Welcome",
                            Body = "Hi @Model.FirstName, your code is @Model.Code",
                        },
                    },
                },
            }
        );

        fixture.RespondNext(
            new { tokens = new Dictionary<string, string[]> { ["en"] = ["FirstName", "Code"] } }
        );
        var tokens = await sms.GetSmsMessageContentTokensAsync(
            new GetSmsMessageContentTokens { Id = template!.Id! }
        );

        fixture.RespondNext(new { text = "Hi Ada, your code is 4242", variables = Array.Empty<string>() });
        var rendered = await sms.RenderSmsAsync(
            new RenderSms
            {
                Code = "Hi @Model.FirstName, your code is @Model.Code",
                Tokens = new HashSet<TokenMappingDto>
                {
                    new() { Key = "FirstName", Value = "Ada" },
                    new() { Key = "Code", Value = "4242" },
                },
                IsForPreview = true,
            }
        );

        fixture.RespondNext(new { id = "cmp-1" });
        var campaign = await sms.CreateSmsCampaignAsync(
            new CreateSmsCampaignRequest
            {
                TemplateId = template.Id!,
                DeliveryType = SmsCampaignRecipientsSourceTypes.PhoneNumbers,
                PhoneNumbers = new SmsToPhoneNumbersDeliverySettingsDto
                {
                    RecipientsSourceType = SmsCampaignRecipientsSourceTypes.PhoneNumbers,
                    PhoneNumbers = new HashSet<string> { "+37060000000" },
                    MappedTokens = new HashSet<TokenMappingDto>
                    {
                        new() { Key = "Code", Value = "4242" },
                    },
                },
            }
        );

        fixture.RespondNext(
            new
            {
                smsCampaign = new
                {
                    id = campaign!.Id,
                    viewId = campaign.Id,
                    status = new { time = "2026-10-02T10:00:00Z", status = "started" },
                    recipients = new { recipientsSourceType = "phoneNumbers" },
                    template = new { viewId = template.Id, templateName = "welcome" },
                },
            }
        );
        var read = await sms.GetSmsCampaignAsync(new GetSmsCampaign { Id = campaign.Id! });

        fixture.RespondNext(
            new
            {
                list = new
                {
                    items = new[]
                    {
                        new
                        {
                            campaignId = campaign.Id,
                            batchId = "batch-1",
                            notificationId = "msg-1",
                            body = "Hi Ada, your code is 4242",
                            recipients = new
                            {
                                to = new[] { new { phoneNumber = "+37060000000" } },
                                hasMore = false,
                            },
                        },
                    },
                    hasMore = false,
                },
            }
        );
        var messages = await sms.GetSmsCampaignMessagesAsync(
            new GetSmsCampaignMessagesRequest { CampaignId = campaign.Id!, CampaignBatchId = "batch-1" }
        );
        var first = messages!.List!.Items.Single();

        fixture.RespondNext(
            new
            {
                campaignNotification = new
                {
                    campaignId = first.CampaignId,
                    batchId = first.BatchId,
                    notificationId = first.NotificationId,
                    body = first.Body,
                    statusHistory = new[] { new { time = "2026-10-02T10:00:01Z", status = "completed" } },
                },
            }
        );
        var message = await sms.GetSmsCampaignBatchNotificationAsync(
            new GetSmsCampaignBatchNotification
            {
                Id = first.CampaignId,
                BatchId = first.BatchId,
                NotificationId = first.NotificationId,
            }
        );

        fixture.RespondNext(
            new { stats = new { batches = 1, sent = 1, failed = 0, successRate = 1.0 } }
        );
        var stats = await sms.GetSmsCampaignStatisticsAsync(
            new GetSmsCampaignStatistics { Id = campaign.Id! }
        );

        fixture.RespondNext(new { });
        await sms.StopSmsCampaignAsync(new StopSmsCampaignRequest { Id = campaign.Id! });

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("SmsFakeFlowTests.Fake_provider_flow");
        await Verifier.Verify(
            new
            {
                Sent = fixture.RecordedRequests,
                Read = new
                {
                    IntegrationId = saved.Id,
                    IntegrationProvider = integration!.Item!.Provider,
                    IntegrationName = integration.Item.IntegrationName,
                    TestResults = test!.Items,
                    TemplateId = template.Id,
                    TemplateTokens = tokens!.Tokens,
                    RenderedText = rendered!.Text,
                    UnresolvedTokens = rendered.Variables,
                    CampaignId = campaign.Id,
                    CampaignStatus = read!.SmsCampaign!.Status!.Status,
                    CampaignAudience = read.SmsCampaign.Recipients.RecipientsSourceType,
                    MessageCount = messages.List.Items.Count,
                    MessageBody = message!.CampaignNotification!.Body,
                    MessageStatus = message.CampaignNotification.StatusHistory.Single().Status,
                    stats!.Stats,
                },
            },
            settings
        );
    }
}
