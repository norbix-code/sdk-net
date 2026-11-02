using System.Text.Json;

using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

namespace Norbix.Hub.Tests;

/// <summary>
/// Workflow listings (gateway contract Code.Marketplace.md §11a): where each
/// parameter lands. Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class MarketplaceWorkflowListingTests
{
    private const string Listing = "ml_workflow_order_thanks_v1";

    private static NorbixTestFixture Fixture() => NorbixTestFixture.Create(o =>
    {
        o.AccountId = "test-account";
        o.BearerToken = "test-bearer";
    });

    [Test]
    public async Task Install_puts_the_listing_in_the_path_and_keeps_every_reference_key_as_written()
    {
        using var fixture = Fixture();
        fixture.RespondNext(new { workflow = new { workflowId = "wf_1", isPublished = false, hasDraft = true } });

        var response = await fixture.Client.Code.InstallMarketplaceWorkflowAsync(new InstallMarketplaceWorkflow
        {
            ListingViewId = Listing,
            Name = "Thanks",
            References = new Dictionary<string, string>
            {
                ["orders"] = "orders",
                ["emailIntegration"] = "int_1",
                ["Mixed_Key"] = "kept",
            },
        });

        var sent = fixture.LastRequest!;
        Assert.That(sent.Method, Is.EqualTo("POST"));
        Assert.That(sent.Path, Does.EndWith($"/code/marketplace/listings/{Listing}/install-workflow"));

        var body = JsonSerializer.SerializeToElement(sent.Body);
        var references = body.GetProperty("references");
        Assert.That(references.GetProperty("emailIntegration").GetString(), Is.EqualTo("int_1"));
        Assert.That(references.GetProperty("Mixed_Key").GetString(), Is.EqualTo("kept"));
        Assert.That(body.GetProperty("name").GetString(), Is.EqualTo("Thanks"));
        Assert.That(response!.Workflow!.WorkflowId, Is.EqualTo("wf_1"));
        Assert.That(response.Workflow.IsPublished, Is.False);
    }

    [Test]
    public async Task Browse_sends_kind_when_asked_and_nothing_when_not()
    {
        using var fixture = Fixture();

        fixture.RespondNext(new { });
        await fixture.Client.Code.GetMarketplaceListingsAsync(new GetMarketplaceListings { Kind = MarketplaceListingKind.Workflow });
        Assert.That(fixture.LastRequest!.Query, Does.Contain("kind=Workflow").IgnoreCase);

        fixture.RespondNext(new { });
        await fixture.Client.Code.GetMarketplaceListingsAsync(new GetMarketplaceListings());
        Assert.That(fixture.LastRequest!.Query ?? string.Empty, Does.Not.Contain("kind="));
    }
}
