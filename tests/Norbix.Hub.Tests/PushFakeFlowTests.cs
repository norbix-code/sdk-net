using Norbix.Sdk.Tests;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The whole push path from one client, using only the Fake provider:
/// enable push → save a Fake integration → read it back → make it default →
/// test it → confirm delivery → create a template → register a device →
/// create a campaign to that device → read the campaign → read its stats.
///
/// Each step gets the JSON reply the gateway would send, written the way the
/// gateway writes it — enums as camelCase names (`"fake"`, `"devices"`). The
/// snapshot holds every request that left the client and the values the client
/// read back, so it proves both directions of the wire. Nothing leaves the
/// process; no push service is contacted.
/// </summary>
[TestFixture]
public sealed class PushFakeFlowTests
{
    [Test]
    public async Task Fake_provider_flow_from_enable_to_campaign_stats()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });
        var push = fixture.Client.Notifications;

        fixture.RespondNext(new { });
        await push.EnablePushAsync(new EnablePush());

        fixture.RespondNext(new { id = "int-fake" });
        var saved = await push.SavePushIntegrationAsync(
            new SavePushIntegration
            {
                Integration = new FakePushIntegrationRequest
                {
                    IntegrationName = "fake",
                    IsEnabled = true,
                },
            }
        );

        fixture.RespondNext(
            new
            {
                item = new
                {
                    viewId = saved!.Id,
                    integrationName = "fake",
                    isEnabled = true,
                    provider = "fake",
                    requiresHumanDeliveryConfirmation = true,
                },
            }
        );
        var integration = await push.GetPushIntegrationAsync(
            new GetPushIntegration { Id = saved.Id! }
        );

        fixture.RespondNext(new { });
        await push.SetPushIntegrationAsDefaultAsync(
            new SetPushIntegrationAsDefaultRequest { Id = saved.Id! }
        );

        fixture.RespondNext(
            new { items = new[] { new { operation = "send", result = "ok" } } }
        );
        var test = await push.TestPushIntegrationAsync(
            new TestPushIntegration
            {
                IntegrationId = saved.Id!,
                TestToken = "fake-device-token",
                DeliveryFamily = "ios",
            }
        );

        fixture.RespondNext(new { });
        await push.ConfirmPushIntegrationHumanDeliveryAsync(
            new ConfirmPushIntegrationHumanDeliveryRequest { IntegrationId = saved.Id! }
        );

        fixture.RespondNext(new { id = "tpl-1" });
        var template = await push.CreatePushTemplateAsync(
            new CreatePushTemplateRequest
            {
                TemplateName = "welcome",
                CommunicationChannel = CommunicationChannel.Transactional,
                Translations = new HashSet<PushMessageTranslationDto>
                {
                    new()
                    {
                        Language = "en",
                        Content = new PushMessageContentDto { Title = "Hi", Body = "Welcome" },
                    },
                },
            }
        );

        fixture.RespondNext(new { id = "dev-1" });
        var device = await push.RegisterDeviceAsync(
            new RegisterDevice
            {
                UserId = "user-1",
                ProjectId = "test-project",
                PushDeviceDto = new PushDeviceDto
                {
                    DeviceOs = "ios",
                    Token = "fake-device-token",
                    DeviceType = DeviceType.Phone,
                },
            }
        );

        fixture.RespondNext(new { id = "cmp-1" });
        var campaign = await push.CreatePushCampaignAsync(
            new CreatePushCampaignRequest
            {
                Campaign = new PushToDevicesRequest
                {
                    TemplateId = template!.Id!,
                    Devices = new HashSet<PushDeviceDeliveryTokenRequest>
                    {
                        new()
                        {
                            Token = "fake-device-token",
                            DeliveryFamily = PushDeviceDeliveryFamily.Ios,
                        },
                    },
                },
            }
        );

        fixture.RespondNext(
            new
            {
                item = new
                {
                    id = campaign!.Id,
                    viewId = campaign.Id,
                    status = new { time = "2026-09-19T10:00:00Z", status = "started" },
                    recipients = new { recipientsSourceType = "devices" },
                },
            }
        );
        var read = await push.GetPushCampaignAsync(new GetPushCampaign { Id = campaign.Id! });

        fixture.RespondNext(
            new
            {
                stats = new
                {
                    batches = 1,
                    sent = 1,
                    failed = 0,
                    successRate = 1.0,
                },
            }
        );
        var stats = await push.GetPushCampaignStatisticsAsync(
            new GetPushCampaignStatistics { Id = campaign.Id! }
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("PushFakeFlowTests.Fake_provider_flow");
        await Verifier.Verify(
            new
            {
                Sent = fixture.RecordedRequests,
                Read = new
                {
                    IntegrationId = saved.Id,
                    IntegrationProvider = integration!.Item!.Provider,
                    TestResults = test!.Items,
                    TemplateId = template.Id,
                    DeviceId = device!.Id,
                    CampaignId = campaign.Id,
                    CampaignStatus = read!.Item!.Status!.Status,
                    CampaignAudience = read.Item.Recipients.RecipientsSourceType,
                    stats!.Stats,
                },
            },
            settings
        );
    }
}
