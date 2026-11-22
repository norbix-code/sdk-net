using System.Net;

using Norbix.Sdk;
using Norbix.Sdk.Auth;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// E-mail address verification for e-mail + password sign-up:
/// POST /{version}/membership/userauth/email-verification/confirm (the token
/// from the link) and /resend (a new link, always OK). Both are public: a
/// client with no API key and no bearer token sends them with no
/// <c>Authorization</c> header. While the project requires a verified address,
/// a password sign-in of an unverified user is HTTP 401
/// <c>CM-ERRORS-MEMBERSHIP-USERS-052</c>, which
/// <see cref="NorbixException.IsEmailNotVerified"/> recognises. Nothing leaves
/// the process.
/// </summary>
[TestFixture]
public sealed class EmailAddressVerificationTests
{
    [Test]
    public async Task Verify_goes_out_with_no_credentials_and_reads_the_answer()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondNext(new { authId = "usr_1", verifiedAtUtc = "2026-11-22T10:00:00Z" });

        var response = await fixture.Client.Membership.VerifyEmailAddressAsync(
            new VerifyEmailAddressRequest { Token = "tok-1" });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response?.AuthId, response?.VerifiedAtUtc },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Verify_still_sends_a_configured_key_and_the_integration()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(new { authId = "usr_1" });

        await fixture.Client.Membership.VerifyEmailAddressAsync(
            new VerifyEmailAddressRequest { Token = "tok-1", DatabaseIntegrationId = "int_db" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Resend_goes_out_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondNext(new { });

        await fixture.Client.Membership.ResendEmailAddressVerificationAsync(
            new ResendEmailAddressVerificationRequest { Email = "ada@example.test" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task A_used_link_is_refused_with_the_gateway_code()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondNext(
            new
            {
                responseStatus = new
                {
                    errorCode = "CM-ERRORS-MEMBERSHIP-USERS-051",
                    message = "The verification link is invalid or expired.",
                },
            },
            HttpStatusCode.BadRequest);

        var error = Assert.ThrowsAsync<NorbixException>(
            () => fixture.Client.Membership.VerifyEmailAddressAsync(new VerifyEmailAddressRequest { Token = "used" }));

        Assert.That(error!.ErrorCode, Is.EqualTo("CM-ERRORS-MEMBERSHIP-USERS-051"));
        Assert.That(error.IsEmailNotVerified, Is.False);
        Assert.That(NorbixErrorCodes.IsEmailNotVerified(error), Is.False);
    }

    [Test]
    public void Password_sign_in_of_an_unverified_user_is_recognised()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondNext(
            new
            {
                responseStatus = new
                {
                    errorCode = "Unauthorized",
                    message = "Confirm your e-mail address first.",
                    errors = new object[]
                    {
                        new { errorCode = "CM-ERRORS-MEMBERSHIP-USERS-052", message = "Confirm your e-mail address first." },
                    },
                },
            },
            HttpStatusCode.Unauthorized);

        var error = Assert.ThrowsAsync<NorbixException>(
            () => fixture.Client.LoginAsync(new LoginCredentials { UserName = "ada@example.test", Password = "pw-1" }));

        Assert.That(NorbixErrorCodes.EmailNotVerified, Is.EqualTo("CM-ERRORS-MEMBERSHIP-USERS-052"));
        Assert.That(error!.HttpStatus, Is.EqualTo(401));
        Assert.That(error.IsEmailNotVerified, Is.True);
        Assert.That(NorbixErrorCodes.IsEmailNotVerified(error), Is.True);
    }

    [Test]
    public void Other_errors_are_not_the_e_mail_refusal()
    {
        Assert.That(NorbixErrorCodes.IsEmailNotVerified(null), Is.False);
        Assert.That(NorbixErrorCodes.IsEmailNotVerified(new InvalidOperationException()), Is.False);
        Assert.That(
            NorbixErrorCodes.IsEmailNotVerified(new NorbixException("nope", 401, "CM-ERRORS-MEMBERSHIP-USERS-001")),
            Is.False);
        Assert.That(
            NorbixErrorCodes.IsEmailNotVerified(new NorbixException("verify", 401, NorbixErrorCodes.EmailNotVerified)),
            Is.True);
    }
}
