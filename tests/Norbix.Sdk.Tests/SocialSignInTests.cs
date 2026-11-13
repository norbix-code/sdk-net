using System.Text.Json;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Project social sign-in and link identity (gateway 3fb6f5569): the start URL
/// builder, the code exchange and the two link calls. Each call test pins the
/// exact verb, path and body.
/// </summary>
[TestFixture]
public sealed class SocialSignInTests
{
    private static string BodyJson(RecordedRequest? request) =>
        JsonSerializer.Serialize(request!.Body);

    [Test]
    public void SocialSignInUrl_carries_the_project_id_and_no_env_for_PROD()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiBaseUrl = "https://api.test");

        Assert.That(
            fixture.Client.SocialSignInUrl("google"),
            Is.EqualTo("https://api.test/v2/membership/auth/social/google?projectId=test-project"));
        Assert.That(fixture.RecordedRequests, Is.Empty);
    }

    [Test]
    public void SocialSignInUrl_encodes_mode_and_returnUrl()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiBaseUrl = "https://api.test");

        var url = fixture.Client.SocialSignInUrl(
            "github",
            mode: "Admin portal & app",
            returnUrl: "https://app.example.com/oauth/callback?next=/a b&x=1");

        Assert.That(url, Is.EqualTo(
            "https://api.test/v2/membership/auth/social/github?projectId=test-project"
            + "&mode=Admin%20portal%20%26%20app"
            + "&returnUrl=https%3A%2F%2Fapp.example.com%2Foauth%2Fcallback%3Fnext%3D%2Fa%20b%26x%3D1"));
    }

    [Test]
    public void SocialSignInUrl_puts_a_non_PROD_env_in_the_query()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiBaseUrl = "https://api.test";
            o.Env = "TEST";
        });

        Assert.That(
            fixture.Client.SocialSignInUrl("okta"),
            Is.EqualTo("https://api.test/v2/membership/auth/social/okta?projectId=test-project&env=TEST"));
    }

    [Test]
    public async Task ExchangeSocialSignInCode_posts_the_code_without_a_token()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/membership/auth/social/google/exchange", new
        {
            bearerToken = "a.b.c",
            userId = "usr_1",
            displayName = "Anna",
            expiresAt = "2026-11-27T10:00:00Z",
            meta = new Dictionary<string, string> { ["credentialVersion"] = "v7" },
        });

        var answer = await fixture.Client.Membership.ExchangeSocialSignInCodeAsync(
            new ExchangeSocialSignInCodeRequest { Provider = "google", Code = "one-time" });

        var sent = fixture.LastRequest!;
        Assert.That(sent.Method, Is.EqualTo("POST"));
        Assert.That(sent.Path, Is.EqualTo("/v2/membership/auth/social/google/exchange"));
        Assert.That(BodyJson(sent), Is.EqualTo("""{"code":"one-time"}"""));
        Assert.That(sent.Headers.ContainsKey("norbix-project-id"), Is.True);
        Assert.That(sent.Headers.ContainsKey("Authorization"), Is.False, "an API key must not be sent");
        Assert.That(answer!.BearerToken, Is.EqualTo("a.b.c"));
        Assert.That(answer.UserId, Is.EqualTo("usr_1"));
        Assert.That(answer.Meta!["credentialVersion"], Is.EqualTo("v7"));
    }

    /// <summary>
    /// The gateway judges any token it gets, even on this open route: a JWT
    /// from before a password change is refused 401. The exchange is how a
    /// user signs in again, so a client that still holds an old JWT and a key
    /// sends neither.
    /// </summary>
    [Test]
    public async Task ExchangeSocialSignInCode_sends_no_credentials_even_with_a_jwt()
    {
        using var fixture = NorbixTestFixture.Create(o => o.BearerToken = "old.jwt");
        fixture.Respond("/membership/auth/social/google/exchange", new { bearerToken = "a.b.c" });

        await fixture.Client.Membership.ExchangeSocialSignInCodeAsync(
            new ExchangeSocialSignInCodeRequest { Provider = "google", Code = "one-time" });

        var sent = fixture.LastRequest!;
        Assert.That(sent.Headers.ContainsKey("Authorization"), Is.False);
        Assert.That(sent.Headers["norbix-project-id"], Is.EqualTo("test-project"));
    }

    [Test]
    public async Task StartLinkIdentity_posts_provider_mode_and_returnUrl()
    {
        using var fixture = NorbixTestFixture.Create(o => o.BearerToken = "jwt-1");
        fixture.Respond("/link-identity/start", new { authorizeUrl = "https://accounts.example/authorize" });

        var answer = await fixture.Client.Membership.StartLinkIdentityAsync(new StartLinkIdentityRequest
        {
            UserId = "usr_1",
            Provider = "google",
            Mode = "app",
            ReturnUrl = "https://app.example.com/settings",
        });

        var sent = fixture.LastRequest!;
        Assert.That(sent.Method, Is.EqualTo("POST"));
        Assert.That(sent.Path, Is.EqualTo("/v2/membership/auth/usr_1/link-identity/start"));
        Assert.That(
            BodyJson(sent),
            Is.EqualTo("""{"provider":"google","mode":"app","returnUrl":"https://app.example.com/settings"}"""));
        Assert.That(sent.Headers["Authorization"], Is.EqualTo("Bearer jwt-1"));
        Assert.That(answer!.AuthorizeUrl, Is.EqualTo("https://accounts.example/authorize"));
    }

    [Test]
    public async Task LinkIdentity_posts_the_link_code()
    {
        using var fixture = NorbixTestFixture.Create(o => o.BearerToken = "jwt-1");

        await fixture.Client.Membership.LinkIdentityAsync(
            new LinkIdentityRequest { UserId = "usr_1", Code = "link-code" });

        var sent = fixture.LastRequest!;
        Assert.That(sent.Method, Is.EqualTo("POST"));
        Assert.That(sent.Path, Is.EqualTo("/v2/membership/auth/usr_1/link-identity"));
        Assert.That(BodyJson(sent), Is.EqualTo("""{"code":"link-code"}"""));
        Assert.That(sent.Headers["Authorization"], Is.EqualTo("Bearer jwt-1"));
    }
}
