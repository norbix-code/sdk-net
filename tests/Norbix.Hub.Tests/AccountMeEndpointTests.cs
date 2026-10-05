using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The signed-in account user's own profile and phone, and the team list.
/// The gateway reads the account from the session for all three, so the
/// client needs only a token — no <c>AccountId</c>.
///
/// The phone saved here is the number "Account users" SMS campaigns send to.
/// Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class AccountMeEndpointTests
{
    private static NorbixTestFixture TokenOnlyClient() =>
        NorbixTestFixture.Create(o => o.BearerToken = "test-bearer");

    [Test]
    public async Task Get_my_profile_sends_a_get_and_reads_the_profile()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNext(
            new
            {
                item = new
                {
                    id = "user-1",
                    email = "ada@example.test",
                    generalInfo = new { phone = "+37060000000" },
                },
            }
        );

        var response = await fixture.Client.Account.GetMyAccountUserProfileAsync(
            new GetMyAccountUserProfile()
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("AccountMeEndpointTests.GetMyProfile");
        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                response!.Item!.Email,
                response.Item.GeneralInfo!.Phone,
            },
            settings
        );
    }

    [Test]
    public async Task Update_my_phone_sends_the_phone_in_a_put_body()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.UpdateMyAccountUserPhoneAsync(
            new UpdateMyAccountUserPhone { Phone = "+37060000000" }
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("AccountMeEndpointTests.UpdateMyPhone");
        await Verifier.Verify(fixture.LastRequest, settings);
    }

    /// <summary>
    /// The team list pages with flat query fields (no nested paging object)
    /// and can be narrowed to one project's collaborators.
    /// </summary>
    [Test]
    public async Task Team_list_sends_flat_paging_and_the_project_filter()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.GetAccountCollaboratorsAsync(
            new GetAccountCollaborators
            {
                ProjectId = "proj-1",
                PageSize = 50,
                StartingAfter = "member-20",
            }
        );

        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName("AccountMeEndpointTests.TeamList");
        await Verifier.Verify(fixture.LastRequest, settings);
    }
}
