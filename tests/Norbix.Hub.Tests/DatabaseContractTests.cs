using System.Net;

using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The Database contract changes of the gateway's "last wave"
/// (refactoringV2, 2026-10-05), seen from the Hub client.
/// <list type="bullet">
/// <item>Update many / delete many carry <c>AllRecords</c>. An empty filter
/// <c>{}</c> without it is refused with CM-ERRORS-DATABASE-037.</item>
/// <item>The taxonomy list answers <c>DependencyRefs</c> (id + name, in the
/// order of <c>Dependencies</c>) instead of <c>DependencyNames</c>; an id that
/// no longer resolves keeps its place with no name.</item>
/// <item>A schema trigger carries the env of its copy, and <c>SchemaId</c> is
/// the owning schema id.</item>
/// <item>A saved aggregate lists the collections its pipeline joins.</item>
/// <item>Rename sends only the title (no uniqueness switch).</item>
/// </list>
/// Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class DatabaseContractTests
{
    private static readonly string[] Dependencies = ["tax-sizes", "tax-gone"];
    private static readonly string[] Joined = ["customers"];

    private static NorbixTestFixture Client() =>
        NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });

    private static VerifySettings Named(string name)
    {
        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"DatabaseContractTests.{name}");
        return settings;
    }

    [Test]
    public async Task Update_many_with_all_records_sends_the_flag_in_the_body()
    {
        using var fixture = Client();
        fixture.RespondNoContentDefault();

        await fixture.Client.Database.UpdateManyRecordsAsync(
            new UpdateManyRecords
            {
                CollectionName = "orders",
                Filter = "{}",
                AllRecords = true,
                Update = "{\"status\":\"archived\"}",
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("UpdateManyAllRecords"));
    }

    [Test]
    public async Task Delete_many_with_all_records_sends_the_flag_in_the_query()
    {
        using var fixture = Client();
        fixture.RespondNoContentDefault();

        await fixture.Client.Database.DeleteManyRecordsAsync(
            new DeleteManyRecords
            {
                CollectionName = "orders",
                Filter = "{}",
                AllRecords = true,
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("DeleteManyAllRecords"));
    }

    /// <summary>
    /// Without the flag the SDK sends no <c>allRecords</c> at all, and the
    /// gateway's refusal reaches the caller with its own code and message.
    /// </summary>
    [Test]
    public async Task Delete_many_with_an_empty_filter_and_no_flag_surfaces_DATABASE_037()
    {
        using var fixture = Client();
        fixture.RespondNext(
            new
            {
                responseStatus = new
                {
                    isSuccess = false,
                    errors = new[]
                    {
                        new
                        {
                            message = "An empty filter matches every record. Set allRecords to true to confirm.",
                            errorCode = "CM-ERRORS-DATABASE-037",
                            fieldName = "filter",
                        },
                    },
                },
            },
            HttpStatusCode.BadRequest
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.DeleteManyRecordsAsync(
                    new DeleteManyRecords { CollectionName = "orders", Filter = "{}" }
                )
        )!;

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                ex.HttpStatus,
                ex.ErrorCode,
                ex.Message,
            },
            Named("DeleteManyEmptyFilterRefused")
        );
    }

    [Test]
    public async Task Taxonomy_list_reads_dependency_refs_in_order_with_an_unresolved_id()
    {
        using var fixture = Client();
        fixture.RespondNext(
            new
            {
                list = new
                {
                    items = new[]
                    {
                        new
                        {
                            viewId = "tax-colors",
                            taxonomyName = "Colors",
                            taxonomySlug = "colors",
                            dependencies = Dependencies,
                            dependencyRefs = new object[]
                            {
                                new { id = "tax-sizes", name = "Sizes" },
                                new { id = "tax-gone", name = (string?)null },
                            },
                        },
                    },
                },
            }
        );

        var response = await fixture.Client.Database.GetDatabaseTaxonomiesAsync(
            new GetDatabaseTaxonomies()
        );

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, Taxonomies = response!.List!.Items },
            Named("TaxonomyDependencyRefs")
        );
    }

    [Test]
    public async Task Schema_trigger_reads_the_owning_schema_id_and_the_env()
    {
        using var fixture = Client();
        fixture.RespondNext(
            new
            {
                trigger = new
                {
                    viewId = "trg_1",
                    name = "On order created",
                    schemaId = "sch_orders",
                    env = "test",
                    isEnabled = true,
                },
            }
        );

        var response = await fixture.Client.Database.GetSchemaTriggerAsync(
            new GetSchemaTrigger { Id = "trg_1", Env = "test" }
        );

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                response!.Trigger!.ViewId,
                response.Trigger.SchemaId,
                response.Trigger.Env,
            },
            Named("SchemaTriggerEnv")
        );
    }

    [Test]
    public async Task Schema_trigger_list_rows_carry_the_env()
    {
        using var fixture = Client();
        fixture.RespondNext(
            new
            {
                list = new
                {
                    items = new[]
                    {
                        new { viewId = "trg_1", name = "On order created", env = "test" },
                    },
                },
            }
        );

        var response = await fixture.Client.Database.GetSchemaTriggersAsync(
            new GetSchemaTriggers { Env = "test" }
        );

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                Rows = response!.List!.Items!.Select(r => new { r.ViewId, r.Env }),
            },
            Named("SchemaTriggerListEnv")
        );
    }

    [Test]
    public async Task Saved_aggregate_reads_the_joined_collections()
    {
        using var fixture = Client();
        fixture.RespondNext(
            new
            {
                item = new
                {
                    viewId = "agg_1",
                    displayName = "Orders with customers",
                    schemaViewId = "sch_orders",
                    pipeline = "[{\"$lookup\":{\"from\":\"customers\"}}]",
                    joinedCollections = Joined,
                },
            }
        );

        var response = await fixture.Client.Database.GetDatabaseAggregateAsync(
            new GetDatabaseAggregate { Id = "agg_1", SchemaId = "sch_orders" }
        );

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response!.Item!.JoinedCollections },
            Named("AggregateJoinedCollections")
        );
    }

    [Test]
    public async Task Rename_schema_sends_only_the_title()
    {
        using var fixture = Client();
        fixture.RespondNoContentDefault();

        await fixture.Client.Database.RenameDatabaseSchemaAsync(
            new RenameDatabaseSchemaRequest { Id = "sch_orders", Title = "Customer Orders" }
        );

        await Verifier.Verify(fixture.LastRequest, Named("RenameSchemaTitleOnly"));
    }
}
