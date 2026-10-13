using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// GET /{version}/code/marketplace/integrations/{IntegrationViewId}/tools returns
/// the stored tool catalog of an MCP connection. The coverage snapshot sends a
/// placeholder id; this case pins a real connection id in the path, no query
/// value for it, no body, and the catalog read back from the generated response.
/// Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class MarketplaceMcpToolsTests
{
    private static NorbixTestFixture NewFixture() =>
        NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });

    [Test]
    public async Task Tools_put_the_connection_id_in_the_path_with_no_body()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(new
        {
            tools = new[]
            {
                new
                {
                    name = "create_issue",
                    title = "Create issue",
                    description = "Open a new issue in a repository",
                    inputSchema = "{\"type\":\"object\",\"properties\":{\"title\":{\"type\":\"string\"}},\"required\":[\"title\"]}",
                    readOnlyHint = false,
                    destructiveHint = false,
                },
            },
            listedAt = "2026-10-13T08:00:00Z",
            catalogHash = "4ce4259bec02f283",
            truncated = false,
        });

        var response = await fixture.Client.Code.GetMarketplaceIntegrationToolsAsync(
            new GetMarketplaceIntegrationTools { IntegrationViewId = "int_7Hq2mcpServer" });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response!.Tools, response.CatalogHash, response.Truncated },
            VerifyConfig.VerifySettings);
    }
}
