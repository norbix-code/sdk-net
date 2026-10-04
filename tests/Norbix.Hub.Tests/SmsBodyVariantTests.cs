using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Two SMS routes take a body whose shape depends on a choice the caller
/// makes. `POST /campaigns` carries a `deliveryType` plus the one settings
/// object that matches it (unlike Push, the SMS request is flat: the server
/// reads the settings object named by `deliveryType`). `POST /integrations`
/// carries a provider request picked by the `provider` discriminator.
///
/// These tests assert that the discriminator and the chosen shape's own
/// fields actually reach the wire — a route test alone would not see them.
///
/// Nothing leaves the process and no SMS provider is contacted.
/// </summary>
[TestFixture]
public sealed class SmsBodyVariantTests
{
    private static async Task<object> SendAsync(
        Func<NorbixClient, Task> call,
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

    /// <summary>
    /// The four audiences the portal offers (All users · Specified users ·
    /// Phone numbers · Collection). `AccountUsers` is also in the enum but the
    /// request has no settings object for it, so it is not a shape a caller
    /// can send — see the item file's findings.
    /// </summary>
    public static IEnumerable<TestCaseData> CampaignDeliveries()
    {
        yield return new TestCaseData(
            new CreateSmsCampaignRequest
            {
                TemplateId = "tpl-1",
                IntegrationId = "sms-int-1",
                DeliveryType = SmsCampaignRecipientsSourceTypes.AllUsers,
                AllUsers = new SmsToAllUsersDeliverySettingsDto
                {
                    RecipientsSourceType = SmsCampaignRecipientsSourceTypes.AllUsers,
                    RolesNames = new HashSet<string> { "authenticated" },
                    UserTags = new HashSet<string> { "beta" },
                    CampaignTime = 1_800_000_000,
                },
            }
        ).SetName("AllUsers");

        yield return new TestCaseData(
            new CreateSmsCampaignRequest
            {
                TemplateId = "tpl-1",
                IntegrationId = "sms-int-1",
                DeliveryType = SmsCampaignRecipientsSourceTypes.SpecifiedUsers,
                SpecifiedUsers = new SmsToUsersDeliverySettingsDto
                {
                    RecipientsSourceType = SmsCampaignRecipientsSourceTypes.SpecifiedUsers,
                    Recipients = new HashSet<string> { "user-1", "user-2" },
                    CampaignTime = 1_800_000_000,
                },
            }
        ).SetName("SpecifiedUsers");

        yield return new TestCaseData(
            new CreateSmsCampaignRequest
            {
                TemplateId = "tpl-1",
                IntegrationId = "sms-int-1",
                DeliveryType = SmsCampaignRecipientsSourceTypes.PhoneNumbers,
                PhoneNumbers = new SmsToPhoneNumbersDeliverySettingsDto
                {
                    RecipientsSourceType = SmsCampaignRecipientsSourceTypes.PhoneNumbers,
                    PhoneNumbers = new HashSet<string> { "+37060000001", "+37060000002" },
                    CampaignTime = 1_800_000_000,
                },
            }
        ).SetName("PhoneNumbers");

        yield return new TestCaseData(
            new CreateSmsCampaignRequest
            {
                TemplateId = "tpl-1",
                IntegrationId = "sms-int-1",
                DeliveryType = SmsCampaignRecipientsSourceTypes.Collection,
                Collection = new SmsToCollectionRecordsDeliverySettingsDto
                {
                    RecipientsSourceType = SmsCampaignRecipientsSourceTypes.Collection,
                    SchemaName = "subscribers",
                    Fields = new HashSet<string> { "phone" },
                    FieldType = CollectionEmailCampaignRecipientField.User,
                    RoleNames = new HashSet<string> { "member" },
                    Languages = new HashSet<string> { "en" },
                    CampaignTime = 1_800_000_000,
                },
            }
        ).SetName("Collection");
    }

    [TestCaseSource(nameof(CampaignDeliveries))]
    public async Task Create_campaign_carries_the_delivery_shape(CreateSmsCampaignRequest campaign)
    {
        var sent = await SendAsync(client =>
            client.Notifications.CreateSmsCampaignAsync(campaign)
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"SmsBodyVariantTests.Campaign.{campaign.DeliveryType}");
        await Verifier.Verify(sent, settings);
    }

    /// <summary>
    /// The Fake provider is the sandbox: it accepts a send and never contacts
    /// a real SMS service. The server builds the whole integration itself, so
    /// the body only needs the provider discriminator.
    /// </summary>
    [Test]
    public async Task Save_integration_carries_the_fake_provider()
    {
        var sent = await SendAsync(client =>
            client.Notifications.SaveSmsIntegrationAsync(
                new SaveSmsIntegration
                {
                    Integration = new SmsIntegrationRequest
                    {
                        Provider = SmsProvider.Fake,
                        IntegrationName = "Test Me",
                        IsEnabled = true,
                    },
                }
            )
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("SmsBodyVariantTests.Integration.Fake");
        await Verifier.Verify(sent, settings);
    }

    /// <summary>
    /// Which provider request shapes a caller can build from the generated
    /// types, next to the providers the gateway knows. Today the generated
    /// types hold only the base <see cref="SmsIntegrationRequest"/>: the
    /// gateway's type-generation anchor leaves the eight SMS provider request
    /// records out (gateway `Community.Hub/Internals.cs`, "omitted until
    /// Hub.Sms is referenced"), so a real provider's credentials cannot be
    /// typed from .NET. When the anchor is fixed and the types regenerated,
    /// this snapshot changes and the provider cases above must grow with it.
    /// </summary>
    [Test]
    public async Task Provider_request_types_a_caller_can_build()
    {
        var baseType = typeof(SmsIntegrationRequest);
        var shapes = baseType
            .Assembly.GetTypes()
            .Where(t => t != baseType && baseType.IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("SmsBodyVariantTests.ProviderRequestTypes");
        await Verifier.Verify(
            new
            {
                Base = baseType.Name,
                // Verify drops an empty list, so say it in words.
                ProviderRequestTypes = shapes.Count == 0
                    ? new List<string> { "(none — only the base request exists)" }
                    : shapes,
                ProvidersTheGatewayKnows = Enum.GetNames<SmsProvider>(),
            },
            settings
        );
    }
}
