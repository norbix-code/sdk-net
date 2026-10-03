using Norbix.Sdk.Tests;
using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// One test per live SMS endpoint. Each case builds the generated request
/// DTO, calls the generated client method, and snapshots the request that
/// actually left the client: verb, interpolated path, query, headers, body.
///
/// Nothing leaves the process — <see cref="NorbixTestFixture"/> wires the
/// client to an in-memory handler, so no SMS provider is ever contacted.
/// Same shape as <see cref="PushEndpointTests"/>.
/// </summary>
[TestFixture]
public sealed class SmsEndpointTests
{
    /// <summary>The route prefix that marks an endpoint as part of the SMS module.</summary>
    private const string SmsPathPrefix = "/{version}/notifications/sms";

    public static IEnumerable<TestCaseData> SmsEndpoints()
    {
        return EndpointCoverageDriver.GetEndpointCases(e =>
            e.Path.StartsWith(SmsPathPrefix, StringComparison.Ordinal)
        );
    }

    [TestCaseSource(nameof(SmsEndpoints))]
    public async Task Sms_endpoint_sends_the_expected_request(string methodName)
    {
        var result = await EndpointCoverageDriver.CoverEndpointAsync(
            e =>
                e.Path.StartsWith(SmsPathPrefix, StringComparison.Ordinal)
                && e.MethodName == methodName
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"SmsEndpointTests.{methodName}");
        await Verifier.Verify(result, settings);
    }

    /// <summary>
    /// Guards the audit: if the gateway grows an SMS route and the DTOs are
    /// regenerated, this count changes and the snapshot fails, so the new
    /// endpoint cannot slip in untested.
    /// </summary>
    [Test]
    public async Task Sms_surface_is_the_expected_size()
    {
        var names = EndpointCoverageDriver
            .Endpoints(e => e.Path.StartsWith(SmsPathPrefix, StringComparison.Ordinal))
            .Select(e => $"{e.HttpMethod} {e.Path} -> {e.MethodName}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("SmsEndpointTests.Surface");
        await Verifier.Verify(new { Count = names.Count, Endpoints = names }, settings);
    }
}
