using System.Net.Http;

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
/// var orders = await client.Api.Database.FindAsync(new() { CollectionName = "orders" });
///
/// // User mode — exchange credentials for a JWT
/// using var u = new NorbixClient(new NorbixClientOptions { ProjectId = "proj_123" });
/// await u.LoginAsync(new() { UserName = "alice", Password = "secret" });
/// </code>
/// </example>
public sealed partial class NorbixClient : IDisposable
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
        _options = options.ApplyEnvironment();
        _options.Validate();

        _transport = new HttpTransport(_options);
        _ownsTransport = true;

        InitializeNamespaces(); // populated by source generator
    }

    /// <summary>
    /// Test-only constructor. <see cref="HttpMessageHandler"/> is hidden from
    /// the public surface; tests reach this via <c>InternalsVisibleTo</c>.
    /// </summary>
    internal NorbixClient(NorbixClientOptions options, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);
        _options = options.ApplyEnvironment();
        _options.Validate();

        var http = new HttpClient(handler) { Timeout = _options.Timeout };
        _transport = new HttpTransport(_options, http);
        _ownsTransport = true;

        InitializeNamespaces();
    }

    /// <summary>True when the client has either an API key or a bearer token.</summary>
    public bool IsAuthenticated =>
        !string.IsNullOrEmpty(_options.BearerToken) || !string.IsNullOrEmpty(_options.ApiKey);

    /// <summary>Read-only snapshot of the current options. Useful for diagnostics.</summary>
    public NorbixClientOptions Options => _options;

    /// <summary>Internal access for the source-generated module classes — no public API leak.</summary>
    internal INorbixTransport Transport => _transport;

    /// <summary>Replace the bearer token without rebuilding the client.</summary>
    public void SetBearerToken(string? token) => _options.BearerToken = token;

    /// <summary>Replace the API key without rebuilding the client.</summary>
    public void SetApiKey(string? apiKey) => _options.ApiKey = apiKey;

    /// <summary>Switch project (and optionally account) scope at runtime.</summary>
    public void SetScope(string projectId, string? accountId = null)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            throw new ArgumentException("projectId is required.", nameof(projectId));
        }
        _options.ProjectId = projectId;
        _options.AccountId = accountId;
    }

    /// <summary>
    /// Exchange credentials for a JWT bearer token. On success, the token is
    /// stored on the client and used for every subsequent call (it takes
    /// precedence over a configured API key).
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

        if (response is { BearerToken: { Length: > 0 } token })
        {
            _options.BearerToken = token;
        }

        return response ?? new LoginResponse();
    }

    /// <summary>Clear the JWT bearer token. Falls back to the API key when one is configured.</summary>
    public void Logout() => _options.BearerToken = null;

    /// <summary>Source-generated partial — wires the Api / Hub namespace classes onto the client.</summary>
    private partial void InitializeNamespaces();

    public void Dispose()
    {
        if (_ownsTransport && _transport is IDisposable d) d.Dispose();
    }
}
