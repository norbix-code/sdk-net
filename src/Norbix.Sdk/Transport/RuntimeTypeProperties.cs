using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Norbix.Sdk.Transport;

/// <summary>
/// Writes a nested request DTO as the type it really is, not the type the
/// property is declared as.
/// <para>
/// Several gateway bodies are polymorphic below the top level: the scheduler
/// save body carries <c>task</c> (declared <c>SchedulerTaskRequest</c>), and
/// that task carries <c>campaign</c> (declared <c>EmailCampaignRequest</c>,
/// one subclass per audience). System.Text.Json writes a property by its
/// declared type, so a subclass lost its own fields (<c>rolesNames</c>,
/// <c>userRecipients</c>, …) — and, because the generated subclasses
/// redeclare the discriminator (<c>source</c>), the base slot was written
/// instead: a "specified users" campaign went out as <c>source: allUsers</c>.
/// The transport already wrote the request's own top-level properties by
/// their runtime type; this modifier does the same at every depth.
/// </para>
/// Only properties whose declared type is a non-sealed DTO class from the
/// SDK's type namespaces are touched. Collections of a base type are not
/// covered (no gateway body needs that today).
/// </summary>
internal static class RuntimeTypeProperties
{
    private const string DtoNamespace = "Norbix.Sdk.Types";

    public static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        foreach (var property in typeInfo.Properties)
        {
            var declared = property.PropertyType;
            if (property.CustomConverter is not null) continue;
            if (!declared.IsClass || declared.IsSealed || declared == typeof(string)) continue;
            if (declared.Namespace is null
                || !declared.Namespace.StartsWith(DtoNamespace, StringComparison.Ordinal)) continue;

            property.CustomConverter = (JsonConverter)Activator.CreateInstance(
                typeof(RuntimeTypeConverter<>).MakeGenericType(declared))!;
        }
    }

    private sealed class RuntimeTypeConverter<T> : JsonConverter<T>
        where T : class
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<T>(ref reader, options);

        // The converter sits on the property, not on the type, so serializing
        // the runtime type here uses the plain object contract — no recursion.
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
