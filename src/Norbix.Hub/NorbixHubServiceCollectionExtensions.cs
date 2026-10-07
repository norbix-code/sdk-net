using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Norbix.Sdk;

/// <summary>
/// Helpers for wiring <see cref="NorbixHubClient"/> into ASP.NET Core /
/// generic-host containers. Optional — the SDK works fine without DI.
/// The Norbix.Api package has the same helpers as <c>AddNorbixApi…</c>, so
/// one container can register both clients.
/// </summary>
public static class NorbixHubServiceCollectionExtensions
{
    /// <summary>Name of the <see cref="NorbixClientOptions"/> instance the Hub client reads.</summary>
    public const string OptionsName = "Norbix.Hub";

    /// <summary>
    /// Register the named <see cref="HttpClient"/> used by the Norbix SDK.
    /// Call this if you want to attach policies (e.g. Polly) or customize
    /// low-level handler settings.
    /// </summary>
    public static IHttpClientBuilder AddNorbixHubHttpClient(this IServiceCollection services) =>
        NorbixRegistration.AddHttpClient(services);

    /// <summary>
    /// Register a singleton <see cref="NorbixHubClient"/>. <c>NORBIX_*</c> env
    /// vars fill in any unset field.
    /// </summary>
    public static IServiceCollection AddNorbixHub(
        this IServiceCollection services,
        Action<NorbixClientOptions>? configure = null) =>
        NorbixRegistration.AddClient(
            services, OptionsName, ServiceLifetime.Singleton, configure, null, "",
            static (o, http, log) => new NorbixHubClient(o, http, log));

    /// <summary>
    /// Bind <see cref="NorbixClientOptions"/> from configuration (e.g.
    /// appsettings.json section <c>"Norbix"</c>) and register a singleton
    /// <see cref="NorbixHubClient"/>.
    /// </summary>
    public static IServiceCollection AddNorbixHub(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Norbix")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return NorbixRegistration.AddClient(
            services, OptionsName, ServiceLifetime.Singleton, null, configuration, sectionName,
            static (o, http, log) => new NorbixHubClient(o, http, log));
    }

    /// <summary>
    /// Register a scoped <see cref="NorbixHubClient"/>. Useful when you want to
    /// attach per-request auth (e.g. forward a user JWT) by constructing a
    /// derived client with <c>WithBearerToken(...)</c>.
    /// </summary>
    public static IServiceCollection AddNorbixHubScoped(
        this IServiceCollection services,
        Action<NorbixClientOptions>? configure = null) =>
        NorbixRegistration.AddClient(
            services, OptionsName, ServiceLifetime.Scoped, configure, null, "",
            static (o, http, log) => new NorbixHubClient(o, http, log));
}
