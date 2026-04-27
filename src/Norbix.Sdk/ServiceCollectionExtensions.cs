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
    /// Register a singleton <see cref="NorbixClient"/>. <c>NORBIX_*</c> env
    /// vars fill in any unset field.
    /// </summary>
    public static IServiceCollection AddNorbix(
        this IServiceCollection services,
        Action<NorbixClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<NorbixClientOptions>()
            .Configure(opts =>
            {
                configure?.Invoke(opts);
                opts.ApplyEnvironment();
                opts.Validate();
            });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NorbixClientOptions>>().Value;
            return new NorbixClient(options);
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

        services.AddOptions<NorbixClientOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Configure(opts =>
            {
                opts.ApplyEnvironment();
                opts.Validate();
            });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NorbixClientOptions>>().Value;
            return new NorbixClient(options);
        });

        return services;
    }
}
