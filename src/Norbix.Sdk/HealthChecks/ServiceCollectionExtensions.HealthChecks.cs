using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Norbix.Sdk.HealthChecks;

public static class NorbixHealthChecksServiceCollectionExtensions
{
    /// <summary>
    /// Register a health check for the Norbix SDK.
    ///
    /// <para>
    /// When <paramref name="ping"/> is <c>false</c>, the check validates configuration only (no network).
    /// When <paramref name="ping"/> is <c>true</c>, it performs a lightweight gateway call (<c>GET /{version}/echo</c>).
    /// </para>
    /// </summary>
    public static IHealthChecksBuilder AddNorbixHealthChecks(
        this IServiceCollection services,
        string name = "norbix",
        bool ping = false,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ensure the SDK is registered; the health check depends on NorbixClient.
        // (We don't auto-register the client here to avoid surprising configuration behavior.)
        var builder = services.AddHealthChecks();
        builder.Add(new HealthCheckRegistration(
            name,
            sp => new NorbixHealthCheck(sp.GetRequiredService<Norbix.Sdk.NorbixClient>(), ping),
            failureStatus,
            tags));
        return builder;
    }
}

