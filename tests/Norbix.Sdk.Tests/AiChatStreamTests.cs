using System.Net;
using System.Text;
using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Norbix.Sdk.Tests;

/// <summary>
/// The end-user AI chat stream, <see cref="NorbixApiClient.AiChatAsync"/> (the twin
/// of TypeScript <c>norbix.aiChat</c>), against a fake SSE server: the request it
/// opens (API host, <c>/event-stream</c>, the channel query, the headers), the frame
/// parsing (comments, a notice that is not an envelope, CRLF, a data field over two
/// lines, event / id fields), the reconnect with <c>Last-Event-Id</c>, a refusal
/// that is never retried, and cancellation. Nothing leaves the process.
/// </summary>
[TestFixture]
public sealed class AiChatStreamTests
{
    private static string Envelope(string eventName, string created, string payload) =>
        "{\"channel\":\"ai-chat\",\"eventName\":\"" + eventName + "\",\"projectId\":\"test-project\"," +
        "\"createdAtUtc\":\"" + created + "\",\"payload\":" + payload + "}";

    private static readonly string Started = Envelope("ai.chat.turn.started", "2026-11-18T10:00:00Z", "{\"turnId\":\"trn_1\"}");
    private static readonly string Token1 = Envelope("ai.chat.turn.token", "2026-11-18T10:00:01Z", "{\"turnId\":\"trn_1\",\"text\":\"Your order \"}");
    private static readonly string Token2 = Envelope("ai.chat.turn.token", "2026-11-18T10:00:02Z", "{\"turnId\":\"trn_1\",\"text\":\"has shipped.\"}");
    private static readonly string Completed = Envelope("ai.chat.turn.completed", "2026-11-18T10:00:03Z", "{\"turnId\":\"trn_1\"}");

    // The second token's JSON is split over two data lines between two members
    // (the joined "\n" is JSON whitespace) and uses CRLF line ends.
    private static readonly int Split = Token2.IndexOf("\"createdAtUtc\"", StringComparison.Ordinal);

    private static readonly string FirstHalf =
        ": heartbeat\n\n" +
        "event: cmd.onConnect\ndata: {\"id\":\"sub_1\"}\n\n" +
        "id: 1\nevent: cmd.ai.chat.turn.started\ndata: " + Started + "\n\n" +
        "id: 2\nevent: cmd.ai.chat.turn.token\ndata: " + Token1 + "\n\n";

    private static readonly string SecondHalf =
        "id: 3\r\nevent: cmd.ai.chat.turn.token\r\ndata: " + Token2[..Split] + "\r\ndata: " + Token2[Split..] + "\r\n\r\n" +
        ":ping\n\n" +
        "id: 4\nevent:cmd.ai.chat.turn.completed\ndata:" + Completed + "\n\n";

    private static NorbixApiClient Client(HttpMessageHandler handler) =>
        new(new NorbixClientOptions
        {
            ApiKey = "test-api-key",
            ProjectId = "test-project",
            ApiBaseUrl = "https://api.test",
            HubBaseUrl = "https://hub.invalid",
            ApiVersion = "v2",
            HubVersion = "v2",
        }, handler);

    private sealed record Seen(string EventName, string? FrameId, string? TurnId, string? Text);

    private static List<Seen> Shape(IEnumerable<AiChatEvent> events) => events.Select(e => new Seen(
        e.EventName,
        e.FrameId,
        e.Payload?.GetProperty("turnId").GetString(),
        e.Payload is { } p && p.TryGetProperty("text", out var t) ? t.GetString() : null)).ToList();

    [Test]
    public async Task Opens_the_callers_channel_on_the_api_host_and_parses_the_turn()
    {
        var handler = new SseHandler(_ => SseHandler.Text(FirstHalf + SecondHalf));
        using var client = Client(handler);

        var events = new List<AiChatEvent>();
        await foreach (var e in client.AiChatAsync("usr_1", new AiChatStreamOptions { Reconnect = false }))
        {
            events.Add(e);
        }

        await Verifier.Verify(
            new { Sent = handler.Requests, Events = Shape(events), Text = string.Concat(events.Select(e => e.Payload is { } p && p.TryGetProperty("text", out var t) ? t.GetString() : null)) },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task A_dropped_stream_is_reopened_with_Last_Event_Id()
    {
        var call = 0;
        var handler = new SseHandler(_ => SseHandler.Text(++call == 1 ? FirstHalf : SecondHalf));
        using var client = Client(handler);

        var events = new List<AiChatEvent>();
        var options = new AiChatStreamOptions { BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(5) };
        await foreach (var e in client.AiChatAsync("usr_1", options))
        {
            events.Add(e);
            if (e.EventName == "ai.chat.turn.completed")
            {
                break;
            }
        }

        await Verifier.Verify(new { Sent = handler.Requests, Events = Shape(events) }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task A_refusal_is_thrown_at_once_and_never_retried()
    {
        var handler = new SseHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(
                "{\"responseStatus\":{\"errorCode\":\"AiChatChannelRefused\",\"message\":\"Not your channel.\"}}",
                Encoding.UTF8,
                "application/json"),
        });
        using var client = Client(handler);

        NorbixException? refused = null;
        try
        {
            await foreach (var _ in client.AiChatAsync("usr_other"))
            {
            }
        }
        catch (NorbixException ex)
        {
            refused = ex;
        }

        await Verifier.Verify(
            new { refused?.StatusCode, refused?.Code, refused?.Message, Calls = handler.Requests.Count },
            VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task Cancellation_ends_an_open_stream()
    {
        var handler = new SseHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new HangingStream(Encoding.UTF8.GetBytes("id: 1\ndata: " + Started + "\n\n"))),
        });
        using var client = Client(handler);
        using var cts = new CancellationTokenSource();

        var events = new List<AiChatEvent>();
        string? ended = null;
        try
        {
            await foreach (var e in client.AiChatAsync("usr_1", cancellationToken: cts.Token))
            {
                events.Add(e);
                await cts.CancelAsync();
            }
        }
        catch (OperationCanceledException ex)
        {
            ended = ex is TaskCanceledException ? nameof(OperationCanceledException) : ex.GetType().Name;
        }

        await Verifier.Verify(new { Events = Shape(events), Ended = ended, Calls = handler.Requests.Count }, VerifyConfig.VerifySettings);
    }

    [Test]
    public async Task An_empty_auth_id_is_refused_before_anything_is_sent()
    {
        var handler = new SseHandler(_ => SseHandler.Text(FirstHalf));
        using var client = Client(handler);

        string? thrown = null;
        try
        {
            await foreach (var _ in client.AiChatAsync(""))
            {
            }
        }
        catch (ArgumentException ex)
        {
            thrown = ex.GetType().Name;
        }

        await Verifier.Verify(new { Thrown = thrown, Calls = handler.Requests.Count, Channel = AiChatChannel.Of("pr_x", "usr_y") }, VerifyConfig.VerifySettings);
    }

    /// <summary>Records each request (host, path, query and the stream headers) and answers with <c>respond</c>.</summary>
    private sealed class SseHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<object> Requests { get; } = [];

        public static HttpResponseMessage Text(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var v) ? string.Join(",", v) : null;
            Requests.Add(new
            {
                request.Method.Method,
                request.RequestUri!.Host,
                request.RequestUri.AbsolutePath,
                Query = request.RequestUri.GetComponents(UriComponents.Query, UriFormat.UriEscaped),
                Accept = Header("Accept"),
                CacheControl = Header("Cache-Control"),
                Authorization = Header("Authorization"),
                LastEventId = Header("Last-Event-Id"),
                ProjectId = Header("norbix-project-id"),
                HasBody = request.Content is not null,
            });
            return Task.FromResult(respond(request));
        }
    }

    /// <summary>A response body that sends its bytes and then stays open until cancelled.</summary>
    private sealed class HangingStream(byte[] first) : Stream
    {
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_offset < first.Length)
            {
                var n = Math.Min(buffer.Length, first.Length - _offset);
                first.AsMemory(_offset, n).CopyTo(buffer);
                _offset += n;
                return n;
            }
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }
    }
}
