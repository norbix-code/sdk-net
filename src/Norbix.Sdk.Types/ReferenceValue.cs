#nullable enable

using System.Text.Json;

namespace Norbix.Sdk.Types;

/// <summary>
/// A reference field of a record read with <c>ExpandReferences = true</c>:
/// the gateway replaces the stored id with <c>{ "id": …, "display": … }</c>,
/// where <c>display</c> is the target's display field per the schema (a user's
/// name, a term's title or slug, a role's name, a record's display field, a
/// file's path) and <c>null</c> when the target is gone.
/// <para>
/// Records are extended-JSON documents (the SDK reads them as
/// <see cref="JsonElement"/>), so this is the one typed piece: use
/// <see cref="TryRead"/> on a field, <see cref="ReadMany"/> on a
/// <c>multiple</c> field, or <see cref="Read(JsonElement, string)"/> with the
/// field's path (dotted for a nested form, e.g. <c>customer.address.region</c>).
/// </para>
/// </summary>
public sealed class ReferenceValue
{
    /// <summary>The stored id of the target (user, role, term, record or file).</summary>
    public required string Id { get; init; }

    /// <summary>
    /// The display value when the gateway sent a string; <c>null</c> when the
    /// target is gone or when the display is not a string (see <see cref="RawDisplay"/>).
    /// </summary>
    public string? Display { get; init; }

    /// <summary>
    /// The display element as sent, for a display that is not a plain string
    /// (a language → text map, for example). <c>null</c> when absent or JSON null.
    /// </summary>
    public JsonElement? RawDisplay { get; init; }

    /// <summary>Reads one expanded value out of a <c>{ id, display }</c> object.</summary>
    /// <returns><c>false</c> when the element is not such an object (for example the plain id, when the read did not expand).</returns>
    public static bool TryRead(JsonElement element, out ReferenceValue? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? display = null;
        JsonElement? raw = null;
        if (element.TryGetProperty("display", out var d) && d.ValueKind != JsonValueKind.Null)
        {
            if (d.ValueKind == JsonValueKind.String)
            {
                display = d.GetString();
            }
            else
            {
                raw = d.Clone();
            }
        }

        value = new ReferenceValue { Id = id.GetString()!, Display = display, RawDisplay = raw };
        return true;
    }

    /// <summary>
    /// Reads the expanded values of a <c>multiple</c> reference field: an array
    /// of <c>{ id, display }</c>. A single object gives a list of one; anything
    /// else (the plain ids, null) gives an empty list.
    /// </summary>
    public static IReadOnlyList<ReferenceValue> ReadMany(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<ReferenceValue>();
            foreach (var item in element.EnumerateArray())
            {
                if (TryRead(item, out var one))
                {
                    list.Add(one!);
                }
            }

            return list;
        }

        return TryRead(element, out var single) ? [single!] : [];
    }

    /// <summary>
    /// Reads the expanded value of the field at <paramref name="path"/> in a
    /// record. The path is dotted for a nested form (<c>customer.address.region</c>);
    /// an array index is a number segment (<c>lines.1.product</c>).
    /// </summary>
    /// <returns><c>null</c> when the path does not exist or does not hold an expanded value.</returns>
    public static ReferenceValue? Read(JsonElement record, string path)
    {
        var current = record;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index))
            {
                if (index < 0 || index >= current.GetArrayLength())
                {
                    return null;
                }

                current = current[index];
                continue;
            }

            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return TryRead(current, out var value) ? value : null;
    }
}
