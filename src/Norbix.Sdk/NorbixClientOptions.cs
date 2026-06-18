namespace Norbix.Sdk;

/// <summary>
/// Configuration for <see cref="NorbixClient"/>. Every field is optional in
/// code if the matching <c>NORBIX_*</c> environment variable is set;
/// <see cref="ProjectId"/> is the only one that must come from somewhere.
/// </summary>
/// <remarks>
/// Auth modes:
/// <list type="bullet">
///   <item><description><b>API key</b> — long-lived, server-to-server. Set <see cref="ApiKey"/> or <c>NORBIX_API_KEY</c>.</description></item>
///   <item><description><b>JWT bearer</b> — short-lived, on behalf of a user. Set <see cref="BearerToken"/>, <c>NORBIX_BEARER_TOKEN</c>, or call <see cref="NorbixClient.LoginAsync"/>.</description></item>
/// </list>
/// If both are set, the JWT bearer wins.
/// </remarks>
public sealed class NorbixClientOptions
{
    /// <summary>SDK-default API gateway base URL.</summary>
    public const string DefaultApiBaseUrl = "https://api.norbix.ai";

    /// <summary>SDK-default Hub gateway base URL.</summary>
    public const string DefaultHubBaseUrl = "https://hub.norbix.ai";

    /// <summary>Long-lived API key (or <c>NORBIX_API_KEY</c>).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Short-lived JWT bearer (or <c>NORBIX_BEARER_TOKEN</c> / <see cref="NorbixClient.LoginAsync"/>).</summary>
    public string? BearerToken { get; set; }

    /// <summary>Project the SDK operates against. Required (or <c>NORBIX_PROJECT_ID</c>).</summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Optional account ID. Required for Hub account-scoped endpoints
    /// (team invite, billing portal, account verify). <c>NORBIX_ACCOUNT_ID</c>.
    /// </summary>
    public string? AccountId { get; set; }

    /// <summary>
    /// Project environment every request targets, sent as the <c>norbix-env</c>
    /// header. Each project owns a set of named environments; <c>PROD</c> always
    /// exists and is the default. A non-PROD env (e.g. <c>TEST</c>, <c>STAGING</c>)
    /// scopes every read and write to that environment's integrations — there is
    /// no cross-env fallback. Default <c>PROD</c> (or <c>NORBIX_ENV</c>). Use
    /// <see cref="NorbixClient.WithEnv"/> for a per-call override.
    /// </summary>
    public string Env { get; set; } = "PROD";

    /// <summary>
    /// Norbix region code (e.g. <c>nb-eu-germany</c>) every request targets,
    /// sent as the <c>nb-region</c> header. Unlike <see cref="Env"/> there is
    /// NO default region — when unset, no header is sent. Resolution order:
    /// per-call <see cref="NorbixClient.WithRegion"/> override → this option
    /// → <c>NORBIX_REGION</c> environment variable → unset. When a region is
    /// resolved and the base URL is the SDK default, the transport composes
    /// the regional endpoint (<c>https://{region}.api.norbix.ai</c>) per
    /// request; a custom base URL is never rewritten.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>API gateway base URL. Default <c>https://api.norbix.ai</c> (or <c>NORBIX_API_URL</c>).</summary>
    public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;

    /// <summary>Hub gateway base URL. Default <c>https://hub.norbix.ai</c> (or <c>NORBIX_HUB_URL</c>).</summary>
    public string HubBaseUrl { get; set; } = DefaultHubBaseUrl;

    /// <summary>{version} segment for API routes. Default <c>v2</c>.</summary>
    public string ApiVersion { get; set; } = "v2";

    /// <summary>{version} segment for Hub routes. Default <c>v2</c>.</summary>
    public string HubVersion { get; set; } = "v2";

    /// <summary>Per-request timeout. Default 30s.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Extra headers added to every request.</summary>
    public IDictionary<string, string> DefaultHeaders { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public NorbixClientOptions() { }

    /// <summary>Convenience overload for the common case.</summary>
    public NorbixClientOptions(string apiKey, string projectId, string? accountId = null)
    {
        ApiKey = apiKey;
        ProjectId = projectId;
        AccountId = accountId;
    }

    /// <summary>
    /// Create a copy of these options. Used to prevent accidental shared mutable
    /// state when options are provided by DI containers.
    /// </summary>
    public NorbixClientOptions Clone()
    {
        var copy = new NorbixClientOptions
        {
            ApiKey = ApiKey,
            BearerToken = BearerToken,
            ProjectId = ProjectId,
            AccountId = AccountId,
            Env = Env,
            Region = Region,
            ApiBaseUrl = ApiBaseUrl,
            HubBaseUrl = HubBaseUrl,
            ApiVersion = ApiVersion,
            HubVersion = HubVersion,
            Timeout = Timeout,
        };

        foreach (var (k, v) in DefaultHeaders)
        {
            copy.DefaultHeaders[k] = v;
        }

        return copy;
    }

    /// <summary>
    /// Apply <c>NORBIX_*</c> environment variables on top of any values that
    /// are still null/empty. Explicit constructor / configuration values
    /// always win.
    /// </summary>
    /// <param name="source">
    /// Optional in-memory env source. When null, reads from
    /// <see cref="Environment.GetEnvironmentVariable(string)"/>. Useful in tests.
    /// </param>
    public NorbixClientOptions ApplyEnvironment(IDictionary<string, string?>? source = null)
    {
        string? Read(string key) =>
            source is null
                ? Environment.GetEnvironmentVariable(key)
                : (source.TryGetValue(key, out var v) ? v : null);

        ApiKey ??= NullIfEmpty(Read("NORBIX_API_KEY"));
        BearerToken ??= NullIfEmpty(Read("NORBIX_BEARER_TOKEN"));
        ProjectId ??= NullIfEmpty(Read("NORBIX_PROJECT_ID"));
        AccountId ??= NullIfEmpty(Read("NORBIX_ACCOUNT_ID"));

        var env = NullIfEmpty(Read("NORBIX_ENV"));
        if (env is not null) Env = env;

        Region ??= NullIfEmpty(Read("NORBIX_REGION"));

        var apiUrl = NullIfEmpty(Read("NORBIX_API_URL"));
        if (apiUrl is not null) ApiBaseUrl = apiUrl;

        var hubUrl = NullIfEmpty(Read("NORBIX_HUB_URL"));
        if (hubUrl is not null) HubBaseUrl = hubUrl;

        var apiVersion = NullIfEmpty(Read("NORBIX_API_VERSION"));
        if (apiVersion is not null) ApiVersion = apiVersion;

        var hubVersion = NullIfEmpty(Read("NORBIX_HUB_VERSION"));
        if (hubVersion is not null) HubVersion = hubVersion;

        var timeoutMs = NullIfEmpty(Read("NORBIX_TIMEOUT_MS"));
        if (timeoutMs is not null && int.TryParse(timeoutMs, out var ms))
        {
            Timeout = TimeSpan.FromMilliseconds(ms);
        }

        return this;
    }

    /// <summary>Validate fields that must be set before the first request.</summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="ProjectId"/> is missing.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            throw new InvalidOperationException(
                "Norbix: ProjectId is required. Pass it to NorbixClientOptions or set NORBIX_PROJECT_ID.");
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
