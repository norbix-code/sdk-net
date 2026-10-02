using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The public link routes of the notification modules — one-click
/// unsubscribe, preferences by signed unsubscribe link, and the three signed
/// preview links. The gateway does not authenticate them
/// (<c>requiresAuth</c> is not true), so the generated request types carry
/// <c>INorbixOptionalAuth</c> and the client sends them with the
/// <c>Optional</c> scope:
/// <list type="bullet">
/// <item>no API key and no bearer token → the request still goes out, with no
/// <c>Authorization</c> header (before: <c>NORBIX_NOT_AUTHENTICATED</c>);</item>
/// <item>a configured key → it is sent as usual.</item>
/// </list>
/// Every test builds its own client and fake response; nothing leaves the
/// process and no email provider is contacted.
/// </summary>
[TestFixture]
public sealed class EmailPublicLinkTests
{
    private const string Token = "signed-link-abc";

    [Test]
    public async Task OneClickUnsubscribe_works_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondNoContentDefault();

        await fixture.Client.Email.OneClickUnsubscribeAsync(new OneClickUnsubscribeRequest());

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetEmailPreferencesByLink_works_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.Respond("/email/preferences", new { item = new { emailAddress = "ada@example.test", unsubscribedFromMarketing = true } });

        var response = await fixture.Client.Email.GetEmailPreferencesByLinkAsync(
            new GetEmailPreferencesByLinkRequest { Token = Token });

        await Verifier.Verify(new { Sent = fixture.LastRequest, response?.Item }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task GetEmailPreferencesByLink_still_sends_a_configured_key()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/email/preferences", new { item = new { emailAddress = "ada@example.test" } });

        await fixture.Client.Email.GetEmailPreferencesByLinkAsync(
            new GetEmailPreferencesByLinkRequest { Token = Token });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task PreviewEmailNotification_works_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.Respond("/notifications/email/preview", new { });

        await fixture.Client.Notifications.PreviewEmailNotificationAsync(
            new PreviewEmailNotification { Hash = Token });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task PreviewSmsNotification_works_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.Respond("/notifications/sms/preview", new { });

        await fixture.Client.Notifications.PreviewSmsNotificationAsync(
            new PreviewSmsNotification { Hash = Token });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task PreviewPushNotification_works_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.Respond("/notifications/push/preview", new { });

        await fixture.Client.Notifications.PreviewPushNotificationAsync(
            new PreviewPushNotification { Hash = Token });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    /// <summary>
    /// The comparison that makes the tests above mean something: the same
    /// client, calling a project-scoped Email route, is still refused.
    /// </summary>
    [Test]
    public async Task A_project_email_route_still_refuses_a_client_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);

        NorbixException? thrown = null;
        try
        {
            await fixture.Client.Notifications.StopEmailCampaignAsync(new StopEmailCampaignRequest { Id = "camp_1" });
        }
        catch (NorbixException e)
        {
            thrown = e;
        }

        await Verifier.Verify(new { thrown?.Code, Sent = fixture.LastRequest }, VerifyConfig.VerifySettings);
    }
}
