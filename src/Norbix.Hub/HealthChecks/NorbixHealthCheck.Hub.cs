using Microsoft.Extensions.Diagnostics.HealthChecks;

using Norbix.Sdk.Transport;
using Norbix.Sdk.Types.Hub;

namespace Norbix.Sdk.HealthChecks;

internal sealed class NorbixHealthCheck(NorbixClient client, bool ping)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            client.Options.Validate();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Norbix options are invalid.", ex);
        }

        if (!ping)
        {
            return HealthCheckResult.Healthy("Norbix options are valid.");
        }

        try
        {
            _ = await client.Transport.SendAsync<EchoResponse>(
                    new NorbixRequestSpec
                    {
                        Target = NorbixTarget.Hub,
                        Path = "/{version}/echo",
                        Method = "GET",
                        Request = new Echo(),
                        PathParams = Array.Empty<string>(),
                        Scope = NorbixScope.Project,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy("Norbix gateway is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Norbix gateway check failed.", ex);
        }
    }
}

