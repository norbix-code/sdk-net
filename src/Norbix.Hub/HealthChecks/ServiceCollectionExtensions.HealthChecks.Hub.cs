using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Norbix.Sdk.HealthChecks;

public static class NorbixHealthChecksServiceCollectionExtensions
{
    public static IHealthChecksBuilder AddNorbixHealthChecks(
        this IServiceCollection services,
        string name = "norbix",
        bool ping = false,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddHealthChecks();
        builder.Add(new HealthCheckRegistration(
            name,
            sp => new NorbixHealthCheck(sp.GetRequiredService<Norbix.Sdk.NorbixClient>(), ping),
            failureStatus,
            tags));
        return builder;
    }
}

