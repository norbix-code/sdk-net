// The fake gateway answers below are literal test data; a static field per array would hide the shape.
#pragma warning disable CA1861

using System.Net;
using System.Text.Json;

using Norbix.Sdk.Tests.Helpers;
using Norbix.Sdk.Types;
using Norbix.Sdk.Types.Api;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// The schema-content contract of the gateway (campaign
/// <c>audit/schema-content</c>, 2026-10-07), seen from the public Api client.
/// <list type="bullet">
/// <item>A schema read yields typed fields: the <c>$fieldType</c> discriminator
/// picks <c>StringFieldDto</c>, <c>ObjectFieldDto</c> (nested form),
/// <c>ArrayFieldDto</c>, <c>JsonFieldDto</c>, … and an unknown kind lands in
/// <c>UnknownSchemaFieldDto</c>.</item>
/// <item><c>ExpandReferences</c> on find / find one / find own turns every
/// reference into <c>{ id, display }</c>; <see cref="ReferenceValue"/> reads
/// it. A missing read right on a linked source is CM-ERRORS-DATABASE-056.</item>
/// <item>Nested documents: dotted update paths, <c>$[]</c> / <c>$[name]</c>
/// with <c>ArrayFilters</c>, nested filters and a dotted <c>SortBy</c> travel
/// as sent; the record refusals keep their code.</item>
/// <item>A file is addressed by its stored id:
/// <c>GET /files/{filesIntegrationId}/by-id/{id}</c>.</item>
/// </list>
/// Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class SchemaContentTests
{
    private const string IntegrationId = "11111111-1111-1111-1111-111111111111";

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

    /// <summary>A display that is not a string (a language map) shows as its JSON text.</summary>
    private static object? Raw(ReferenceValue? value) =>
        value is null ? null : new { value.Id, value.Display, RawDisplay = value.RawDisplay?.GetRawText() };

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

    // ----- typed schema fields ------------------------------------------------

    [Test]
    public async Task Schema_read_yields_one_typed_field_per_kind_including_nested_forms()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/database/schemas/sch_orders",
            new
            {
                item = new
                {
                    id = "sch_orders",
                    collectionName = "orders",
                    dataSchema = new
                    {
                        fields = new object[]
                        {
                            Field("StringFieldDto", "code", ("pattern", "^[a-z]{3}$"), ("default", "abc"), ("unique", true)),
                            Field("DecimalFieldDto", "price", ("multipleOf", 0.01), ("default", 9.99)),
                            Field("CurrencyFieldDto", "budget",
                                ("allowedCurrencies", new[] { "EUR", "USD" }),
                                ("multipleOf", 0.01),
                                ("minimum", 0),
                                ("maximum", 1000),
                                ("default", new { value = 10, currency = "EUR" })),
                            Field("BooleanFieldDto", "paid", ("default", false)),
                            Field("DateFieldDto", "due", ("minimum", 16777216L), ("default", 20000000L)),
                            Field("IntegerFieldDto", "qty", ("minimum", 1), ("maximum", 10), ("default", 1), ("unique", false)),
                            Field("GeolocationFieldDto", "where", ("allowedTypes", new[] { "Point" })),
                            Field("TagsFieldDto", "tags", ("minItems", 1), ("maxItems", 5), ("default", new[] { "new" })),
                            Field("FileFieldDto", "photos",
                                ("storages", new[] { IntegrationId }),
                                ("minItems", 0),
                                ("maxItems", 3),
                                ("allowedFileType", "image/*"),
                                ("maxSizeMb", 2.5)),
                            Field("TaxonomySelectionFieldDto", "region", ("taxonomyId", "tax_regions"), ("multiple", false), ("displayField", "slug")),
                            Field("CollectionSelectionFieldDto", "customer", ("collectionId", "sch_customers"), ("displayField", "name"), ("multiple", false)),
                            Field("UserSelectionFieldDto", "owner", ("multiple", false), ("displayField", "email")),
                            Field("RoleSelectionFieldDto", "visibleTo", ("multiple", true), ("displayField", "name")),
                            Field("EnumSelectionFieldDto", "status", ("values", new[] { "open", "closed" }), ("multiple", false), ("default", new[] { "open" })),
                            Field("JsonFieldDto", "settings", ("maxBytes", 65536)),
                            Field("ArrayFieldDto", "lines",
                                ("minItems", 1),
                                ("maxItems", 20),
                                ("uniqueItems", false),
                                ("items", Field("ObjectFieldDto", "line",
                                    ("required", new[] { "sku" }),
                                    ("properties", new object[]
                                    {
                                        Field("StringFieldDto", "sku"),
                                        Field("IntegerFieldDto", "qty", ("minimum", 1)),
                                    })))),
                            Field("ObjectFieldDto", "address",
                                ("required", new[] { "city" }),
                                ("properties", new object[]
                                {
                                    Field("StringFieldDto", "city"),
                                    Field("ObjectFieldDto", "geo",
                                        ("properties", new object[] { Field("GeolocationFieldDto", "point") })),
                                })),
                            Field("HologramFieldDto", "future", ("shape", "cube")),
                            new Dictionary<string, object?> { ["fieldName"] = "bare" },
                        },
                    },
                },
            }
        );

        var response = await fixture.Client.Database.GetDatabaseSchemaAsync(
            new GetDatabaseSchemaRequest { Id = "sch_orders" }
        );

        await Verifier.Verify(
            response!.Item!.DataSchema.Fields.Select(Describe).ToArray(),
            Named("TypedSchemaFields")
        );
    }

    // ----- expand references -------------------------------------------------

    [Test]
    public async Task Find_with_expand_references_sends_the_flag_and_reads_id_and_display()
    {
        using var fixture = NorbixTestFixture.Create();
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
                            owner = new { id = "usr_1", display = (string?)null },
                            visibleTo = new object[]
                            {
                                new { id = "rol_1", display = "Admin" },
                                new { id = "rol_2", display = "Editor" },
                            },
                            region = new { id = "trm_1", display = new { en = "North", lt = "Šiaurė" } },
                            lines = new object[]
                            {
                                new { sku = "A-1", product = new { id = "rec_9", display = "Widget" } },
                            },
                        },
                    },
                    hasMore = false,
                },
            }
        );

        var response = await fixture.Client.Database.FindAsync(
            new FindRequest { CollectionName = "orders", ExpandReferences = true }
        );

        var record = (JsonElement)response!.List!.Items[0];
        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                Customer = ReferenceValue.Read(record, "customer"),
                Owner = ReferenceValue.Read(record, "owner"),
                VisibleTo = ReferenceValue.ReadMany(record.GetProperty("visibleTo")),
                Region = Raw(ReferenceValue.Read(record, "region")),
                LineProduct = ReferenceValue.Read(record, "lines.0.product"),
                Missing = ReferenceValue.Read(record, "lines.3.product"),
                PlainId = ReferenceValue.TryRead(record.GetProperty("_id"), out _),
            },
            Named("FindExpandReferences")
        );
    }

    [Test]
    public async Task Find_one_with_expand_references_sends_the_flag()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/database/collections/orders/ord_1",
            new { result = new { _id = "ord_1", customer = new { id = "rec_7", display = "Ann Example" } } }
        );

        var response = await fixture.Client.Database.FindOneAsync(
            new FindOneRequest { CollectionName = "orders", Id = "ord_1", ExpandReferences = true }
        );

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                Customer = ReferenceValue.Read((JsonElement)response!.Result!, "customer"),
            },
            Named("FindOneExpandReferences")
        );
    }

    [Test]
    public async Task Find_own_with_expand_references_sends_the_flag()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/database/collections/orders/own", new { list = new { items = Array.Empty<object>() } });

        await fixture.Client.Database.FindOwnAsync(
            new FindOwnRequest { CollectionName = "orders", ExpandReferences = true }
        );

        await Verifier.Verify(fixture.LastRequest, Named("FindOwnExpandReferences"));
    }

    [Test]
    public async Task Find_without_the_flag_does_not_send_it_and_keeps_the_ids()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/database/collections/orders",
            new { list = new { items = new object[] { new { _id = "ord_1", customer = "rec_7" } } } }
        );

        var response = await fixture.Client.Database.FindAsync(new FindRequest { CollectionName = "orders" });

        var record = (JsonElement)response!.List!.Items[0];
        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                Customer = ReferenceValue.Read(record, "customer"),
                CustomerId = record.GetProperty("customer").GetString(),
            },
            Named("FindWithoutExpand")
        );
    }

    [Test]
    public async Task Expand_references_without_read_right_on_a_source_surfaces_DATABASE_056()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(
            Refusal(
                "CM-ERRORS-DATABASE-056",
                "Reference expansion refused: no read permission on users (fields: owner). Missing: db.users.read.",
                "expandReferences"
            ),
            HttpStatusCode.Forbidden
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.FindAsync(
                    new FindRequest { CollectionName = "orders", ExpandReferences = true }
                )
        )!;

        await Verifier.Verify(
            new { ex.HttpStatus, ex.ErrorCode, ex.Message },
            Named("ExpandReferencesRefused")
        );
    }

    // ----- nested documents + array filters ------------------------------------

    [Test]
    public async Task Insert_one_sends_a_nested_document_as_is()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/database/collections/orders", new { id = "ord_1" });

        await fixture.Client.Database.InsertOneAsync(
            new InsertOneRequest
            {
                CollectionName = "orders",
                Document =
                    "{\"customer\":\"rec_7\",\"address\":{\"city\":\"Vilnius\",\"geo\":{\"point\":{\"type\":\"Point\",\"coordinates\":[25.28,54.69]}}},"
                    + "\"lines\":[{\"sku\":\"A-1\",\"qty\":2},{\"sku\":\"B-2\",\"qty\":1}],\"settings\":{\"theme\":{\"dark\":true}}}",
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("InsertNestedDocument"));
    }

    [Test]
    public async Task Update_one_with_a_dotted_path_and_array_filters_sends_both()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Database.UpdateOneAsync(
            new UpdateOneRequest
            {
                CollectionName = "orders",
                Id = "ord_1",
                Update = "{\"address.city\":\"Kaunas\",\"lines.$[line].qty\":3}",
                ArrayFilters = "[{\"line.sku\":\"A-1\"}]",
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("UpdateOneArrayFilters"));
    }

    [Test]
    public async Task Update_many_with_every_element_and_array_filters_sends_both()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNoContentDefault();

        await fixture.Client.Database.UpdateManyAsync(
            new UpdateManyRequest
            {
                CollectionName = "orders",
                Filter = "{\"lines\":{\"$elemMatch\":{\"sku\":\"A-1\",\"qty\":{\"$gte\":2}}}}",
                Update = "{\"lines.$[].checked\":true,\"lines.$[big].flag\":\"bulk\"}",
                ArrayFilters = "[{\"$or\":[{\"big.qty\":{\"$gte\":10}},{\"big.sku\":\"Z-9\"}]}]",
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("UpdateManyArrayFilters"));
    }

    [Test]
    public async Task Find_with_a_nested_filter_and_a_dotted_sort_sends_them_unchanged()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond("/database/collections/orders", new { list = new { items = Array.Empty<object>() } });

        await fixture.Client.Database.FindAsync(
            new FindRequest
            {
                CollectionName = "orders",
                Filter = "{\"address.city\":\"Vilnius\",\"tags.0\":\"vip\"}",
                SortBy = "address.city",
                SortOrder = -1,
            }
        );

        await Verifier.Verify(fixture.LastRequest, Named("FindNestedFilterDottedSort"));
    }

    [Test]
    public async Task Array_filters_that_do_not_match_the_update_paths_surface_DATABASE_014()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(
            Refusal(
                "CM-ERRORS-PROPERTY-002",
                "Property 'ArrayFilters' is invalid: CM-ERRORS-DATABASE-014 identifier 'line' is used in the update but has no filter",
                "arrayFilters"
            ),
            HttpStatusCode.BadRequest
        );

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.UpdateOneAsync(
                    new UpdateOneRequest
                    {
                        CollectionName = "orders",
                        Id = "ord_1",
                        Update = "{\"lines.$[line].qty\":3}",
                        ArrayFilters = "[]",
                    }
                )
        )!;

        await Verifier.Verify(
            new { ex.HttpStatus, ex.ErrorCode, ex.Message },
            Named("ArrayFiltersRefused")
        );
    }

    [TestCase("CM-ERRORS-DATABASE-039", "Field 'lines' is invalid: must be an array", "lines", "WrongType")]
    [TestCase("CM-ERRORS-DATABASE-040", "Field 'lines' is invalid: must have at most 20 items", "lines", "TooManyItems")]
    [TestCase("CM-ERRORS-DATABASE-046", "Field 'tags' is invalid: must not repeat an entry", "tags", "RepeatedEntry")]
    [TestCase("CM-ERRORS-DATABASE-047", "Field 'address' is invalid: 'street' is not a member of the nested form", "address", "UndeclaredMember")]
    [TestCase("CM-ERRORS-DATABASE-053", "Field 'customer' is invalid: record 'rec_404' is not in collection 'customers'", "customer", "ReferenceNotFound")]
    public async Task A_record_refusal_at_depth_keeps_its_code(string code, string message, string field, string snapshot)
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.RespondNext(Refusal(code, message, field), HttpStatusCode.BadRequest);

        var ex = Assert.ThrowsAsync<NorbixException>(
            async () =>
                await fixture.Client.Database.InsertOneAsync(
                    new InsertOneRequest { CollectionName = "orders", Document = "{}" }
                )
        )!;

        await Verifier.Verify(
            new { ex.HttpStatus, ex.ErrorCode, ex.Message },
            Named($"RecordRefused.{snapshot}")
        );
    }

    // ----- files by id ---------------------------------------------------------

    [Test]
    public async Task Get_file_by_id_puts_the_integration_and_the_id_in_the_path()
    {
        using var fixture = NorbixTestFixture.Create();
        fixture.Respond(
            "/by-id/nbfl_0f8fad5b-d9cb-469f-a165-70867728950e",
            new
            {
                file = new { integrationId = IntegrationId, path = "sc_e2e/photos/cat.png", isPublic = true },
                isPublic = true,
                publicUrl = "https://files.example/sc_e2e/photos/cat.png",
            }
        );

        var response = await fixture.Client.Files.GetFileByIdAsync(
            new GetFileByIdRequest
            {
                FilesIntegrationId = IntegrationId,
                Id = "nbfl_0f8fad5b-d9cb-469f-a165-70867728950e",
            }
        );

        await Verifier.Verify(
            new
            {
                Sent = fixture.LastRequest,
                FilePath = response?.File?.Path,
                response?.IsPublic,
                response?.PublicUrl,
            },
            Named("GetFileById")
        );
    }
}
