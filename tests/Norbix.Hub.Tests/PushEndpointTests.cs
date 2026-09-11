using Norbix.Sdk.Tests;
using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// One test per live Push endpoint. Each case builds the generated request
/// DTO, calls the generated client method, and snapshots the request that
/// actually left the client: verb, interpolated path, query, headers, body.
///
/// Nothing leaves the process — <see cref="NorbixTestFixture"/> wires the
/// client to an in-memory handler, so no push provider is ever contacted.
/// </summary>
[TestFixture]
public sealed class PushEndpointTests
{
    /// <summary>The route prefix that marks an endpoint as part of the Push module.</summary>
    private const string PushPathPrefix = "/{version}/notifications/push";

    public static IEnumerable<TestCaseData> PushEndpoints()
    {
        return EndpointCoverageDriver.GetEndpointCases(e =>
            e.Path.StartsWith(PushPathPrefix, StringComparison.Ordinal)
        );
    }

    [TestCaseSource(nameof(PushEndpoints))]
    public async Task Push_endpoint_sends_the_expected_request(string methodName)
    {
        var result = await EndpointCoverageDriver.CoverEndpointAsync(
            e =>
                e.Path.StartsWith(PushPathPrefix, StringComparison.Ordinal)
                && e.MethodName == methodName
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"PushEndpointTests.{methodName}");
        await Verifier.Verify(result, settings);
    }

    /// <summary>
    /// Guards the audit: if the gateway grows a Push route and the DTOs are
    /// regenerated, this count changes and the snapshot fails, so the new
    /// endpoint cannot slip in untested.
    /// </summary>
    [Test]
    public async Task Push_surface_is_the_expected_size()
    {
        var names = EndpointCoverageDriver
            .Endpoints(e => e.Path.StartsWith(PushPathPrefix, StringComparison.Ordinal))
            .Select(e => $"{e.HttpMethod} {e.Path} -> {e.MethodName}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("PushEndpointTests.Surface");
        await Verifier.Verify(new { Count = names.Count, Endpoints = names }, settings);
    }
}
