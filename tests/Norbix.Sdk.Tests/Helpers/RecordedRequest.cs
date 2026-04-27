using System.Net.Http;
using System.Text.Json;

namespace Norbix.Sdk.Tests.Helpers;

/// <summary>
/// Snapshot-friendly view of an <see cref="HttpRequestMessage"/>. POCO so
/// Verify can serialize it deterministically and tests don't need to know
/// about HttpClient at all.
/// </summary>
public sealed record RecordedRequest(
    string Method,
    string Path,
    string? Query,
    IReadOnlyDictionary<string, string> Headers,
    object? Body);

internal static class RecordedRequestExtensions
{
    private static readonly string[] InterestingHeaders =
    {
        "Authorization",
        "X-CM-ProjectId",
        "X-CM-AccountId",
        "Content-Type",
    };

    public static RecordedRequest ToRecorded(this HttpRequestMessage req)
    {
        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in InterestingHeaders)
        {
            if (req.Headers.TryGetValues(name, out var values))
            {
                headers[name] = string.Join(", ", values);
            }
            if (req.Content?.Headers.TryGetValues(name, out var contentValues) == true)
            {
                headers[name] = string.Join(", ", contentValues);
            }
        }

        object? body = null;
        if (req.Content is not null)
        {
            var raw = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(raw))
            {
                try
                {
                    body = ToSnapshotValue(JsonSerializer.Deserialize<JsonElement>(raw));
                }
                catch
                {
                    body = raw;
                }
            }
        }

        return new RecordedRequest(
            Method: req.Method.Method,
            Path: req.RequestUri?.AbsolutePath ?? "",
            Query: string.IsNullOrEmpty(req.RequestUri?.Query) ? null : req.RequestUri!.Query,
            Headers: headers,
            Body: body);
    }

    private static object? ToSnapshotValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ToSnapshotValue(p.Value), StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray().Select(ToSnapshotValue).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString(),
        };
    }
}
