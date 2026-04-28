using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Norbix.Sdk;

/// <summary>
/// Helpers for wiring the SDK into ASP.NET Core / generic-host containers.
/// Optional — the SDK works fine without DI, but most .NET apps will reach
/// for these.
/// </summary>
public static class NorbixServiceCollectionExtensions
{
    /// <summary>
    /// Register the named <see cref="HttpClient"/> used by the Norbix SDK.
    /// Call this if you want to attach policies (e.g. Polly) or customize
    /// low-level handler settings.
    /// </summary>
    public static IHttpClientBuilder AddNorbixHttpClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddHttpClient(NorbixDefaults.HttpClientName);
    }

    /// <summary>
    /// Register a singleton <see cref="NorbixClient"/>. <c>NORBIX_*</c> env vars
    /// fill in any unset field.
    /// </summary>
    public static IServiceCollection AddNorbix(
        this IServiceCollection services,
        Action<NorbixClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddNorbixHttpClient();

        services.AddOptions<NorbixClientOptions>()
            .Configure(opts =>
            {
                configure?.Invoke(opts);
                opts.ApplyEnvironment();
                opts.Validate();
            })
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ProjectId),
                "Norbix: ProjectId is required. Pass it to NorbixClientOptions or set NORBIX_PROJECT_ID.")
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NorbixClientOptions>>().Value;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(NorbixDefaults.HttpClientName);
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<NorbixClient>>();
            return new NorbixClient(options, http, logger);
        });

        return services;
    }

    /// <summary>
    /// Bind <see cref="NorbixClientOptions"/> from configuration (e.g.
    /// appsettings.json section <c>"Norbix"</c>) and register the client.
    /// </summary>
    public static IServiceCollection AddNorbix(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Norbix")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddNorbixHttpClient();

        services.AddOptions<NorbixClientOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Configure(opts =>
            {
                opts.ApplyEnvironment();
                opts.Validate();
            })
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ProjectId),
                "Norbix: ProjectId is required. Set it in configuration or via NORBIX_PROJECT_ID.")
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NorbixClientOptions>>().Value;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(NorbixDefaults.HttpClientName);
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<NorbixClient>>();
            return new NorbixClient(options, http, logger);
        });

        return services;
    }

    /// <summary>
    /// Register a scoped <see cref="NorbixClient"/>. Useful when you want to
    /// attach per-request auth (e.g. forward a user JWT) by constructing a
    /// derived client with <c>WithBearerToken(...)</c>.
    /// </summary>
    public static IServiceCollection AddNorbixScoped(
        this IServiceCollection services,
        Action<NorbixClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddNorbixHttpClient();

        services.AddOptions<NorbixClientOptions>()
            .Configure(opts =>
            {
                configure?.Invoke(opts);
                opts.ApplyEnvironment();
                opts.Validate();
            })
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ProjectId),
                "Norbix: ProjectId is required. Pass it to NorbixClientOptions or set NORBIX_PROJECT_ID.")
            .ValidateOnStart();

        services.AddScoped(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NorbixClientOptions>>().Value;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(NorbixDefaults.HttpClientName);
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<NorbixClient>>();
            return new NorbixClient(options, http, logger);
        });

        return services;
    }
}
