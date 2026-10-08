using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The four account routes a person calls before they have a session:
/// sign-up (<c>POST /account</c>), joining a team from an invitation
/// (<c>POST /account/team/member</c>), the region list for the sign-up form
/// (<c>GET /account/regions</c>) and the e-mail verification link
/// (<c>GET /account/verify</c>). The gateway has no <c>[Authenticate]</c> on
/// any of them, and it never reads the <c>norbix-account-id</c> header, so the
/// request types carry <c>INorbixUnauthenticated</c> and the client sends
/// them with no <c>Authorization</c> header.
/// <para>
/// Every test uses a client with no API key, no bearer token and no account
/// id (the matching <c>NORBIX_*</c> environment variables are cleared too).
/// <c>VerifyAccount</c> carries the account id once, in its own query.
/// The snapshot also holds the endpoint catalog row, so a scope regression
/// shows up as a diff. Nothing leaves the process.
/// </para>
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class AccountPublicEndpointTests
{
    private static readonly string[] CredentialVariables =
    [
        "NORBIX_API_KEY",
        "NORBIX_BEARER_TOKEN",
        "NORBIX_ACCOUNT_ID",
    ];

    private readonly Dictionary<string, string?> _savedVariables = new();

    [SetUp]
    public void ClearCredentialVariables()
    {
        foreach (var name in CredentialVariables)
        {
            _savedVariables[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TearDown]
    public void RestoreCredentialVariables()
    {
        foreach (var (name, value) in _savedVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static NorbixTestFixture AnonymousClient() =>
        NorbixTestFixture.Create(o =>
        {
            o.ApiKey = null;
            o.BearerToken = null;
            o.AccountId = null;
        });

    private static async Task VerifyAnonymousCall(
        string name,
        NorbixTestFixture fixture,
        Type requestType
    )
    {
        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"AccountPublicEndpointTests.{name}");
        await Verifier.Verify(
            new
            {
                Client = new
                {
                    ApiKey = fixture.Client.Options.ApiKey ?? "(none)",
                    BearerToken = fixture.Client.Options.BearerToken ?? "(none)",
                    AccountId = fixture.Client.Options.AccountId ?? "(none)",
                },
                Catalog = NorbixEndpointCatalog
                    .All.Where(e => e.RequestType == requestType)
                    .Select(e => new { e.IsAccountScoped, e.IsUnauthenticated })
                    .SingleOrDefault(),
                Sent = fixture.LastRequest,
            },
            settings
        );
    }

    [Test]
    public async Task CreateAccount_works_with_no_token_and_no_account_id()
    {
        using var fixture = AnonymousClient();
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.CreateAccountAsync(
            new CreateAccount
            {
                DisplayName = "Ada",
                Email = "ada@example.test",
                Password = "pa55-word",
            }
        );

        await VerifyAnonymousCall("CreateAccount", fixture, typeof(CreateAccount));
    }

    [Test]
    public async Task CreateTeamMemberFromInvitation_works_with_no_token_and_no_account_id()
    {
        using var fixture = AnonymousClient();
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.CreateTeamMemberFromInvitationAsync(
            new CreateTeamMemberFromInvitation
            {
                DisplayName = "Ada",
                Token = "invite-token-1",
                Password = "pa55-word",
            }
        );

        await VerifyAnonymousCall(
            "CreateTeamMemberFromInvitation",
            fixture,
            typeof(CreateTeamMemberFromInvitation)
        );
    }

    [Test]
    public async Task GetAccountRegions_works_with_no_token_and_no_account_id()
    {
        using var fixture = AnonymousClient();
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.GetAccountRegionsAsync(new GetAccountRegions());

        await VerifyAnonymousCall("GetAccountRegions", fixture, typeof(GetAccountRegions));
    }

    [Test]
    public async Task VerifyAccount_sends_the_account_id_once_in_the_query()
    {
        using var fixture = AnonymousClient();
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.VerifyAccountAsync(
            new VerifyAccount { AccountId = "account-1", Token = "verify-token-1" }
        );

        await VerifyAnonymousCall("VerifyAccount", fixture, typeof(VerifyAccount));
    }
}
