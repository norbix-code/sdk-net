using Norbix.Sdk.Auth;
using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Auth surface: API key vs JWT bearer, login/logout flow. Each test does
/// a single SDK action and snapshots both the response and the recorded
/// HTTP requests. The `.verified.txt` file is the contract.
/// </summary>
[TestFixture]
public sealed class AuthenticationTests
{
    [Test]
    public async Task ApiKey_is_sent_as_Bearer_when_no_jwt()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = "service-key";
            o.ProjectId = "p1";
        });
        fixture.Respond("/auth/echo", new { ok = true });

        // Trigger a request that runs through the regular auth path. We
        // deliberately use LoginAsync's *unauthenticated* /auth path? — no,
        // that one doesn't add Authorization. Use the auto-generated `Echo`
        // endpoint instead (placeholder DTO in Generated/Api.dtos.cs).
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        await Verifier.Verify(fixture.RecordedRequests, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task BearerToken_wins_over_ApiKey_when_both_set()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = "service-key";
            o.BearerToken = "user-jwt";
            o.ProjectId = "p1";
        });

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Calling_without_auth_throws_NotAuthenticated()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = null;
            o.BearerToken = null;
            o.ProjectId = "p1";
        });

        try
        {
            await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
            await Verifier.Verify(new { Threw = false }, VerifyConfig.VerifySettings);
        }
        catch (NorbixException ex)
        {
            await Verifier.Verify(new
            {
                Threw = true,
                ex.Code,
                ex.Message,
                ex.StatusCode,
                fixture.Client.IsAuthenticated,
                FiredHttp = fixture.RecordedRequests.Count > 0,
            }, VerifyConfig.VerifySettings);
        }
    }

    [Test]
    public async Task LoginAsync_posts_to_auth_unauthenticated_then_uses_jwt()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = "service-key";
            o.ProjectId = "p1";
        });

        fixture.Respond("/auth", new
        {
            bearerToken = "fresh-jwt",
            refreshToken = "r1",
            userId = "u1",
            userName = "alice",
        });

        var session = await fixture.Client.LoginAsync(new LoginCredentials
        {
            UserName = "alice",
            Password = "secret",
        });

        // Follow up call after login: should use the JWT, not the API key.
        var authed = fixture.Client.WithBearerToken(session.BearerToken);
        await authed.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        await Verifier.Verify(new
        {
            session,
            ClientAuthenticated = fixture.Client.IsAuthenticated,
            AuthedAuthenticated = authed.IsAuthenticated,
            requests = fixture.RecordedRequests,
        }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task LoginAsync_defaults_provider_to_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = "service-key";
            o.ProjectId = "p1";
        });
        fixture.Respond("/auth", new { });

        await fixture.Client.LoginAsync(new LoginCredentials
        {
            UserName = "alice",
            Password = "secret",
        });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task WithoutBearerToken_reverts_to_api_key()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = "service-key";
            o.BearerToken = "old-jwt";
            o.ProjectId = "p1";
        });

        var serviceClient = fixture.Client.WithoutBearerToken();
        await serviceClient.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        await Verifier.Verify(new
        {
            ClientAuthenticated = fixture.Client.IsAuthenticated,
            ServiceClientAuthenticated = serviceClient.IsAuthenticated,
            request = fixture.LastRequest,
        }, VerifyConfig.VerifySettings);
    }
}
