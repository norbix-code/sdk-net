using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Norbix.Sdk;

/// <summary>
/// Helpers for wiring <see cref="NorbixApiClient"/> into ASP.NET Core /
/// generic-host containers. Optional — the SDK works fine without DI.
/// The Norbix.Hub package has the same helpers as <c>AddNorbixHub…</c>, so
/// one container can register both clients.
/// </summary>
public static class NorbixApiServiceCollectionExtensions
{
    /// <summary>Name of the <see cref="NorbixClientOptions"/> instance the API client reads.</summary>
    public const string OptionsName = "Norbix.Api";

    /// <summary>
    /// Register the named <see cref="HttpClient"/> used by the Norbix SDK.
    /// Call this if you want to attach policies (e.g. Polly) or customize
    /// low-level handler settings.
    /// </summary>
    public static IHttpClientBuilder AddNorbixApiHttpClient(this IServiceCollection services) =>
        NorbixRegistration.AddHttpClient(services);

    /// <summary>
    /// Register a singleton <see cref="NorbixApiClient"/>. <c>NORBIX_*</c> env
    /// vars fill in any unset field.
    /// </summary>
    public static IServiceCollection AddNorbixApi(
        this IServiceCollection services,
        Action<NorbixClientOptions>? configure = null) =>
        NorbixRegistration.AddClient(
            services, OptionsName, ServiceLifetime.Singleton, configure, null, "",
            static (o, http, log) => new NorbixApiClient(o, http, log));

    /// <summary>
    /// Bind <see cref="NorbixClientOptions"/> from configuration (e.g.
    /// appsettings.json section <c>"Norbix"</c>) and register a singleton
    /// <see cref="NorbixApiClient"/>.
    /// </summary>
    public static IServiceCollection AddNorbixApi(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Norbix")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return NorbixRegistration.AddClient(
            services, OptionsName, ServiceLifetime.Singleton, null, configuration, sectionName,
            static (o, http, log) => new NorbixApiClient(o, http, log));
    }

    /// <summary>
    /// Register a scoped <see cref="NorbixApiClient"/>. Useful when you want to
    /// attach per-request auth (e.g. forward a user JWT) by constructing a
    /// derived client with <c>WithBearerToken(...)</c>.
    /// </summary>
    public static IServiceCollection AddNorbixApiScoped(
        this IServiceCollection services,
        Action<NorbixClientOptions>? configure = null) =>
        NorbixRegistration.AddClient(
            services, OptionsName, ServiceLifetime.Scoped, configure, null, "",
            static (o, http, log) => new NorbixApiClient(o, http, log));
}
