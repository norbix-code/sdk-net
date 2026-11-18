using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// The public project pages — <c>GET /{version}/public/projects/{ProjectId}/config</c>,
/// <c>/legal/{Kind}</c> and <c>/brand/{Kind}</c>. The gateway does not authenticate
/// them: an Admin Portal or a sign-in page reads them before anyone signs in.
/// <para>
/// So a client with no API key and no token can call all three, and a client
/// that holds a key does not send it. Each snapshot holds the verb, the exact
/// path, the query and the headers sent.
/// </para>
/// </summary>
[TestFixture]
public sealed class PublicProjectEndpointTests
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G'];

    [Test]
    public async Task Config_legal_and_brand_go_out_from_a_client_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.Respond("/config", new { displayName = "Shop", adminPortalEnabled = true });
        fixture.Respond("/legal/terms", new { kind = "terms", title = "Terms", body = "# Terms", available = true });
        fixture.RespondBytes("/brand/logo", Png, "image/png");

        var config = await fixture.Client.Public.GetPublicProjectConfigAsync(new GetPublicProjectConfig { ProjectId = "pr_1" });
        var terms = await fixture.Client.Public.GetPublicProjectLegalAsync(new GetPublicProjectLegal { ProjectId = "pr_1", Kind = "terms" });
        var logo = await fixture.Client.Public.GetPublicProjectBrandAssetAsync(new GetPublicProjectBrandAsset { ProjectId = "pr_1", Kind = "logo" });

        await Verifier.Verify(
            new
            {
                Sent = fixture.RecordedRequests,
                config!.DisplayName,
                terms!.Available,
                LogoBytes = logo?.Length,
            },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task A_client_with_an_api_key_does_not_send_it_to_the_public_pages()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/config", new { displayName = "Shop" });
        fixture.Respond("/legal/privacy", new { kind = "privacy", available = false });

        await fixture.Client.Public.GetPublicProjectConfigAsync(new GetPublicProjectConfig { ProjectId = "pr_1" });
        await fixture.Client.Public.GetPublicProjectLegalAsync(new GetPublicProjectLegal { ProjectId = "pr_1", Kind = "privacy" });

        await Verifier.Verify(fixture.RecordedRequests, VerifyConfig.VerifySettings);
    }
}
