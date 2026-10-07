using System.Net.Http;

using Microsoft.Extensions.Logging;

using Norbix.Sdk.Auth;
using Norbix.Sdk.Transport;

// Norbix.Api and Norbix.Hub both compile this file. Each gives the client its
// own name, so one project can reference both packages: Norbix.Hub builds with
// NORBIX_HUB defined (see Norbix.Hub.csproj).
#if NORBIX_HUB
using Client = Norbix.Sdk.NorbixHubClient;
#else
using Client = Norbix.Sdk.NorbixApiClient;
#endif

namespace Norbix.Sdk;

/// <summary>
/// Single entry point for the Norbix SDK: <c>NorbixApiClient</c> in the
/// Norbix.Api package, <c>NorbixHubClient</c> in the Norbix.Hub package.
/// </summary>
/// <example>
/// <code>
/// // Zero-arg — reads NORBIX_* env vars
/// using var client = new NorbixApiClient();
///
/// // Service mode — long-lived API key
/// using var client = new NorbixApiClient(new NorbixClientOptions
/// {
///     ApiKey = "sk_live_...",
///     ProjectId = "proj_123",
/// });
///
/// // Use it
/// var orders = await client.Database.FindAsync(new() { CollectionName = "orders" });
///
/// // User mode — exchange credentials for a JWT
/// using var u = new NorbixApiClient(new NorbixClientOptions { ProjectId = "proj_123" });
/// await u.LoginAsync(new() { UserName = "alice", Password = "secret" });
/// </code>
/// </example>
#if NORBIX_HUB
public sealed partial class NorbixHubClient : IDisposable, IAsyncDisposable
#else
public sealed partial class NorbixApiClient : IDisposable, IAsyncDisposable
#endif
{
    private readonly NorbixClientOptions _options;
    private readonly HttpTransport _transport;
    private readonly bool _ownsTransport;

    /// <summary>Build a client from <c>NORBIX_*</c> environment variables alone.</summary>
#if NORBIX_HUB
    public NorbixHubClient() : this(new NorbixClientOptions())
#else
    public NorbixApiClient() : this(new NorbixClientOptions())
#endif
    {
    }

    /// <summary>Build a client from explicit options. Env vars fill in any unset field.</summary>
#if NORBIX_HUB
    public NorbixHubClient(NorbixClientOptions options)
#else
    public NorbixApiClient(NorbixClientOptions options)
#endif
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Clone().ApplyEnvironment();
        _options.Validate();

        _transport = new HttpTransport(_options);
        _ownsTransport = true;

        InitializeModules(); // populated by source generator
    }

    /// <summary>
    /// Test-only constructor. <see cref="HttpMessageHandler"/> is hidden from
    /// the public surface; tests reach this via <c>InternalsVisibleTo</c>.
    /// </summary>
#if NORBIX_HUB
    internal NorbixHubClient(NorbixClientOptions options, HttpMessageHandler handler)
#else
    internal NorbixApiClient(NorbixClientOptions options, HttpMessageHandler handler)
#endif
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);
        _options = options.Clone().ApplyEnvironment();
        _options.Validate();

        var http = new HttpClient(handler) { Timeout = _options.Timeout };
        _transport = new HttpTransport(_options, http);
        _ownsTransport = true;

        InitializeModules();
    }

    /// <summary>
    /// Internal constructor used by DI to supply an <see cref="HttpClient"/>
    /// from <c>IHttpClientFactory</c> without exposing it on the public API.
    /// </summary>
#if NORBIX_HUB
    internal NorbixHubClient(NorbixClientOptions options, HttpClient httpClient, ILogger? logger = null)
#else
    internal NorbixApiClient(NorbixClientOptions options, HttpClient httpClient, ILogger? logger = null)
#endif
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        _options = options.Clone().ApplyEnvironment();
        _options.Validate();

        _transport = new HttpTransport(_options, httpClient, logger);
        _ownsTransport = true;

        InitializeModules();
    }

    /// <summary>True when the client has either an API key or a bearer token.</summary>
    public bool IsAuthenticated =>
        !string.IsNullOrEmpty(_options.BearerToken) || !string.IsNullOrEmpty(_options.ApiKey);

    /// <summary>Read-only snapshot of the current options. Useful for diagnostics.</summary>
    public NorbixClientOptions Options => _options;

    /// <summary>Internal access for the source-generated module classes — no public API leak.</summary>
    internal INorbixTransport Transport => _transport;

    /// <summary>Create a new client with a different bearer token.</summary>
    public Client WithBearerToken(string? token)
    {
        var o = _options.Clone();
        o.BearerToken = token;
        return new Client(o, _transport.HttpClient, _transport.Logger);
    }

    /// <summary>Create a new client with a different API key.</summary>
    public Client WithApiKey(string? apiKey)
    {
        var o = _options.Clone();
        o.ApiKey = apiKey;
        return new Client(o, _transport.HttpClient, _transport.Logger);
    }

    /// <summary>Create a new client with a different project/account scope.</summary>
    public Client WithScope(string projectId, string? accountId = null)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            throw new ArgumentException("projectId is required.", nameof(projectId));
        }
        var o = _options.Clone();
        o.ProjectId = projectId;
        o.AccountId = accountId;
        return new Client(o, _transport.HttpClient, _transport.Logger);
    }

    /// <summary>
    /// Create a new client scoped to a different project environment. The new
    /// client shares the underlying <see cref="HttpClient"/> and sends the
    /// <c>norbix-env</c> header on every request (omitted for <c>PROD</c>).
    /// Use this for per-call or per-scope environment overrides, e.g.
    /// <c>await client.WithEnv("TEST").Hub.Account.GetProjectEnvironmentsAsync()</c>.
    /// </summary>
    public Client WithEnv(string? env)
    {
        var o = _options.Clone();
        o.Env = string.IsNullOrEmpty(env) ? "PROD" : env;
        return new Client(o, _transport.HttpClient, _transport.Logger);
    }

    /// <summary>
    /// Create a new client scoped to a Norbix region. The new client shares
    /// the underlying <see cref="HttpClient"/> and sends the <c>nb-region</c>
    /// header on every request (omitted when <paramref name="region"/> is
    /// null/empty — there is no default region). When the base URL is still
    /// the SDK default, requests are composed against the regional endpoint
    /// (<c>https://{region}.api.norbix.ai</c>) per request; a custom base URL
    /// is never rewritten. Use this for per-call or per-scope region
    /// overrides, e.g.
    /// <c>await client.WithRegion("nb-eu-germany").Echo.EchoAsync(new())</c>.
    /// </summary>
    public Client WithRegion(string? region)
    {
        var o = _options.Clone();
        o.Region = string.IsNullOrEmpty(region) ? null : region;
        return new Client(o, _transport.HttpClient, _transport.Logger);
    }

    /// <summary>Create a new client without a JWT bearer token (falls back to ApiKey if configured).</summary>
    public Client WithoutBearerToken() => WithBearerToken(null);

    /// <summary>
    /// Exchange credentials for a JWT bearer token. On success, the token is
    /// returned to the caller. Use <see cref="WithBearerToken"/> to create an
    /// authenticated client for follow-up calls.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(
        LoginCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (string.IsNullOrEmpty(credentials.Provider)) credentials.Provider = "credentials";

        var response = await _transport.SendAsync<LoginResponse>(
            new NorbixRequestSpec
            {
                Target = NorbixTarget.Api,
                Path = "/auth",
                Method = "POST",
                Request = credentials,
                PathParams = Array.Empty<string>(),
                Scope = NorbixScope.Unauthenticated,
            },
            cancellationToken).ConfigureAwait(false);

        return response ?? new LoginResponse();
    }

    /// <summary>Source-generated partial — wires endpoint modules onto the client.</summary>
    partial void InitializeModules();

    public void Dispose()
    {
        if (_ownsTransport && _transport is IDisposable d) d.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
