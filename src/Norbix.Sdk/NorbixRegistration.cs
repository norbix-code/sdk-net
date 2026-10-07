using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Norbix.Sdk;

/// <summary>
/// The container wiring shared by Norbix.Api and Norbix.Hub. Each package
/// exposes it under its own public names (<c>AddNorbixApi</c> /
/// <c>AddNorbixHub</c>), and each keeps its options under its own name, so one
/// container can hold both clients without their settings mixing.
/// </summary>
internal static class NorbixRegistration
{
    /// <summary>Register the named <see cref="HttpClient"/> the SDK uses.</summary>
    internal static IHttpClientBuilder AddHttpClient(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddHttpClient(NorbixDefaults.HttpClientName);
    }

    /// <summary>
    /// Register <typeparamref name="TClient"/> with options named
    /// <paramref name="optionsName"/>. <c>NORBIX_*</c> env vars fill in any
    /// unset field; a missing project id fails at start-up.
    /// </summary>
    internal static IServiceCollection AddClient<TClient>(
        IServiceCollection services,
        string optionsName,
        ServiceLifetime lifetime,
        Action<NorbixClientOptions>? configure,
        IConfiguration? configuration,
        string sectionName,
        Func<NorbixClientOptions, HttpClient, ILogger?, TClient> create)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(services);

        AddHttpClient(services);

        var options = services.AddOptions<NorbixClientOptions>(optionsName);
        if (configuration is not null)
        {
            options.Bind(configuration.GetSection(sectionName));
        }

        options
            .Configure(opts =>
            {
                configure?.Invoke(opts);
                opts.ApplyEnvironment();
                opts.Validate();
            })
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ProjectId),
                "Norbix: ProjectId is required. Pass it to NorbixClientOptions, set it in configuration or set NORBIX_PROJECT_ID.")
            .ValidateOnStart();

        services.Add(new ServiceDescriptor(
            typeof(TClient),
            sp =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<NorbixClientOptions>>().Get(optionsName);
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(NorbixDefaults.HttpClientName);
                var logger = sp.GetService<ILogger<TClient>>();
                return create(opts, http, logger);
            },
            lifetime));

        return services;
    }
}
