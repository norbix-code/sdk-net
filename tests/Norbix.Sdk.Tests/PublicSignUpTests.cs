using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Public sign-up (POST /{version}/membership/auth/register/email and
/// /register/user-name). The gateway signs the caller in only when the request
/// brings credentials; with none it runs as the project's Guest role. The
/// contract still says "requires auth" for these two, so the export marks them
/// <c>INorbixOptionalAuth</c> by their <c>IPublicRegistrationRequest</c>
/// interface. A client with no API key and no bearer token must send them —
/// with no <c>Authorization</c> header — instead of failing with
/// <c>NORBIX_NOT_AUTHENTICATED</c> before anything is sent. Nothing leaves the
/// process.
/// </summary>
[TestFixture]
public sealed class PublicSignUpTests
{
    [Test]
    public async Task Sign_up_with_email_goes_out_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondNext(new { id = "usr_1" });

        var response = await fixture.Client.Membership.SaveEmailUserAsync(
            new SaveEmailUser { Email = "ada@example.test", Password = "pw-1" });

        await Verifier.Verify(new { Sent = fixture.LastRequest, response?.Id }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Sign_up_with_user_name_goes_out_with_no_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);
        fixture.RespondNext(new { id = "usr_2" });

        await fixture.Client.Membership.SaveUserNameUserAsync(
            new SaveUserNameUser { UserName = "ada", Password = "pw-1" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Sign_up_with_email_still_sends_a_configured_key()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(new { id = "usr_3" });

        await fixture.Client.Membership.SaveEmailUserAsync(
            new SaveEmailUser { Email = "ada@example.test", Password = "pw-1" });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task A_guest_user_still_needs_credentials()
    {
        using var fixture = NorbixTestFixture.Create(o => o.ApiKey = null);

        var error = Assert.ThrowsAsync<NorbixException>(
            () => fixture.Client.Membership.SaveGuestUserAsync(new SaveGuestUser()));

        await Verifier.Verify(
            new { error!.Code, RequestsSent = fixture.RecordedRequests.Count },
            VerifyConfig.VerifySettings);
    }
}
