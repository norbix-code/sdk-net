using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;

using NUnit.Framework;

using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// Managed Service bundles and the account's Norbix-managed Atlas clusters
/// (gateway 14352de08):
/// GET /{version}/account/stripe/bundles, POST /{version}/account/stripe/change-bundle,
/// GET /{version}/account/flex-clusters and
/// POST /{version}/account/flex-clusters/{flexClusterId}/upgrade.
///
/// The gateway reads the account from the session, so the client needs only a
/// token — no <c>AccountId</c>. Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class AccountBundlesAndFlexClustersTests
{
    private const string ClusterId = "fc_7Hq2mN4bV8cX1zL6kP3rT9";

    private static readonly string[] NearLimits = ["storage"];

    private static NorbixTestFixture TokenOnlyClient() =>
        NorbixTestFixture.Create(o => o.BearerToken = "test-bearer");

    [Test]
    public async Task Bundles_send_a_get_with_no_body_and_read_the_cap_and_both_prices()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNext(new
        {
            bundles = new object[]
            {
                new
                {
                    bundle = "Growth",
                    projectCap = 10,
                    monthly = new { amount = 4900L, currency = "usd" },
                    annual = new { amount = 49000L, currency = "usd" },
                },
                new
                {
                    bundle = "Scale",
                    projectCap = 50,
                    monthly = new { amount = 5000000000L, currency = "usd" },
                    annual = new { amount = 50000000000L, currency = "usd" },
                },
            },
        });

        var response = await fixture.Client.Account.GetManagedServiceBundlesAsync(new GetManagedServiceBundles());

        await Verifier.Verify(new { Sent = fixture.LastRequest, response!.Bundles }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Change_bundle_posts_the_bundle_and_the_billing_period_in_the_body()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.ChangeManagedServiceBundleAsync(new ChangeManagedServiceBundle
        {
            Bundle = ManagedServiceBundle.Growth,
            BillingPeriod = SubscriptionBillingPeriod.Annual,
        });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Flex_clusters_send_a_get_with_no_body_and_read_databases_and_usage()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNext(new
        {
            clusters = new object[]
            {
                new
                {
                    id = ClusterId,
                    displayName = "Main cluster",
                    atlasClusterName = "nbx-flex-1",
                    norbixRegionCode = "eu-central",
                    tierCode = "FLEX",
                    status = "Active",
                    databases = new object[]
                    {
                        new
                        {
                            projectId = "proj-1",
                            projectName = "Shop",
                            env = "PROD",
                            integrationId = "int_db",
                            integrationName = "Main database",
                            databaseName = "shop_prod",
                        },
                    },
                    usage = new
                    {
                        databaseCount = 1,
                        storageBytes = 4294967296L,
                        connections = 12,
                        operationsPerSecond = 3.5,
                    },
                    nearLimits = NearLimits,
                },
            },
            canCreateNew = false,
            cannotCreateReason = "ClusterLimitReached",
        });

        var response = await fixture.Client.Account.GetFlexClustersAsync(new GetFlexClusters());

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                response!.Clusters,
                response.CanCreateNew,
                response.CannotCreateReason,
            },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Upgrade_puts_the_cluster_id_in_the_path_and_the_tier_in_the_body()
    {
        using var fixture = TokenOnlyClient();
        fixture.RespondNoContentDefault();

        await fixture.Client.Account.UpgradeFlexClusterAsync(new UpgradeFlexCluster
        {
            FlexClusterId = ClusterId,
            TierCode = "M10",
        });

        await Verifier.Verify(fixture.LastRequest, VerifyConfig.VerifySettings);
    }
}
