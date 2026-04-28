using System.Net.Http;

using Microsoft.Extensions.Logging;

using Norbix.Sdk.Auth;
using Norbix.Sdk.Transport;

namespace Norbix.Sdk;

/// <summary>
/// Single entry point for the Norbix SDK.
/// </summary>
/// <example>
/// <code>
/// // Zero-arg — reads NORBIX_* env vars
/// using var client = new NorbixClient();
///
/// // Service mode — long-lived API key
/// using var client = new NorbixClient(new NorbixClientOptions
/// {
///     ApiKey = "sk_live_...",
///     ProjectId = "proj_123",
/// });
///
/// // Use it
/// var orders = await client.Database.FindAsync(new() { CollectionName = "orders" });
///
/// // User mode — exchange credentials for a JWT
/// using var u = new NorbixClient(new NorbixClientOptions { ProjectId = "proj_123" });
/// await u.LoginAsync(new() { UserName = "alice", Password = "secret" });
/// </code>
/// </example>
public sealed partial class NorbixClient : IDisposable, IAsyncDisposable
{
    private readonly NorbixClientOptions _options;
    private readonly HttpTransport _transport;
    private readonly bool _ownsTransport;

    /// <summary>Build a client from <c>NORBIX_*</c> environment variables alone.</summary>
    public NorbixClient() : this(new NorbixClientOptions())
    {
    }

    /// <summary>Build a client from explicit options. Env vars fill in any unset field.</summary>
    public NorbixClient(NorbixClientOptions options)
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
    internal NorbixClient(NorbixClientOptions options, HttpMessageHandler handler)
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
    internal NorbixClient(NorbixClientOptions options, HttpClient httpClient, ILogger? logger = null)
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
    public NorbixClient WithBearerToken(string? token)
    {
        var o = _options.Clone();
        o.BearerToken = token;
        return new NorbixClient(o, _transport.HttpClient, _transport.Logger);
    }

    /// <summary>Create a new client with a different API key.</summary>
    public NorbixClient WithApiKey(string? apiKey)
    {
        var o = _options.Clone();
        o.ApiKey = apiKey;
        return new NorbixClient(o, _transport.HttpClient, _transport.Logger);
    }

    /// <summary>Create a new client with a different project/account scope.</summary>
    public NorbixClient WithScope(string projectId, string? accountId = null)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            throw new ArgumentException("projectId is required.", nameof(projectId));
        }
        var o = _options.Clone();
        o.ProjectId = projectId;
        o.AccountId = accountId;
        return new NorbixClient(o, _transport.HttpClient, _transport.Logger);
    }

    /// <summary>Create a new client without a JWT bearer token (falls back to ApiKey if configured).</summary>
    public NorbixClient WithoutBearerToken() => WithBearerToken(null);

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
