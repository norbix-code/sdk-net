#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Norbix.Sdk.Types;

/// <summary>
/// Reads and writes a polymorphic schema-field DTO by the gateway's
/// <c>$fieldType</c> discriminator (the value is the DTO's class name:
/// <c>StringFieldDto</c>, <c>ObjectFieldDto</c>, <c>ArrayFieldDto</c>, …).
/// <para>
/// The Api and Hub namespaces each have their own <c>JsonSchemaFieldDto</c>
/// family, so each declares a small subclass that supplies its
/// <see cref="Kinds"/> map and its <see cref="Fallback"/> type. The
/// fallback is used when the discriminator is missing or names a kind this
/// SDK does not know yet: a newer gateway never breaks a schema read, the
/// unknown field just arrives with its name and raw discriminator.
/// </para>
/// </summary>
/// <typeparam name="TBase">The base class of the field DTO family.</typeparam>
public abstract class SchemaFieldJsonConverter<TBase> : JsonConverter<TBase>
    where TBase : class
{
    /// <summary>The discriminator property the gateway writes on every field.</summary>
    public const string Discriminator = "$fieldType";

    /// <summary>Discriminator value → concrete DTO type (a subclass of <typeparamref name="TBase"/>).</summary>
    protected abstract IReadOnlyDictionary<string, Type> Kinds { get; }

    /// <summary>
    /// The type read when the discriminator is missing or unknown. It must be
    /// a subclass of <typeparamref name="TBase"/> that does not carry this
    /// converter itself.
    /// </summary>
    protected abstract Type Fallback { get; }

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(TBase);

    /// <inheritdoc />
    public override TBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var target = Fallback;
        if (
            root.TryGetProperty(Discriminator, out var discriminator)
            && discriminator.ValueKind == JsonValueKind.String
            && Kinds.TryGetValue(discriminator.GetString()!, out var known)
        )
        {
            target = known;
        }

        return (TBase?)root.Deserialize(target, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        var type = value.GetType();
        if (JsonSerializer.SerializeToNode(value, type, options) is not JsonObject node)
        {
            writer.WriteNullValue();
            return;
        }

        string? kind = null;
        foreach (var pair in Kinds)
        {
            if (pair.Value == type)
            {
                kind = pair.Key;
                break;
            }
        }

        if (kind is not null)
        {
            node.Remove(Discriminator);
            node.Insert(0, Discriminator, kind);
        }

        node.WriteTo(writer, options);
    }
}
