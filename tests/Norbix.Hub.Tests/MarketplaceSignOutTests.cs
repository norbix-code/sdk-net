using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Route 30 of the marketplace contract (gateway item access-2b): sign an MCP
/// connection out of OAuth. This case pins a real connection id in the path and
/// reads WasSignedIn / Revoke back from the generated response. Nothing leaves
/// the process.
/// </summary>
[TestFixture]
public sealed class MarketplaceSignOutTests
{
    [Test]
    public async Task Sign_out_puts_the_connection_id_in_the_path_and_returns_the_revoke_outcome()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });
        fixture.RespondNext(new { wasSignedIn = true, revoke = "Revoked" });

        var response = await fixture.Client.Code.SignOutMarketplaceIntegrationAsync(
            new SignOutMarketplaceIntegration { IntegrationViewId = "int_6Qp1dW2kRm8NsT4vYc0Hb" });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response!.WasSignedIn, response.Revoke },
            VerifyConfig.VerifySettings);
    }
}
