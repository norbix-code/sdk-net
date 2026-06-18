using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;

namespace Norbix.Sdk.Tests;

/// <summary>
/// The <c>norbix-env</c> header — sent from the configured environment, omitted
/// for the default PROD, and overridable via <see cref="NorbixClient.WithEnv"/>.
/// </summary>
[TestFixture]
public sealed class EnvironmentHeaderTests
{
    [Test]
    public async Task Prod_default_omits_env_header()
    {
        using var fixture = NorbixTestFixture.Create();
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("norbix-env"), Is.False);
    }

    [Test]
    public async Task Configured_env_sets_header()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Env = "TEST");
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers["norbix-env"], Is.EqualTo("TEST"));
    }

    [Test]
    public async Task WithEnv_overrides_configured_env()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Env = "TEST");
        var scoped = fixture.Client.WithEnv("STAGING");
        await scoped.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers["norbix-env"], Is.EqualTo("STAGING"));
    }

    [Test]
    public async Task WithEnv_PROD_clears_header()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Env = "TEST");
        var scoped = fixture.Client.WithEnv("PROD");
        await scoped.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("norbix-env"), Is.False);
    }

    [Test]
    public void Env_env_var_is_read_by_ApplyEnvironment()
    {
        var opts = new NorbixClientOptions { ProjectId = "p" }
            .ApplyEnvironment(new Dictionary<string, string?> { ["NORBIX_ENV"] = "STAGING" });
        Assert.That(opts.Env, Is.EqualTo("STAGING"));
    }
}
