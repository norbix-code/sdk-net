using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Route 28 of the marketplace contract (gateway item oauth): start the OAuth
/// sign-in of an MCP connection. The coverage snapshot sends a placeholder id;
/// this case pins a real connection id in the path and reads the authorize URL
/// back from the generated response. Route 29 (the callback) is not in the type
/// metadata and has no SDK method. Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class MarketplaceOAuthTests
{
    [Test]
    public async Task Sign_in_start_puts_the_connection_id_in_the_path_and_returns_the_authorize_url()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });
        fixture.RespondNext(new
        {
            authorizeUrl = "https://slack.com/oauth/v2/authorize?client_id=1&state=s1",
            redirectUrl = "https://hub.norbix.ai/v3/code/marketplace/oauth/callback",
            expiresAt = "2026-10-13T08:10:00Z",
        });

        var response = await fixture.Client.Code.StartMarketplaceIntegrationSignInAsync(
            new StartMarketplaceIntegrationSignIn { IntegrationViewId = "int_6Qp1dW2kRm8NsT4vYc0Hb" });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response!.AuthorizeUrl, response.RedirectUrl, response.ExpiresAt },
            VerifyConfig.VerifySettings);
    }
}
