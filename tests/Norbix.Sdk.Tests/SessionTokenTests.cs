using System.Text.Json;

using Norbix.Sdk.Auth;
using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Sessions (gateway D18): a renewed user JWT comes back in the
/// <c>norbix-session-token</c> answer header. The client swaps its own bearer
/// token for it and tells the app through <c>OnSessionToken</c> — only when
/// it sent that bearer token on the call. Sign-in always sends
/// <c>rememberMe</c>.
/// </summary>
[TestFixture]
public sealed class SessionTokenTests
{
    private static readonly IReadOnlyDictionary<string, string> FreshToken =
        new Dictionary<string, string> { ["norbix-session-token"] = "fresh-jwt" };

    [Test]
    public async Task A_renewed_token_replaces_the_bearer_token_and_calls_back()
    {
        var seen = new List<string>();
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = null;
            o.BearerToken = "old-jwt";
            o.OnSessionToken = seen.Add;
        });
        fixture.Respond("/echo", new { }, headers: FreshToken);

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(seen, Has.Count.EqualTo(1));
        Assert.That(seen[0], Is.EqualTo("fresh-jwt"));
        Assert.That(fixture.Client.Options.BearerToken, Is.EqualTo("fresh-jwt"));
        Assert.That(fixture.RecordedRequests[0].Headers["Authorization"], Is.EqualTo("Bearer old-jwt"));
        Assert.That(fixture.RecordedRequests[1].Headers["Authorization"], Is.EqualTo("Bearer fresh-jwt"));
    }

    [Test]
    public async Task A_call_that_needs_a_token_swaps_too()
    {
        var seen = new List<string>();
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = null;
            o.BearerToken = "old-jwt";
            o.OnSessionToken = seen.Add;
        });
        fixture.Respond("/count", new { count = 1 }, headers: FreshToken);

        await fixture.Client.Database.CountAsync(new Norbix.Sdk.Types.Api.CountRequest { CollectionName = "c" });
        await fixture.Client.Database.CountAsync(new Norbix.Sdk.Types.Api.CountRequest { CollectionName = "c" });

        Assert.That(seen, Has.Count.EqualTo(1));
        Assert.That(fixture.Client.Options.BearerToken, Is.EqualTo("fresh-jwt"));
        Assert.That(fixture.LastRequest!.Headers["Authorization"], Is.EqualTo("Bearer fresh-jwt"));
    }

    [Test]
    public async Task The_swap_stays_on_the_client_that_made_the_call()
    {
        // A With… clone has its own options: the parent keeps its token, and
        // the app learns the new one from OnSessionToken.
        var seen = new List<string>();
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = null;
            o.BearerToken = "old-jwt";
            o.OnSessionToken = seen.Add;
        });
        fixture.Respond("/echo", new { }, headers: FreshToken);

        using var test = fixture.Client.WithEnv("TEST");
        await test.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(seen, Has.Count.EqualTo(1));
        Assert.That(test.Options.BearerToken, Is.EqualTo("fresh-jwt"));
        Assert.That(fixture.Client.Options.BearerToken, Is.EqualTo("old-jwt"));
    }

    [Test]
    public async Task No_header_leaves_the_token_as_it_is()
    {
        var seen = new List<string>();
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.BearerToken = "old-jwt";
            o.OnSessionToken = seen.Add;
        });
        fixture.Respond("/echo", new { });

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(seen, Is.Empty);
        Assert.That(fixture.Client.Options.BearerToken, Is.EqualTo("old-jwt"));
    }

    [Test]
    public async Task An_empty_header_is_ignored()
    {
        var seen = new List<string>();
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.BearerToken = "old-jwt";
            o.OnSessionToken = seen.Add;
        });
        fixture.Respond("/echo", new { }, headers: new Dictionary<string, string> { ["norbix-session-token"] = "" });

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(seen, Is.Empty);
        Assert.That(fixture.Client.Options.BearerToken, Is.EqualTo("old-jwt"));
    }

    [Test]
    public async Task An_api_key_client_never_swaps()
    {
        var seen = new List<string>();
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = "service-key";
            o.BearerToken = null;
            o.OnSessionToken = seen.Add;
        });
        fixture.Respond("/echo", new { }, headers: FreshToken);

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(seen, Is.Empty);
        Assert.That(fixture.Client.Options.BearerToken, Is.Null);
        Assert.That(fixture.LastRequest!.Headers["Authorization"], Is.EqualTo("Bearer service-key"));
    }

    [Test]
    public async Task A_call_sent_without_the_bearer_token_never_swaps()
    {
        // Sign-in goes out with no Authorization header, so its answer must
        // not replace the token the client holds.
        var seen = new List<string>();
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.BearerToken = "old-jwt";
            o.OnSessionToken = seen.Add;
        });
        fixture.Respond("/auth", new { bearerToken = "login-jwt" }, headers: FreshToken);

        await fixture.Client.LoginAsync(new LoginCredentials { UserName = "alice", Password = "secret" });

        Assert.That(seen, Is.Empty);
        Assert.That(fixture.Client.Options.BearerToken, Is.EqualTo("old-jwt"));
    }

    [Test]
    public async Task A_throwing_callback_does_not_fail_the_call()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.BearerToken = "old-jwt";
            o.OnSessionToken = _ => throw new InvalidOperationException("store is down");
        });
        fixture.Respond("/echo", new { }, headers: FreshToken);

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(fixture.Client.Options.BearerToken, Is.EqualTo("fresh-jwt"));
    }

    [Test]
    public void WithBearerToken_keeps_the_callback()
    {
        Action<string> callback = _ => { };
        using var fixture = NorbixTestFixture.Create(o => o.OnSessionToken = callback);

        using var asUser = fixture.Client.WithBearerToken("user-jwt");

        Assert.That(asUser.Options.OnSessionToken, Is.SameAs(callback));
    }

    [TestCase(null, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    public async Task LoginAsync_always_sends_rememberMe(bool? rememberMe, bool sent)
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/auth", new { bearerToken = "jwt" });

        await fixture.Client.LoginAsync(new LoginCredentials
        {
            UserName = "alice",
            Password = "secret",
            RememberMe = rememberMe,
        });

        var body = JsonSerializer.SerializeToElement(fixture.LastRequest!.Body);
        Assert.That(body.GetProperty("rememberMe").GetBoolean(), Is.EqualTo(sent));
    }
}
