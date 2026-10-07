#nullable enable annotations
#nullable disable warnings

using System.Text.Json.Serialization;

namespace Norbix.Sdk.Types.Hub;

// Hand-written companions to Generated/Hub.dtos.cs for the data-schema field
// DTOs (`DataSchemaDto.Fields`, `ObjectFieldDto.Properties`,
// `ArrayFieldDto.Items`).
//
// The gateway writes every field with a `$fieldType` discriminator whose value
// is the DTO's class name and reads it back the same way. The export carries
// the 17 subclasses but not the discriminator, so without this file a schema
// read lands every field in the base class and drops its rules. The converter
// below picks the subclass by `$fieldType`; an unknown or missing kind reads as
// `UnknownSchemaFieldDto` (name + raw kind), never a throw.

[JsonConverter(typeof(JsonSchemaFieldDtoJsonConverter))]
public partial class JsonSchemaFieldDto
{
}

/// <summary>
/// A field whose <c>$fieldType</c> this SDK does not know (a newer gateway),
/// or a field sent without one. <see cref="JsonSchemaFieldDto.FieldName"/> is
/// set; <see cref="FieldType"/> keeps the raw discriminator.
/// </summary>
public sealed partial class UnknownSchemaFieldDto : JsonSchemaFieldDto
{
    [JsonPropertyName("$fieldType")]
    public string? FieldType { get; set; }
}

/// <summary>The <c>$fieldType</c> converter for the Hub schema-field DTOs.</summary>
public sealed class JsonSchemaFieldDtoJsonConverter : SchemaFieldJsonConverter<JsonSchemaFieldDto>
{
    private static readonly Dictionary<string, Type> Known = new(StringComparer.Ordinal)
    {
        [nameof(StringFieldDto)] = typeof(StringFieldDto),
        [nameof(DecimalFieldDto)] = typeof(DecimalFieldDto),
        [nameof(CurrencyFieldDto)] = typeof(CurrencyFieldDto),
        [nameof(BooleanFieldDto)] = typeof(BooleanFieldDto),
        [nameof(DateFieldDto)] = typeof(DateFieldDto),
        [nameof(IntegerFieldDto)] = typeof(IntegerFieldDto),
        [nameof(GeolocationFieldDto)] = typeof(GeolocationFieldDto),
        [nameof(TagsFieldDto)] = typeof(TagsFieldDto),
        [nameof(FileFieldDto)] = typeof(FileFieldDto),
        [nameof(TaxonomySelectionFieldDto)] = typeof(TaxonomySelectionFieldDto),
        [nameof(CollectionSelectionFieldDto)] = typeof(CollectionSelectionFieldDto),
        [nameof(UserSelectionFieldDto)] = typeof(UserSelectionFieldDto),
        [nameof(RoleSelectionFieldDto)] = typeof(RoleSelectionFieldDto),
        [nameof(EnumSelectionFieldDto)] = typeof(EnumSelectionFieldDto),
        [nameof(ObjectFieldDto)] = typeof(ObjectFieldDto),
        [nameof(ArrayFieldDto)] = typeof(ArrayFieldDto),
        [nameof(JsonFieldDto)] = typeof(JsonFieldDto),
    };

    protected override IReadOnlyDictionary<string, Type> Kinds => Known;

    protected override Type Fallback => typeof(UnknownSchemaFieldDto);
}
