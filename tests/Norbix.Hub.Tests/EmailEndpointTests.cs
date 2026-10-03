using Norbix.Sdk.Tests;
using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// One test per live Email endpoint. Each case builds the generated request
/// DTO, calls the generated client method, and snapshots the request that
/// actually left the client: verb, interpolated path, query, headers, body.
///
/// The Email module has two route families: the project routes under
/// <c>/{version}/notifications/email</c> (and the older
/// <c>/{version}/notifications/emails/campaigns/{campaignId}/messages</c>),
/// and the public link routes under <c>/{version}/email</c> (one-click
/// unsubscribe, preferences by signed link).
///
/// Nothing leaves the process — <see cref="NorbixTestFixture"/> wires the
/// client to an in-memory handler, so no email provider is ever contacted.
/// </summary>
[TestFixture]
public sealed class EmailEndpointTests
{
    /// <summary>Project routes: <c>/notifications/email/…</c> and <c>/notifications/emails/…</c>.</summary>
    private const string EmailPathPrefix = "/{version}/notifications/email";

    /// <summary>Public link routes: <c>/email/one-click-unsubscribe</c>, <c>/email/preferences</c>.</summary>
    private const string PublicEmailPathPrefix = "/{version}/email/";

    private static bool IsEmail(string path) =>
        path.StartsWith(EmailPathPrefix, StringComparison.Ordinal)
        || path.StartsWith(PublicEmailPathPrefix, StringComparison.Ordinal);

    public static IEnumerable<TestCaseData> EmailEndpoints()
    {
        return EndpointCoverageDriver.GetEndpointCases(e => IsEmail(e.Path));
    }

    [TestCaseSource(nameof(EmailEndpoints))]
    public async Task Email_endpoint_sends_the_expected_request(string methodName)
    {
        var result = await EndpointCoverageDriver.CoverEndpointAsync(
            e => IsEmail(e.Path) && e.MethodName == methodName
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"EmailEndpointTests.{methodName}");
        await Verifier.Verify(result, settings);
    }

    /// <summary>
    /// Guards the audit: if the gateway grows or drops an Email route and the
    /// DTOs are regenerated, this list changes and the snapshot fails, so the
    /// change cannot slip in untested.
    /// </summary>
    [Test]
    public async Task Email_surface_is_the_expected_size()
    {
        var names = EndpointCoverageDriver
            .Endpoints(e => IsEmail(e.Path))
            .Select(e => $"{e.HttpMethod} {e.Path} -> {e.MethodName}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("EmailEndpointTests.Surface");
        await Verifier.Verify(new { Count = names.Count, Endpoints = names }, settings);
    }
}
