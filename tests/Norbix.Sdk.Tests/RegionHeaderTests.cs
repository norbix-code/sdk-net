using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;

namespace Norbix.Sdk.Tests;

/// <summary>
/// The <c>nb-region</c> header — injected ONLY when a region is resolved
/// (per-call <see cref="NorbixClient.WithRegion"/> override → options
/// <c>Region</c> → <c>NORBIX_REGION</c> → unset; there is no default region,
/// unlike the env header's PROD) — plus regional base-URL composition: the
/// SDK-default base URL becomes <c>https://{region}.api.norbix.ai</c>, while
/// a user-supplied custom base URL is never rewritten.
/// </summary>
[TestFixture]
public sealed class RegionHeaderTests
{
    [Test]
    public async Task Unset_region_omits_header()
    {
        using var fixture = NorbixTestFixture.Create();
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("nb-region"), Is.False);
    }

    [Test]
    public async Task Configured_region_sets_header()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Region = "nb-eu-germany");
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers["nb-region"], Is.EqualTo("nb-eu-germany"));
    }

    [Test]
    public async Task WithRegion_overrides_configured_region()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Region = "nb-eu-germany");
        var scoped = fixture.Client.WithRegion("nb-us-east");
        await scoped.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers["nb-region"], Is.EqualTo("nb-us-east"));
    }

    [Test]
    public async Task WithRegion_null_clears_header()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Region = "nb-eu-germany");
        var scoped = fixture.Client.WithRegion(null);
        await scoped.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("nb-region"), Is.False);
    }

    [Test]
    public void Region_env_var_is_read_by_ApplyEnvironment()
    {
        var opts = new NorbixClientOptions { ProjectId = "p" }
            .ApplyEnvironment(new Dictionary<string, string?> { ["NORBIX_REGION"] = "nb-eu-germany" });
        Assert.That(opts.Region, Is.EqualTo("nb-eu-germany"));
    }

    [Test]
    public void Explicit_region_wins_over_env_var()
    {
        var opts = new NorbixClientOptions { ProjectId = "p", Region = "nb-us-east" }
            .ApplyEnvironment(new Dictionary<string, string?> { ["NORBIX_REGION"] = "nb-eu-germany" });
        Assert.That(opts.Region, Is.EqualTo("nb-us-east"));
    }

    [Test]
    public async Task Region_composes_default_api_base_url()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Region = "nb-eu-germany");
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(
            fixture.LastRequestUri!.GetLeftPart(UriPartial.Authority),
            Is.EqualTo("https://nb-eu-germany.api.norbix.ai"));
    }

    [Test]
    public async Task Region_composes_default_hub_base_url()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Region = "nb-eu-germany");
        // The test suite targets the Norbix.Api package, so Hub modules are
        // not compiled in — drive the (internal) transport directly to cover
        // the Hub-target composition path.
        await fixture.Client.Transport.SendAsync<Norbix.Sdk.Types.EmptyResponse>(
            new Norbix.Sdk.Transport.NorbixRequestSpec
            {
                Target = Norbix.Sdk.Transport.NorbixTarget.Hub,
                Path = "/{version}/account/regions",
                Method = "GET",
                Request = null,
                PathParams = Array.Empty<string>(),
                Scope = Norbix.Sdk.Transport.NorbixScope.Project,
            });
        Assert.That(
            fixture.LastRequestUri!.GetLeftPart(UriPartial.Authority),
            Is.EqualTo("https://nb-eu-germany.hub.norbix.ai"));
    }

    [Test]
    public async Task Unset_region_keeps_default_base_url()
    {
        using var fixture = NorbixTestFixture.Create();
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(
            fixture.LastRequestUri!.GetLeftPart(UriPartial.Authority),
            Is.EqualTo("https://api.norbix.ai"));
    }

    [Test]
    public async Task Custom_base_url_is_never_rewritten()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.Region = "nb-eu-germany";
            o.ApiBaseUrl = "https://my-gateway.example.com";
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(
            fixture.LastRequestUri!.GetLeftPart(UriPartial.Authority),
            Is.EqualTo("https://my-gateway.example.com"));
        // The header still rides along — only the URL is left alone.
        Assert.That(fixture.LastRequest!.Headers["nb-region"], Is.EqualTo("nb-eu-germany"));
    }

    [Test]
    public async Task Echo_response_carries_regions()
    {
        using var fixture = NorbixTestFixture.Create()
            .Respond("/echo", new
            {
                regions = new[]
                {
                    new
                    {
                        code = "nb-eu-germany",
                        displayName = "Germany (EU)",
                        apiUrl = "https://nb-eu-germany.api.norbix.ai",
                        hubUrl = "https://nb-eu-germany.hub.norbix.ai",
                    },
                },
            });
        var response = await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(response!.Regions, Has.Count.EqualTo(1));
        Assert.That(response.Regions![0].Code, Is.EqualTo("nb-eu-germany"));
        Assert.That(response.Regions[0].DisplayName, Is.EqualTo("Germany (EU)"));
        Assert.That(response.Regions[0].ApiUrl, Is.EqualTo("https://nb-eu-germany.api.norbix.ai"));
        Assert.That(response.Regions[0].HubUrl, Is.EqualTo("https://nb-eu-germany.hub.norbix.ai"));
    }
}
