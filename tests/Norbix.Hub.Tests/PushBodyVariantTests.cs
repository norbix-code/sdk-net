using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Two Push routes take a polymorphic body: `POST /campaigns` carries one of
/// five audience shapes, `POST /integrations` one of eight provider shapes.
/// The server picks the shape from a discriminator in the body, so these
/// tests assert the discriminator and the shape's own fields actually reach
/// the wire — a route test alone would not see them.
///
/// Only the Fake provider is exercised for a real send path; the other seven
/// are shape assertions that never leave the process.
/// </summary>
[TestFixture]
public sealed class PushBodyVariantTests
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

    public static IEnumerable<TestCaseData> CampaignAudiences()
    {
        yield return new TestCaseData(
            (PushCampaignRequest)
                new PushToAllUsersRequest
                {
                    TemplateId = "tpl-1",
                    RolesNames = new HashSet<string> { "admin" },
                    UserTags = new HashSet<string> { "beta" },
                }
        ).SetName("AllUsers");

        yield return new TestCaseData(
            (PushCampaignRequest)
                new PushToUsersRequest
                {
                    TemplateId = "tpl-1",
                    UserRecipients = new HashSet<string> { "user-1", "user-2" },
                }
        ).SetName("SpecifiedUsers");

        yield return new TestCaseData(
            (PushCampaignRequest)
                new PushToAccountUsersRequest
                {
                    TemplateId = "tpl-1",
                    UserRecipients = new HashSet<string> { "account-user-1" },
                }
        ).SetName("AccountUsers");

        yield return new TestCaseData(
            (PushCampaignRequest)
                new PushToCollectionRecordsRequest
                {
                    TemplateId = "tpl-1",
                    SchemaName = "subscribers",
                    Fields = new HashSet<string> { "userId" },
                    FieldType = CollectionEmailCampaignRecipientField.User,
                    RoleNames = new HashSet<string> { "member" },
                    Languages = new HashSet<string> { "en" },
                }
        ).SetName("Collection");

        yield return new TestCaseData(
            (PushCampaignRequest)
                new PushToDevicesRequest
                {
                    TemplateId = "tpl-1",
                    Devices = new HashSet<PushDeviceDeliveryTokenRequest>
                    {
                        new()
                        {
                            Token = "device-token-1",
                            DeliveryFamily = PushDeviceDeliveryFamily.Ios,
                        },
                    },
                }
        ).SetName("Devices");
    }

    [TestCaseSource(nameof(CampaignAudiences))]
    public async Task Create_campaign_carries_the_audience_shape(PushCampaignRequest campaign)
    {
        var sent = await SendAsync(client =>
            client.Notifications.CreatePushCampaignAsync(
                new CreatePushCampaignRequest { Campaign = campaign }
            )
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"PushBodyVariantTests.Campaign.{campaign.Source}");
        await Verifier.Verify(sent, settings);
    }

    public static IEnumerable<TestCaseData> IntegrationProviders()
    {
        yield return new TestCaseData(
            (PushIntegrationRequest)
                new FakePushIntegrationRequest { IntegrationName = "fake", IsEnabled = true }
        ).SetName("Fake");

        yield return new TestCaseData(
            (PushIntegrationRequest)
                new AndroidFirebasePushIntegrationRequest
                {
                    IntegrationName = "firebase",
                    Provider = PushProvider.AndroidFirebase,
                    ProjectId = "fb-project",
                    ClientEmail = "svc@example.test",
                    ServiceAccountJson = "{}",
                }
        ).SetName("AndroidFirebase");

        yield return new TestCaseData(
            (PushIntegrationRequest)
                new AppleApnsPushIntegrationRequest
                {
                    IntegrationName = "apns",
                    Provider = PushProvider.AppleApns,
                    TeamId = "team-1",
                    AppBundleId = "test.bundle",
                    KeyId = "key-1",
                    PrivateKey = "private-key",
                    IsProduction = false,
                }
        ).SetName("AppleApns");

        yield return new TestCaseData(
            (PushIntegrationRequest)
                new ChromePluginPushIntegrationRequest
                {
                    IntegrationName = "chrome-plugin",
                    Provider = PushProvider.CodeMashChromePlugin,
                    ExtensionId = "ext-1",
                    VapidPublicKey = "pub",
                    VapidPrivateKey = "priv",
                    Subject = "mailto:ops@example.test",
                }
        ).SetName("ChromePlugin");

        yield return new TestCaseData(
            (PushIntegrationRequest)
                new ChromeWebPushIntegrationRequest
                {
                    IntegrationName = "chrome-web",
                    Provider = PushProvider.ChromeWeb,
                    VapidPublicKey = "pub",
                    VapidPrivateKey = "priv",
                    Subject = "mailto:ops@example.test",
                }
        ).SetName("ChromeWeb");

        yield return new TestCaseData(
            (PushIntegrationRequest)
                new EdgeWebPushIntegrationRequest
                {
                    IntegrationName = "edge-web",
                    Provider = PushProvider.EdgeWeb,
                    VapidPublicKey = "pub",
                    VapidPrivateKey = "priv",
                    Subject = "mailto:ops@example.test",
                }
        ).SetName("EdgeWeb");

        yield return new TestCaseData(
            (PushIntegrationRequest)
                new FirefoxWebPushIntegrationRequest
                {
                    IntegrationName = "firefox-web",
                    Provider = PushProvider.FirefoxWeb,
                    VapidPublicKey = "pub",
                    VapidPrivateKey = "priv",
                    Subject = "mailto:ops@example.test",
                }
        ).SetName("FirefoxWeb");

        yield return new TestCaseData(
            (PushIntegrationRequest)
                new SafariPushIntegrationRequest
                {
                    IntegrationName = "safari",
                    Provider = PushProvider.SafariPush,
                    WebsitePushId = "web.test.push",
                    CertificateP12Base64 = "base64",
                    CertificatePassword = "pwd",
                }
        ).SetName("SafariPush");
    }

    // Named by concrete type, not by `integration.Provider`: every generated
    // provider class re-declares `Provider`, shadowing the base one, so reading
    // it through the base reference always returns the base's default.
    [TestCaseSource(nameof(IntegrationProviders))]
    public async Task Save_integration_carries_the_provider_shape(
        PushIntegrationRequest integration
    )
    {
        var sent = await SendAsync(client =>
            client.Notifications.SavePushIntegrationAsync(
                new SavePushIntegration { Integration = integration }
            )
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"PushBodyVariantTests.Integration.{integration.GetType().Name}");
        await Verifier.Verify(sent, settings);
    }
}
