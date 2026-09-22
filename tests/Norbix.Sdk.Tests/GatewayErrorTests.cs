using System.Net;

using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;

namespace Norbix.Sdk.Tests;

/// <summary>
/// What the caller sees when a call fails (10b-files slice ERRORS, issues #66
/// and #67).
/// <para>
/// Two rules are pinned here. First, the message and the error code are the
/// gateway's own. The gateway puts them inside <c>responseStatus.errors[]</c>,
/// so reading the top of that block gave every caller "Request failed with
/// status 404" and no code — that was #66. Second, a call fails when the
/// gateway says it failed, even when the HTTP status is 200 and the body says
/// <c>responseStatus.isSuccess = false</c> — that was #67.
/// </para>
/// <para>
/// Every test builds its own client and its own fake answer, so the order the
/// tests run in does not matter and no real server is contacted.
/// </para>
/// </summary>
[TestFixture]
public sealed class GatewayErrorTests
{
    private static Norbix.Sdk.Types.Api.Echo AnyRequest() => new();

    /// <summary>(a) HTTP 400 with two errors inside responseStatus.errors.</summary>
    [Test]
    public void A_400_takes_message_and_code_from_the_first_error_and_keeps_them_all()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/echo",
            new
            {
                responseStatus = new
                {
                    isSuccess = false,
                    errors = new object[]
                    {
                        new
                        {
                            message = "File name is required",
                            errorCode = "CM-ERRORS-FILES-002",
                            fieldName = "fileName",
                        },
                        new
                        {
                            message = "Folder does not exist",
                            errorCode = "CM-ERRORS-FILES-016",
                        },
                    },
                },
            },
            status: HttpStatusCode.BadRequest
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () => await fixture.Client.Echo.EchoAsync(AnyRequest())
        )!;

        Assert.That(ex.Message, Is.EqualTo("File name is required"));
        Assert.That(ex.ErrorCode, Is.EqualTo("CM-ERRORS-FILES-002"));
        Assert.That(ex.HttpStatus, Is.EqualTo(400));
        Assert.That(ex.Errors, Has.Count.EqualTo(2));
        Assert.That(ex.Errors[0].FieldName, Is.EqualTo("fileName"));
        Assert.That(ex.Errors[1].ErrorCode, Is.EqualTo("CM-ERRORS-FILES-016"));
        Assert.That(ex.Body, Is.Not.Null);
    }

    /// <summary>(b) HTTP 200 whose body says the call failed.</summary>
    [Test]
    public void A_200_that_says_isSuccess_false_fails_with_the_gateway_message()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/echo",
            new
            {
                responseStatus = new
                {
                    isSuccess = false,
                    errors = new[]
                    {
                        new
                        {
                            message = "Integration with id int_42 not found",
                            errorCode = "CM-ERRORS-INTEGRATIONS-001",
                        },
                    },
                },
            }
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () => await fixture.Client.Echo.EchoAsync(AnyRequest())
        )!;

        Assert.That(ex.HttpStatus, Is.EqualTo(200));
        Assert.That(ex.Message, Is.EqualTo("Integration with id int_42 not found"));
        Assert.That(ex.ErrorCode, Is.EqualTo("CM-ERRORS-INTEGRATIONS-001"));
    }

    /// <summary>(c) HTTP 200 that says the call worked — unchanged.</summary>
    [Test]
    public async Task A_200_that_says_isSuccess_true_still_comes_back_as_a_value()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/echo", new { responseStatus = new { isSuccess = true } });

        var res = await fixture.Client.Echo.EchoAsync(AnyRequest());

        Assert.That(res, Is.Not.Null);
    }

    [Test]
    public async Task A_200_with_no_responseStatus_still_comes_back_as_a_value()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/echo", new { });

        var res = await fixture.Client.Echo.EchoAsync(AnyRequest());

        Assert.That(res, Is.Not.Null);
    }

    /// <summary>(d) A 500 whose body is not JSON at all.</summary>
    [Test]
    public void A_500_with_a_body_that_is_not_JSON_uses_the_fallback_text()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondText("/echo", "<html>Bad Gateway</html>", HttpStatusCode.InternalServerError);

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () => await fixture.Client.Echo.EchoAsync(AnyRequest())
        )!;

        Assert.That(ex.Message, Is.EqualTo("Request failed (HTTP 500)"));
        Assert.That(ex.ErrorCode, Is.Null);
        Assert.That(ex.HttpStatus, Is.EqualTo(500));
    }

    [Test]
    public void An_empty_error_body_uses_the_fallback_text_too()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/echo", new { }, status: HttpStatusCode.NotFound);

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () => await fixture.Client.Echo.EchoAsync(AnyRequest())
        )!;

        Assert.That(ex.Message, Is.EqualTo("Request failed (HTTP 404)"));
    }

    [Test]
    public void Reads_the_top_of_the_body_when_there_is_no_responseStatus()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/echo",
            new { message = "Already exists", errorCode = "CM-ERRORS-FILES-009" },
            status: HttpStatusCode.Conflict
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () => await fixture.Client.Echo.EchoAsync(AnyRequest())
        )!;

        Assert.That(ex.Message, Is.EqualTo("Already exists"));
        Assert.That(ex.ErrorCode, Is.EqualTo("CM-ERRORS-FILES-009"));
    }
}
