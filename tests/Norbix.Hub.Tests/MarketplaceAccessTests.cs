using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Routes 25–27 of the marketplace contract (gateway item access): the access
/// matrix of a connection, a role's package and a custom access policy. The
/// coverage snapshot sends placeholder ids; these cases pin a real connection id
/// in the path, no body on the GET, the role and the ticks in the PUT / POST
/// body, and the answers read back from the generated responses.
/// Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class MarketplaceAccessTests
{
    private const string Integration = "int_6Qp1dW2kRm8NsT4vYc0Hb";
    private const string Role = "acc_6Qp1dW2kRm8NsT4vYc0Hb_nr_3kLm9QwErTyU";
    private static readonly string[] ReadOnlyAdded = ["get_me"];
    private static readonly string[] NotGrantable = ["bad name"];

    private static NorbixTestFixture NewFixture() =>
        NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });

    [Test]
    public async Task Access_matrix_puts_the_connection_id_in_the_path_with_no_body()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(new
        {
            roles = new[]
            {
                new
                {
                    roleId = Role,
                    name = "Projects Developer",
                    scope = "account",
                    isSystem = true,
                    @fixed = false,
                    package = "None",
                    isCustom = false,
                    manage = false,
                    custom = Array.Empty<object>(),
                    defaultNotApplied = "The person who enabled the connection may not manage policies.",
                },
            },
            packages = new[] { new { kind = "ReadOnly", policyId = "pol_1", name = "GitHub — Read-only", exists = true } },
            tools = new[] { new { name = "get_me", readOnly = true, grantable = true } },
            functions = new[] { new { functionId = "func_1", name = "Create issue", functionKey = "create_issue" } },
        });

        var response = await fixture.Client.Code.GetMarketplaceIntegrationAccessAsync(
            new GetMarketplaceIntegrationAccess { IntegrationViewId = Integration });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response!.Roles, response.Packages, response.Tools, response.Functions },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Role_package_is_a_put_with_the_role_in_the_body()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(new { id = Role });

        var response = await fixture.Client.Code.SetMarketplaceIntegrationRoleAccessAsync(
            new SetMarketplaceIntegrationRoleAccess
            {
                IntegrationViewId = Integration,
                RoleId = Role,
                Package = MarketplaceAccessLevel.ReadOnly,
                Manage = false,
            });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response!.Id },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Custom_policy_is_a_post_with_the_ticks_in_the_body()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(new { policyId = "pol_9", created = true, usedByRoles = 1 });

        var response = await fixture.Client.Code.SaveMarketplaceIntegrationCustomAccessAsync(
            new SaveMarketplaceIntegrationCustomAccess
            {
                IntegrationViewId = Integration,
                RoleId = Role,
                BasePackage = MarketplaceAccessLevel.ReadOnly,
                Tools = ["create_issue"],
                Functions = ["func_4hT2b9QkLm3XcT1yRfGh7a"],
                Manage = false,
                NewPolicyForThisRoleOnly = true,
            });

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response!.PolicyId, response.Created, response.UsedByRoles },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Test_response_reads_access_changes_and_the_step_error_code()
    {
        using var fixture = NewFixture();
        fixture.RespondNext(new
        {
            steps = new[]
            {
                new
                {
                    key = "mcp.tools",
                    label = "Tools listed",
                    requiredForFunctions = true,
                    succeeded = false,
                    skipped = false,
                    message = "The server listed no tools.",
                    errorCode = "CM-ERRORS-MARKETPLACE-018",
                },
            },
            accessChanges = new { readOnlyAdded = ReadOnlyAdded, readOnlyRemoved = Array.Empty<string>(), notGrantable = NotGrantable },
        });

        var response = await fixture.Client.Code.TestMarketplaceIntegrationAsync(
            new TestMarketplaceIntegration { IntegrationViewId = Integration });

        await Verifier.Verify(
            new { response!.Steps, response.AccessChanges },
            VerifyConfig.VerifySettings);
    }
}
