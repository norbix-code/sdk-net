using System.Net;

using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Transport behaviors — URL building, version substitution, and error
/// envelope parsing. Snapshot tests so any change to the wire format is
/// visible in a code review.
/// </summary>
[TestFixture]
public sealed class TransportTests
{
    [Test]
    public async Task Replaces_version_token_in_path()
    {
        using var fixture = NorbixTestFixture.Create(o => o.HubVersion = "v2");
        // Echo lives on /auth-style root in our placeholder DTOs; use it
        // as a concrete request that exercises {version} substitution
        // when sent via the hub. (The placeholder Hub Echo points at
        // /{version}/echo so it covers the version branch.)
        await fixture.Client.Hub.Echo.EchoAsync(new Norbix.Sdk.Types.Hub.Echo());
        await Verifier.Verify(new
        {
            path = fixture.LastRequest!.Path,
            fixture.Options.HubVersion,
        }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Parses_response_status_into_NorbixException()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/echo", new
        {
            responseStatus = new
            {
                errorCode = "ValidationError",
                message = "name is required",
                errors = new[]
                {
                    new { fieldName = "name", message = "name is required" },
                },
            },
        }, status: HttpStatusCode.BadRequest);

        try
        {
            await fixture.Client.Api.Echo.EchoAsync(new Norbix.Sdk.Types.Api.Echo());
            await Verifier.Verify(new { Threw = false }, VerifyConfig.VerifySettings);
        }
        catch (NorbixException ex)
        {
            await Verifier.Verify(new
            {
                Threw = true,
                ex.StatusCode,
                ex.Code,
                ex.Message,
                FieldErrors = ex.FieldErrors.Select(e => new { e.FieldName, e.Message }),
            }, VerifyConfig.VerifySettings);
        }
    }
}
