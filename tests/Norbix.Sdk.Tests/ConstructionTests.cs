using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// Construction surface — what callers see at the seam between their code
/// and the SDK. Each test snapshots a small POCO that exposes only the
/// public state the SDK promises.
/// </summary>
[TestFixture]
public sealed class ConstructionTests
{
    [Test]
    public Task ZeroArg_constructor_throws_without_NORBIX_PROJECT_ID()
    {
        // Capture the failure mode as a snapshot — message + type lock the
        // public contract.
        try
        {
            using var _ = new NorbixClient();
            return Verifier.Verify(new { Threw = false }, VerifyConfig.VerifySettings);
        }
        catch (InvalidOperationException ex)
        {
            return Verifier.Verify(new { Threw = true, ex.Message }, VerifyConfig.VerifySettings);
        }
    }

    [Test]
    public async Task Options_built_from_explicit_args()
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.ApiKey = "explicit-key";
            o.ProjectId = "explicit-project";
            o.AccountId = "explicit-account";
        });

        await Verifier.Verify(new
        {
            fixture.Options.ApiKey,
            fixture.Options.ProjectId,
            fixture.Options.AccountId,
            fixture.Options.ApiBaseUrl,
            fixture.Options.HubBaseUrl,
            fixture.Options.ApiVersion,
            fixture.Client.IsAuthenticated,
        }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Options_filled_from_environment_when_unset()
    {
        var options = new NorbixClientOptions().ApplyEnvironment(new Dictionary<string, string?>
        {
            ["NORBIX_API_KEY"] = "env-key",
            ["NORBIX_PROJECT_ID"] = "env-project",
            ["NORBIX_ACCOUNT_ID"] = "env-account",
            ["NORBIX_API_URL"] = "https://api.staging.norbix.dev",
            ["NORBIX_HUB_URL"] = "https://hub.staging.norbix.dev",
            ["NORBIX_API_VERSION"] = "v3",
            ["NORBIX_TIMEOUT_MS"] = "5000",
        });

        await Verifier.Verify(new
        {
            options.ApiKey,
            options.ProjectId,
            options.AccountId,
            options.ApiBaseUrl,
            options.HubBaseUrl,
            options.ApiVersion,
            TimeoutMs = options.Timeout.TotalMilliseconds,
        }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Explicit_options_win_over_environment()
    {
        var options = new NorbixClientOptions
        {
            ApiKey = "explicit",
            ProjectId = "explicit-project",
        }.ApplyEnvironment(new Dictionary<string, string?>
        {
            ["NORBIX_API_KEY"] = "env-key",
            ["NORBIX_PROJECT_ID"] = "env-project",
        });

        await Verifier.Verify(new { options.ApiKey, options.ProjectId }, VerifyConfig.VerifySettings);
    }
}
