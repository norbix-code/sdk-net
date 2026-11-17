using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;

namespace Norbix.Hub.Tests;

/// <summary>
/// The project's own host is for API calls only: every Hub call stays on the
/// shared Hub host (or the regional one), whatever the project id is.
/// Design: devops docs/tasks/clients-project-host.md.
/// </summary>
[TestFixture]
public sealed class ProjectHostTests
{
    private const string Germany = "pr_3vlTD52e9VK8lXzdv0SmWG";

    [Test]
    public async Task Hub_calls_stay_on_the_shared_hub_host()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ProjectId = Germany);
        await fixture.Client.Account.GetAccountRegionsAsync(new GetAccountRegions());

        Assert.That(
            fixture.LastRequestUri!.GetLeftPart(UriPartial.Authority),
            Is.EqualTo("https://hub.norbix.ai"));
    }

    [Test]
    public async Task Hub_calls_with_a_region_go_to_the_regional_hub_host()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Germany;
            o.Region = "nb-eu-germany";
        });
        await fixture.Client.Account.GetAccountRegionsAsync(new GetAccountRegions());

        Assert.That(
            fixture.LastRequestUri!.GetLeftPart(UriPartial.Authority),
            Is.EqualTo("https://nb-eu-germany.hub.norbix.ai"));
        Assert.That(fixture.LastRequest!.Headers["nb-region"], Is.EqualTo("nb-eu-germany"));
    }
}
