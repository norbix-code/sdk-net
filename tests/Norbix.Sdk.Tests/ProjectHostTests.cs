using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Transport;
using NUnit.Framework;

namespace Norbix.Sdk.Tests;

/// <summary>
/// The project's own API host: wherever the API address serves one (the
/// default or a written-out norbix.ai address, a <c>{projectHostLabel}</c>
/// template, a pasted project host) every API call goes to the project's host,
/// built from the project id with no call. Hub calls, the social sign-in link,
/// a project id that is not a Norbix id and any other address keep the shared
/// host. Design and test vectors: devops docs/tasks/clients-project-host.md
/// (rounds 1 and 2).
/// </summary>
[TestFixture]
public sealed class ProjectHostTests
{
    private const string Virginia = "pr_5XvmrTA6JVggZdmEE5bGdm";
    private const string VirginiaHost = "https://pr-022c4aca9f9e4df787ab0a20c83c44b6.api.norbix.ai";
    private const string Germany = "pr_3vlTD52e9VK8lXzdv0SmWG";
    private const string GermanyHost = "https://pr-184336d00e2141c3be5138e8dcf52e81.api.norbix.ai";

    private static string Host(NorbixTestFixture fixture) =>
        fixture.LastRequestUri!.GetLeftPart(UriPartial.Authority);

    private static Task<Norbix.Sdk.Types.EmptyResponse?> HubCall(NorbixApiClient client) =>
        // The suite targets the Norbix.Api package, so Hub modules are not
        // compiled in — drive the (internal) transport for the Hub target.
        client.Transport.SendAsync<Norbix.Sdk.Types.EmptyResponse>(
            new NorbixRequestSpec
            {
                Target = NorbixTarget.Hub,
                Path = "/{version}/account/regions",
                Method = "GET",
                Request = null,
                PathParams = Array.Empty<string>(),
                Scope = NorbixScope.Project,
            });

    // ── The label ───────────────────────────────────────────────────────────

    // The first three labels are the server's own values (project DTO
    // `hostLabel`, a project's DNS record, a project's API host), not this
    // decoder's.
    [TestCase("pr_5XvmrTA6JVggZdmEE5bGdm", "pr-022c4aca9f9e4df787ab0a20c83c44b6")]
    [TestCase("pr_3vlTD52e9VK8lXzdv0SmWG", "pr-184336d00e2141c3be5138e8dcf52e81")]
    [TestCase("pr_89W9MJvt0StwxsEvyohlJ", "pr-60d101b1938747659947d507192a5204")]
    [TestCase("pr_xCeyU0VjHvjVi5MpOYSGY", "pr-6b8b6182960c416098d153d3eb0e601f")]
    public void Label_of_a_project_id(string projectId, string label)
    {
        Assert.That(NorbixProjectHost.Label(projectId), Is.EqualTo(label));
    }

    [TestCase("p1")]
    [TestCase("proj_123")]
    [TestCase("pr_")]
    [TestCase("pr_!!!")]
    [TestCase("pr_zzzzzzzzzzzzzzzzzzzzzzz")] // 23 characters
    [TestCase("pr_zzzzzzzzzzzzzzzzzzzzzz")] // 22 characters, 2^128 or more
    [TestCase("acc_oOAAIfAzbauZ6f0NHEHjP")]
    [TestCase("PR_5XvmrTA6JVggZdmEE5bGdm")]
    [TestCase("pr_1")] // a number, but not a version-4 GUID
    [TestCase("")]
    [TestCase(null)]
    public void Not_a_project_id_gives_no_label_and_does_not_throw(string? value)
    {
        Assert.That(NorbixProjectHost.Label(value), Is.Null);
    }

    [Test]
    public void Label_matches_the_Guid_of_the_id()
    {
        // Round trip against .NET's own Guid: bytes → base62 id → label.
        var guid = Guid.Parse("022c4aca-9f9e-4df7-87ab-0a20c83c44b6");
        var number = new System.Numerics.BigInteger(guid.ToByteArray(), isUnsigned: true);
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var id = "";
        while (number > 0)
        {
            id = alphabet[(int)(number % 62)] + id;
            number /= 62;
        }

        Assert.That("pr_" + id, Is.EqualTo(Virginia));
        Assert.That(NorbixProjectHost.Label("pr_" + id), Is.EqualTo("pr-" + guid.ToString("N")));
    }

    // ── Default base URL ────────────────────────────────────────────────────

    [Test]
    public async Task Api_calls_go_to_the_project_host_and_still_send_the_project_header()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ProjectId = Virginia);
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo(VirginiaHost));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Virginia));
        Assert.That(fixture.LastRequest!.Headers.ContainsKey("nb-region"), Is.False);
    }

    [Test]
    public async Task Hub_calls_stay_on_the_shared_host()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ProjectId = Virginia);
        await HubCall(fixture.Client);

        Assert.That(Host(fixture), Is.EqualTo("https://hub.norbix.ai"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Virginia));
    }

    [Test]
    public async Task Project_id_from_the_environment_gives_the_project_host_too()
    {
        var options = new NorbixClientOptions { ApiKey = "k" }
            .ApplyEnvironment(new Dictionary<string, string?> { ["NORBIX_PROJECT_ID"] = Germany });
        using var fixture = NorbixTestFixture.Create(o => o.ProjectId = options.ProjectId);
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo(GermanyHost));
    }

    [Test]
    public async Task Login_goes_to_the_project_host()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Virginia;
            o.ApiKey = null;
        });
        fixture.Respond("/auth", new { bearerToken = "t" });

        await fixture.Client.LoginAsync(new() { UserName = "alice", Password = "x" });

        Assert.That(fixture.LastRequestUri!.AbsoluteUri, Is.EqualTo(VirginiaHost + "/auth"));
    }

    [Test]
    public async Task A_default_api_url_with_a_trailing_slash_is_still_the_default()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Virginia;
            o.ApiBaseUrl = NorbixClientOptions.DefaultApiBaseUrl + "/";
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo(VirginiaHost));
    }

    [Test]
    public async Task A_project_id_that_is_not_a_Norbix_id_keeps_the_shared_host()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ProjectId = "p1");
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo("https://api.norbix.ai"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo("p1"));
    }

    [Test]
    public async Task WithScope_moves_the_calls_to_the_new_project_host_and_header_agree()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ProjectId = Virginia);

        var germany = fixture.Client.WithScope(Germany);
        await germany.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo(GermanyHost));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Germany));

        var test = germany.WithScope("p1");
        await test.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo("https://api.norbix.ai"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo("p1"));

        // The client it was cloned from still talks to its own project.
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo(VirginiaHost));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Virginia));
    }

    [Test]
    public async Task The_other_clones_keep_the_project_host()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ProjectId = Virginia);

        await fixture.Client.WithEnv("TEST").Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo(VirginiaHost));

        await fixture.Client.WithBearerToken("a.b.c").Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo(VirginiaHost));

        await fixture.Client.WithLanguage("lt").Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo(VirginiaHost));
    }

    [Test]
    public void The_social_sign_in_link_stays_on_the_shared_host()
    {
        // The gateway's callback comes back to the shared host and its nonce
        // cookie is host-only.
        using var fixture = NorbixTestFixture.Create(o => o.ProjectId = Virginia);

        Assert.That(
            fixture.Client.SocialSignInUrl("google"),
            Is.EqualTo("https://api.norbix.ai/v2/membership/auth/social/google?projectId=" + Virginia));
    }

    // ── With a region ───────────────────────────────────────────────────────

    [Test]
    public async Task Region_api_on_the_project_host_with_no_region_label_hub_regional_header_sent()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Germany;
            o.Region = "nb-eu-germany";
        });

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo(GermanyHost));
        Assert.That(fixture.LastRequest!.Headers["nb-region"], Is.EqualTo("nb-eu-germany"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Germany));

        await HubCall(fixture.Client);
        Assert.That(Host(fixture), Is.EqualTo("https://nb-eu-germany.hub.norbix.ai"));
        Assert.That(fixture.LastRequest!.Headers["nb-region"], Is.EqualTo("nb-eu-germany"));
    }

    [Test]
    public async Task WithRegion_changes_the_hub_host_only()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ProjectId = Germany);

        var eu = fixture.Client.WithRegion("nb-eu-germany");
        await eu.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo(GermanyHost));
        Assert.That(fixture.LastRequest!.Headers["nb-region"], Is.EqualTo("nb-eu-germany"));
        await HubCall(eu);
        Assert.That(Host(fixture), Is.EqualTo("https://nb-eu-germany.hub.norbix.ai"));

        var unpinned = eu.WithRegion(null);
        await unpinned.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo(GermanyHost));
        await HubCall(unpinned);
        Assert.That(Host(fixture), Is.EqualTo("https://hub.norbix.ai"));
    }

    [Test]
    public async Task Region_with_a_project_id_that_is_not_a_Norbix_id_keeps_the_regional_api_host()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = "p1";
            o.Region = "nb-eu-germany";
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo("https://nb-eu-germany.api.norbix.ai"));
    }

    [Test]
    public void The_social_sign_in_link_uses_the_regional_shared_host()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Germany;
            o.Region = "nb-eu-germany";
        });

        Assert.That(
            fixture.Client.SocialSignInUrl("google"),
            Does.StartWith("https://nb-eu-germany.api.norbix.ai/v2/membership/auth/social/google?"));
    }

    // ── Another address is used as given ─────────────────────────────────────

    [Test]
    public async Task Another_address_is_used_as_given()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Virginia;
            o.Region = "nb-eu-germany";
            o.ApiBaseUrl = "https://api.norbix.io";
            o.HubBaseUrl = "https://hub.norbix.io";
        });

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo("https://api.norbix.io"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Virginia));
        Assert.That(fixture.LastRequest!.Headers["nb-region"], Is.EqualTo("nb-eu-germany"));

        await HubCall(fixture.Client);
        Assert.That(Host(fixture), Is.EqualTo("https://hub.norbix.io"));

        var germany = fixture.Client.WithScope(Germany);
        await germany.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo("https://api.norbix.io"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Germany));

        Assert.That(
            fixture.Client.SocialSignInUrl("google"),
            Does.StartWith("https://api.norbix.io/v2/membership/auth/social/google?"));
    }

    [TestCase("https://api.customer.com")]
    [TestCase("https://api.norbix.io")]
    [TestCase("http://localhost:5002")]
    [TestCase("http://api.norbix.ai")]
    public async Task A_plain_own_domain_staging_localhost_and_http_are_used_as_given(string api)
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Virginia;
            o.ApiBaseUrl = api;
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(fixture.LastRequestUri!.AbsoluteUri, Does.StartWith(api + "/"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Virginia));
    }

    [Test]
    public async Task NORBIX_API_URL_with_another_address_is_used_as_given()
    {
        var options = new NorbixClientOptions { ApiKey = "k" }
            .ApplyEnvironment(new Dictionary<string, string?>
            {
                ["NORBIX_PROJECT_ID"] = Virginia,
                ["NORBIX_API_URL"] = "https://api.norbix.io",
            });
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = options.ProjectId;
            o.ApiBaseUrl = options.ApiBaseUrl;
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo("https://api.norbix.io"));
    }

    [Test]
    public async Task A_custom_api_url_with_the_default_hub_url_only_the_hub_follows_the_region()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Virginia;
            o.Region = "nb-eu-germany";
            o.ApiBaseUrl = "http://localhost:5002";
        });

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo("http://localhost:5002"));
        await HubCall(fixture.Client);
        Assert.That(Host(fixture), Is.EqualTo("https://nb-eu-germany.hub.norbix.ai"));
    }

    // ── A norbix.ai address written out (round 2) ───────────────────────────

    [Test]
    public async Task NORBIX_API_URL_with_the_default_address_written_out_goes_to_the_project_host()
    {
        var options = new NorbixClientOptions { ApiKey = "k" }
            .ApplyEnvironment(new Dictionary<string, string?>
            {
                ["NORBIX_PROJECT_ID"] = Virginia,
                ["NORBIX_API_URL"] = "https://api.norbix.ai",
            });
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = options.ProjectId;
            o.ApiBaseUrl = options.ApiBaseUrl;
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo(VirginiaHost));
    }

    [Test]
    public async Task A_regional_norbix_ai_address_written_out_goes_to_the_project_host()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Germany;
            o.ApiBaseUrl = "https://nb-eu-germany.api.norbix.ai";
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo(GermanyHost));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Germany));
        Assert.That(
            fixture.Client.SocialSignInUrl("google"),
            Does.StartWith("https://nb-eu-germany.api.norbix.ai/v2/membership/auth/social/google?"));
    }

    [Test]
    public async Task A_norbix_ai_address_with_a_version_path_keeps_the_path()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Virginia;
            o.ApiBaseUrl = "https://api.norbix.ai/v3";
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(fixture.LastRequestUri!.AbsoluteUri, Does.StartWith(VirginiaHost + "/v3/"));
    }

    // ── An install on its own domain (round 2) ──────────────────────────────

    private const string Template = "https://{projectHostLabel}.api.customer.com";

    [Test]
    public async Task A_template_is_filled_and_switching_project_moves_host_and_header_together()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Virginia;
            o.ApiBaseUrl = Template;
            o.HubBaseUrl = "https://hub.customer.com";
        });

        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo("https://pr-022c4aca9f9e4df787ab0a20c83c44b6.api.customer.com"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Virginia));

        var germany = fixture.Client.WithScope(Germany);
        await germany.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
        Assert.That(Host(fixture), Is.EqualTo("https://pr-184336d00e2141c3be5138e8dcf52e81.api.customer.com"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Germany));

        await HubCall(germany);
        Assert.That(Host(fixture), Is.EqualTo("https://hub.customer.com"));

        Assert.That(
            germany.SocialSignInUrl("google"),
            Does.StartWith("https://api.customer.com/v2/membership/auth/social/google?"));
    }

    [Test]
    public async Task A_template_from_NORBIX_API_URL_with_a_port()
    {
        var options = new NorbixClientOptions { ApiKey = "k" }
            .ApplyEnvironment(new Dictionary<string, string?>
            {
                ["NORBIX_PROJECT_ID"] = Virginia,
                ["NORBIX_API_URL"] = "http://{projectHostLabel}.api.local.norbix.test:8443",
            });
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = options.ProjectId;
            o.ApiBaseUrl = options.ApiBaseUrl;
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(
            fixture.LastRequestUri!.AbsoluteUri,
            Does.StartWith("http://pr-022c4aca9f9e4df787ab0a20c83c44b6.api.local.norbix.test:8443/"));
    }

    [Test]
    public async Task A_template_with_an_id_that_is_not_a_Norbix_id_drops_the_label()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = "p1";
            o.ApiBaseUrl = Template;
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo("https://api.customer.com"));
    }

    [Test]
    public async Task A_pasted_project_host_follows_the_clients_project()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ProjectId = Germany;
            o.ApiBaseUrl = "https://pr-022c4aca9f9e4df787ab0a20c83c44b6.api.customer.com";
        });
        await fixture.Client.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());

        Assert.That(Host(fixture), Is.EqualTo("https://pr-184336d00e2141c3be5138e8dcf52e81.api.customer.com"));
        Assert.That(fixture.LastRequest!.Headers["norbix-project-id"], Is.EqualTo(Germany));
        Assert.That(
            fixture.Client.SocialSignInUrl("google"),
            Does.StartWith("https://api.customer.com/v2/membership/auth/social/google?"));
    }

    // ── The helper ──────────────────────────────────────────────────────────

    [TestCase("https://api.norbix.ai", Virginia, VirginiaHost, "https://api.norbix.ai")]
    [TestCase("https://api.norbix.ai/", Virginia, VirginiaHost + "/", "https://api.norbix.ai/")]
    [TestCase("https://nb-eu-germany.api.norbix.ai/v3", Germany, GermanyHost + "/v3", "https://nb-eu-germany.api.norbix.ai/v3")]
    [TestCase("https://API.Norbix.AI", Virginia, VirginiaHost, "https://API.Norbix.AI")]
    [TestCase("https://api.norbix.ai:8443", Virginia, "https://pr-022c4aca9f9e4df787ab0a20c83c44b6.api.norbix.ai:8443", "https://api.norbix.ai:8443")]
    [TestCase("https://api.norbix.ai", "p1", "https://api.norbix.ai", "https://api.norbix.ai")]
    [TestCase("https://a.b.api.norbix.ai", Virginia, "https://a.b.api.norbix.ai", "https://a.b.api.norbix.ai")]
    [TestCase("https://{projectHostLabel}.api.customer.com/v3", Virginia, "https://pr-022c4aca9f9e4df787ab0a20c83c44b6.api.customer.com/v3", "https://api.customer.com/v3")]
    [TestCase("https://{projectHostLabel}.api.customer.com", "p1", "https://api.customer.com", "https://api.customer.com")]
    [TestCase("https://PR-022C4ACA9F9E4DF787AB0A20C83C44B6.api.customer.com", Germany, "https://pr-184336d00e2141c3be5138e8dcf52e81.api.customer.com", "https://api.customer.com")]
    [TestCase("https://pr-022c4aca9f9e4df787ab0a20c83c44b6.api.norbix.ai", "p1", "https://api.norbix.ai", "https://api.norbix.ai")]
    [TestCase("https://pr-123.api.customer.com", Virginia, "https://pr-123.api.customer.com", "https://pr-123.api.customer.com")]
    [TestCase("not a url", Virginia, "not a url", "not a url")]
    [TestCase("", Virginia, "", "")]
    public void ApiBaseUrl_follows_the_rule(string baseUrl, string projectId, string project, string shared)
    {
        Assert.That(NorbixProjectHost.ApiBaseUrl(baseUrl, projectId), Is.EqualTo((project, shared)));
    }

    [Test]
    public void The_placeholder_is_the_one_in_the_install_template()
    {
        Assert.That(NorbixProjectHost.LabelPlaceholder, Is.EqualTo("{projectHostLabel}"));
    }
}
