using System.Runtime.CompilerServices;
using System.Text;

namespace Norbix.Sdk.Transport;

/// <summary>One server-sent events frame: the <c>event:</c> field (<c>"message"</c>
/// when the frame has none), the <c>data:</c> lines joined with <c>"\n"</c>, and the
/// last <c>id:</c> seen.</summary>
internal readonly record struct SseFrame(string Event, string Data, string? Id);

/// <summary>
/// Minimal SSE wire parser, the twin of the TypeScript SDK's <c>readSse</c>:
/// <c>event:</c>, <c>data:</c> (several lines are joined with <c>"\n"</c>),
/// <c>id:</c>, comment lines (<c>:</c> — ServiceStack heartbeats) are skipped, a
/// blank line ends a frame. A single space after the colon is dropped. CRLF, LF
/// and CR line ends are all accepted. <c>retry:</c> is ignored (the caller owns
/// the reconnect policy).
/// </summary>
internal static class SseReader
{
    public static async IAsyncEnumerable<SseFrame> ReadAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

        var eventName = "message";
        var data = new List<string>();
        string? lastId = null;

        while (true)
        {
            // ReadLineAsync accepts \n, \r\n and \r as line ends.
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                yield break;
            }

            if (line.Length == 0)
            {
                if (data.Count > 0 || eventName != "message")
                {
                    yield return new SseFrame(eventName, string.Join('\n', data), lastId);
                }
                eventName = "message";
                data.Clear();
                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? string.Empty : line[(colon + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    data.Add(value);
                    break;
                case "id":
                    lastId = value;
                    break;
            }
        }
    }
}
