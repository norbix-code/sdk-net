using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;

namespace Norbix.Sdk.Tests;

/// <summary>
/// The <c>Accept-Language</c> header — the ONLY language input the gateway
/// reads (the old <c>cultureCode</c> field and <c>CultureCode</c> /
/// <c>norbix-language</c> headers are gone). Injected ONLY when a language is
/// resolved (per-call <see cref="NorbixApiClient.WithLanguage"/> override →
/// options <c>Language</c> → <c>NORBIX_LANGUAGE</c> → unset; there is no
/// default language — the gateway then uses the project default).
/// </summary>
[TestFixture]
public sealed class LanguageHeaderTests
{
    [Test]
    public async Task Unset_language_omits_header()
    {
        using var fixture = NorbixTestFixture.Create();
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("Accept-Language"), Is.False);
    }

    [Test]
    public async Task Configured_language_sets_header()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Language = "lt");
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers["Accept-Language"], Is.EqualTo("lt"));
    }

    [Test]
    public async Task Language_with_q_values_is_passed_through()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Language = "lt-LT, en;q=0.8");
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        // HttpHeaders re-formats a parsed q-value as "en; q=0.8" — compare
        // without whitespace; the tags and weights must be unchanged.
        Assert.That(
            fixture.LastRequest!.Headers["Accept-Language"].Replace(" ", ""),
            Is.EqualTo("lt-LT,en;q=0.8"));
    }

    [Test]
    public async Task WithLanguage_overrides_configured_language()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Language = "lt");
        var scoped = fixture.Client.WithLanguage("de");
        await scoped.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers["Accept-Language"], Is.EqualTo("de"));
    }

    [Test]
    public async Task WithLanguage_null_clears_header()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Language = "lt");
        var scoped = fixture.Client.WithLanguage(null);
        await scoped.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("Accept-Language"), Is.False);
    }

    [Test]
    public async Task WithLanguage_does_not_change_the_original_client()
    {
        using var fixture = NorbixTestFixture.Create();
        _ = fixture.Client.WithLanguage("lt");
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("Accept-Language"), Is.False);
    }

    [Test]
    public void Language_env_var_is_read_by_ApplyEnvironment()
    {
        var opts = new NorbixClientOptions { ProjectId = "p" }
            .ApplyEnvironment(new Dictionary<string, string?> { ["NORBIX_LANGUAGE"] = "lt" });
        Assert.That(opts.Language, Is.EqualTo("lt"));
    }

    [Test]
    public void Explicit_language_wins_over_env_var()
    {
        var opts = new NorbixClientOptions { ProjectId = "p", Language = "de" }
            .ApplyEnvironment(new Dictionary<string, string?> { ["NORBIX_LANGUAGE"] = "lt" });
        Assert.That(opts.Language, Is.EqualTo("de"));
    }

    [Test]
    public void Clone_copies_language()
    {
        var opts = new NorbixClientOptions { ProjectId = "p", Language = "lt" };
        Assert.That(opts.Clone().Language, Is.EqualTo("lt"));
    }

    [Test]
    public async Task Legacy_language_headers_are_never_sent()
    {
        using var fixture = NorbixTestFixture.Create(o => o.Language = "lt");
        var scoped = fixture.Client.WithLanguage("lt");
        await scoped.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("CultureCode"), Is.False);
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("norbix-language"), Is.False);
    }
}
