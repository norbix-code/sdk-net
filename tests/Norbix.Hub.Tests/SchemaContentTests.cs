// The fake gateway answers below are literal test data; a static field per array would hide the shape.
#pragma warning disable CA1861

using System.Net;
using System.Text.Json;

using Norbix.Sdk;
using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types;
using Norbix.Sdk.Types.Hub;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Hub.Tests;

/// <summary>
/// The schema-content contract of the gateway (campaign
/// <c>audit/schema-content</c>, 2026-10-07), seen from the Hub client.
/// <list type="bullet">
/// <item>A schema read yields typed fields (nested form, array, JSON, …) by
/// the <c>$fieldType</c> discriminator; an unknown kind is kept, not thrown.</item>
/// <item><c>ExpandReferences</c> on find records / find one record; the
/// refusal CM-ERRORS-DATABASE-056 keeps its code.</item>
/// <item><c>ArrayFilters</c> and dotted paths on update one / update many.</item>
/// <item>A term carries its <c>Slug</c>; a save may send one.</item>
/// <item>A file by id: <c>GET /files/item/by-id</c>.</item>
/// </list>
/// Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class SchemaContentTests
{
    private const string IntegrationId = "22222222-2222-2222-2222-222222222222";

    private static NorbixTestFixture Client() =>
        NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });

    private static VerifySettings Named(string name)
    {
        var settings = VerifyConfig.VerifySettings;
        settings.UseFileName($"SchemaContentTests.{name}");
        return settings;
    }

    private static object Refusal(string code, string message, string? field = null) =>
        new
        {
            responseStatus = new
            {
                isSuccess = false,
                errors = new[]
                {
                    new
                    {
                        message,
                        errorCode = code,
                        fieldName = field,
                    },
                },
            },
        };

    private static Dictionary<string, object?> Field(string kind, string name, params (string Key, object? Value)[] rest)
    {
        var field = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$fieldType"] = kind,
            ["fieldName"] = name,
        };
        foreach (var (key, value) in rest)
        {
            field[key] = value;
        }

        return field;
    }

    private static object Describe(JsonSchemaFieldDto field) =>
        field switch
        {
            ObjectFieldDto o => new
            {
                Kind = nameof(ObjectFieldDto),
                o.FieldName,
                o.Required,
                Properties = o.Properties.Select(Describe).ToArray(),
            },
            ArrayFieldDto a => new
            {
                Kind = nameof(ArrayFieldDto),
                a.FieldName,
                a.MinItems,
                a.MaxItems,
                a.UniqueItems,
                Items = Describe(a.Items),
            },
            UnknownSchemaFieldDto u => new { Kind = nameof(UnknownSchemaFieldDto), u.FieldName, u.FieldType },
            _ => new { Kind = field.GetType().Name, Field = (object)field },
        };

    [Test]
    public async Task Schema_read_yields_typed_fields_with_nesting()
    {
        using var fixture = Client();
        fixture.Respond(
            "/database/schemas/sch_orders",
            new
            {
                item = new
                {
                    id = "sch_orders",
                    dataSchema = new
                    {
                        fields = new object[]
                        {
                            Field("StringFieldDto", "code", ("minLength", 2), ("default", "ab"), ("unique", true)),
                            Field("CurrencyFieldDto", "budget", ("multipleOf", 0.01), ("default", new { value = 10, currency = "EUR" })),
                            Field("FileFieldDto", "photos", ("maxItems", 3), ("allowedFileType", "image/*"), ("maxSizeMb", 2)),
                            Field("TaxonomySelectionFieldDto", "region", ("taxonomyId", "tax_regions"), ("displayField", "slug")),
                            Field("JsonFieldDto", "settings", ("maxBytes", 65536)),
                            Field("ArrayFieldDto", "lines",
                                ("minItems", 1),
                                ("items", Field("ObjectFieldDto", "line",
                                    ("required", new[] { "sku" }),
                                    ("properties", new object[]
                                    {
                                        Field("StringFieldDto", "sku"),
                                        Field("ArrayFieldDto", "serials", ("uniqueItems", true), ("items", Field("StringFieldDto", "serial"))),
                                    })))),
                            Field("ObjectFieldDto", "address",
                                ("properties", new object[] { Field("StringFieldDto", "city") })),
                            Field("HologramFieldDto", "future"),
                        },
                    },
                },
            }
        );

        var response = await fixture.Client.Database.GetDatabaseSchemaAsync(new GetDatabaseSchema { Id = "sch_orders" });

        await Verifier.Verify(
            response!.Item!.DataSchema.Fields.Select(Describe).ToArray(),
            Named("TypedSchemaFields")
        );
    }

    [Test]
    public async Task Find_records_with_expand_references_sends_the_flag_and_reads_id_and_display()
    {
        using var fixture = Client();
        fixture.Respond(
            "/database/collections/orders",
            new
            {
                list = new
                {
                    items = new object[]
                    {
                        new
                        {
                            _id = "ord_1",
                            customer = new { id = "rec_7", display = "Ann Example" },
                            photos = new object[] { new { id = "nbfl_1", display = "sc_e2e/photos/cat.png" } },
                            region = new { id = "trm_1", display = "north" },
                        },
                    },
                },
            }
        );

        var response = await fixture.Client.Database.FindRecordsAsync(
            new FindRecords { CollectionName = "orders", ExpandReferences = true }
        );

        var record = (JsonElement)response!.List!.Items[0];
        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                Customer = ReferenceValue.Read(record, "customer"),
                Photos = ReferenceValue.ReadMany(record.GetProperty("photos")),
                Region = ReferenceValue.Read(record, "region"),
            },
            Named("FindRecordsExpandReferences")
        );
    }

    [Test]
    public async Task Find_one_record_with_expand_references_sends_the_flag()
    {
        using var fixture = Client();
        fixture.Respond(
            "/database/collections/orders/ord_1",
            new { result = new { _id = "ord_1", owner = new { id = "usr_1", display = "ann@example.test" } } }
        );

        var response = await fixture.Client.Database.FindOneRecordAsync(
            new FindOneRecord { CollectionName = "orders", Id = "ord_1", ExpandReferences = true }
        );

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                Owner = ReferenceValue.Read((JsonElement)response!.Result!, "owner"),
            },
            Named("FindOneRecordExpandReferences")
        );
    }

    [Test]
    public async Task Expand_references_without_read_right_on_a_source_surfaces_DATABASE_056()
    {
        using var fixture = Client();
        fixture.RespondNext(
            Refusal(
                "CM-ERRORS-DATABASE-056",
                "Reference expansion refused: no read permission on taxonomy 'regions' (fields: region).",
                "expandReferences"
            ),
            HttpStatusCode.Forbidden
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.FindRecordsAsync(
                    new FindRecords { CollectionName = "orders", ExpandReferences = true }
                )
        )!;

        await Verifier.Verify(
            new { ex.HttpStatus, ex.ErrorCode, ex.Message },
            Named("ExpandReferencesRefused")
        );
    }

    [Test]
    public async Task Update_one_record_with_a_dotted_path_and_array_filters_sends_both()
    {
        using var fixture = Client();
        fixture.RespondNoContentDefault();

        await fixture.Client.Database.UpdateOneRecordAsync(
            new UpdateOneRecord
            {
                CollectionName = "orders",
                Id = "ord_1",
                Update = "{\"address.city\":\"Kaunas\",\"lines.2.qty\":3,\"lines.$[line].qty\":4}",
                ArrayFilters = "[{\"line.sku\":\"A-1\"}]",
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("UpdateOneRecordArrayFilters"));
    }

    [Test]
    public async Task Update_many_records_with_every_element_and_array_filters_sends_both()
    {
        using var fixture = Client();
        fixture.RespondNoContentDefault();

        await fixture.Client.Database.UpdateManyRecordsAsync(
            new UpdateManyRecords
            {
                CollectionName = "orders",
                Filter = "{\"address.city\":\"Vilnius\"}",
                Update = "{\"lines.$[].checked\":true,\"lines.$[big].flag\":\"bulk\"}",
                ArrayFilters = "[{\"big.qty\":{\"$gte\":10}}]",
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("UpdateManyRecordsArrayFilters"));
    }

    [Test]
    public async Task Overlapping_update_paths_surface_DATABASE_014()
    {
        using var fixture = Client();
        fixture.RespondNext(
            Refusal(
                "CM-ERRORS-PROPERTY-002",
                "Property 'Update' is invalid: CM-ERRORS-DATABASE-014 paths 'address' and 'address.city' overlap — set the whole value or its members, not both",
                "update"
            ),
            HttpStatusCode.BadRequest
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.UpdateOneRecordAsync(
                    new UpdateOneRecord
                    {
                        CollectionName = "orders",
                        Id = "ord_1",
                        Update = "{\"address\":{\"city\":\"Kaunas\"},\"address.city\":\"Vilnius\"}",
                    }
                )
        )!;

        await Verifier.Verify(
            new { ex.HttpStatus, ex.ErrorCode, ex.Message },
            Named("OverlappingPathsRefused")
        );
    }

    [Test]
    public async Task Term_read_carries_the_slug()
    {
        using var fixture = Client();
        fixture.Respond(
            "/database/taxonomies/tax_regions/terms/trm_1",
            new { item = new { id = "trm_1", taxonomyId = "tax_regions", name = "Côte d'Ivoire", slug = "cote-d-ivoire" } }
        );

        var response = await fixture.Client.Database.GetDatabaseTaxonomyTermAsync(
            new GetDatabaseTaxonomyTermRequest { TaxonomyId = "tax_regions", Id = "trm_1" }
        );

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, response?.Item?.Name, response?.Item?.Slug },
            Named("TermSlug")
        );
    }

    [Test]
    public async Task Term_save_with_an_explicit_slug_sends_it_in_the_document()
    {
        using var fixture = Client();
        fixture.Respond("/database/taxonomies/tax_regions/terms", new { id = "trm_2" });

        await fixture.Client.Database.SaveDatabaseTaxonomyTermAsync(
            new SaveDatabaseTaxonomyTermRequest
            {
                TaxonomyId = "tax_regions",
                Document = "{\"name\":\"Springfield\",\"slug\":\"springfield-il\",\"order\":1}",
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("TermSaveSlug"));
    }

    [Test]
    public async Task Term_save_with_a_taken_slug_surfaces_TAXONOMIES_012()
    {
        using var fixture = Client();
        fixture.RespondNext(
            Refusal(
                "CM-ERRORS-TAXONOMIES-012",
                "Slug 'springfield' is already used by term 'trm_1' of this taxonomy.",
                "document"
            ),
            HttpStatusCode.BadRequest
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.SaveDatabaseTaxonomyTermAsync(
                    new SaveDatabaseTaxonomyTermRequest
                    {
                        TaxonomyId = "tax_regions",
                        Document = "{\"name\":\"Springfield\",\"slug\":\"springfield\"}",
                    }
                )
        )!;

        await Verifier.Verify(
            new { ex.HttpStatus, ex.ErrorCode, ex.Message },
            Named("TermSlugTakenRefused")
        );
    }

    [Test]
    public async Task Get_file_by_id_sends_the_integration_and_the_id_in_the_query()
    {
        using var fixture = Client();
        fixture.Respond(
            "/files/item/by-id",
            new
            {
                file = new { integrationId = IntegrationId, path = "sc_e2e/photos/cat.png", isPublic = false },
                isPublic = false,
            }
        );

        var response = await fixture.Client.Files.GetFileByIdAsync(
            new GetFileById { FilesIntegrationId = IntegrationId, Id = "nbfl_0f8fad5b-d9cb-469f-a165-70867728950e" }
        );

        await Verifier.Verify(
            new { Sent = fixture.LastRequest, FilePath = response?.File?.Path, response?.IsPublic },
            Named("GetFileById")
        );
    }
}
