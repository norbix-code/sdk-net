using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// owner 2026-10-22: /internal/* is not SDK surface. <c>GET /internal/_typegen</c>
/// is the gateway's type-generator tooling: its DTO may stay in Hub.dtos.cs, but
/// the source generator must build no method and no <c>Internal</c> module for it.
/// </summary>
[TestFixture]
public sealed class NoInternalRoutesTests
{
    [Test]
    public async Task The_hub_client_has_no_internal_route_and_no_Internal_module()
    {
        var internalRoutes = NorbixEndpointCatalog
            .All.Where(e => e.Path.StartsWith("/internal/", StringComparison.Ordinal))
            .Select(e => $"{e.HttpMethod} {e.Path}")
            .ToArray();

        await Verifier.Verify(
            new
            {
                InternalRoutes = internalRoutes.Length == 0 ? "(none)" : string.Join(", ", internalRoutes),
                InternalModuleProperty = typeof(NorbixHubClient).GetProperty("Internal")?.Name ?? "(none)",
            },
            VerifyConfig.VerifySettings);
    }
}
