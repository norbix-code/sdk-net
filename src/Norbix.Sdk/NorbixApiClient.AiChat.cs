using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Norbix.Sdk.Transport;

namespace Norbix.Sdk;

/// <summary>The end-user AI chat channel names (TypeScript twin: <c>aiChatChannel</c>).</summary>
public static class AiChatChannel
{
    /// <summary><c>ai-chat:{projectId}:{authId}</c> — one project user's own chat channel.</summary>
    public static string Of(string projectId, string authId) => $"ai-chat:{projectId}:{authId}";
}

/// <summary>
/// One realtime envelope of the end-user AI chat stream. <see cref="EventName"/> is
/// <c>ai.chat.turn.started</c> / <c>.token</c> / <c>.tool</c> / <c>.completed</c> /
/// <c>.failed</c> or <c>ai.chat.session.*</c>; <see cref="Payload"/> carries the
/// <c>turnId</c> and, for a token, the <c>text</c>. <see cref="FrameId"/> is the SSE
/// frame id (sent back as <c>Last-Event-Id</c> on a reconnect).
/// </summary>
public sealed record AiChatEvent
{
    /// <summary>Channel family of the envelope.</summary>
    [JsonPropertyName("channel")] public string? Channel { get; init; }

    /// <summary>Dotted event name, e.g. <c>ai.chat.turn.token</c>.</summary>
    [JsonPropertyName("eventName")] public string EventName { get; init; } = string.Empty;

    /// <summary>The project the event belongs to.</summary>
    [JsonPropertyName("projectId")] public string? ProjectId { get; init; }

    /// <summary>Set only when the event was also stored in the inbox.</summary>
    [JsonPropertyName("notificationId")] public string? NotificationId { get; init; }

    /// <summary>When the gateway raised the event (UTC, ISO 8601).</summary>
    [JsonPropertyName("createdAtUtc")] public string? CreatedAtUtc { get; init; }

    /// <summary>The event's data (<c>turnId</c>, <c>text</c>, …).</summary>
    [JsonPropertyName("payload")] public JsonElement? Payload { get; init; }

    /// <summary>The SSE frame id.</summary>
    [JsonIgnore] public string? FrameId { get; init; }
}

/// <summary>
/// How <see cref="NorbixApiClient.AiChatAsync"/> reconnects — the same defaults as
/// the TypeScript SDK's SSE client: on, 500 ms base, 10 s cap, full jitter.
/// </summary>
public sealed class AiChatStreamOptions
{
    /// <summary>Reconnect when the stream drops (a network error, a 5xx, a clean
    /// server close). A refusal (401 / 403 / 404) is never retried. Default: true.</summary>
    public bool Reconnect { get; init; } = true;

    /// <summary>First reconnect delay before jitter. Default: 500 ms.</summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Largest reconnect delay. Default: 10 s.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(10);
}

public sealed partial class NorbixApiClient
{
    /// <summary>
    /// Streams the end-user AI chat (TypeScript twin: <c>norbix.aiChat</c>). It
    /// subscribes to the signed-in user's own channel on the API host —
    /// <c>GET /event-stream?channels=ai-chat:{projectId}:{authId}</c> with the
    /// client's token — and yields every chat envelope; heartbeats and
    /// subscription notices are skipped. Start a turn with
    /// <c>Ai.StartEndUserChatTurnAsync</c>; its <c>turnId</c> is in each event's
    /// payload.
    /// <para>
    /// The stream runs until you stop enumerating or
    /// <paramref name="cancellationToken"/> fires (then an
    /// <see cref="OperationCanceledException"/> ends the enumeration). A dropped
    /// stream is reopened with backoff and <c>Last-Event-Id</c>, as in TypeScript;
    /// set <see cref="AiChatStreamOptions.Reconnect"/> to false to end instead.
    /// Another user's channel is refused with HTTP 403 and the code
    /// <c>AiChatChannelRefused</c> before the stream starts: that
    /// <see cref="NorbixException"/> is thrown at once and never retried.
    /// </para>
    /// </summary>
    /// <param name="authId">The signed-in project user's id (the channel owner).</param>
    /// <param name="options">Reconnect policy; null = the TypeScript defaults.</param>
    /// <param name="cancellationToken">Stops the stream.</param>
    public async IAsyncEnumerable<AiChatEvent> AiChatAsync(
        string authId,
        AiChatStreamOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(authId);
        if (string.IsNullOrEmpty(_options.ProjectId))
        {
            throw new InvalidOperationException("AiChatAsync: the client has no ProjectId.");
        }

        options ??= new AiChatStreamOptions();
        var query = new[] { new KeyValuePair<string, string>("channels", AiChatChannel.Of(_options.ProjectId, authId)) };
        string? lastEventId = null;
        var attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Exception? dropped = null;

            var frames = _transport.StreamAsync(NorbixTarget.Api, "/event-stream", query, lastEventId, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            try
            {
                while (true)
                {
                    SseFrame frame;
                    try
                    {
                        if (!await frames.MoveNextAsync().ConfigureAwait(false))
                        {
                            break;
                        }
                        frame = frames.Current;
                    }
                    catch (NorbixException ex) when (ex.StatusCode is 401 or 403 or 404)
                    {
                        throw; // the gateway refused the subscription; retrying repeats it
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex) when (ex is NorbixException or HttpRequestException or IOException)
                    {
                        dropped = ex;
                        break;
                    }

                    attempt = 0;
                    if (frame.Id is not null)
                    {
                        lastEventId = frame.Id;
                    }
                    var envelope = ParseEnvelope(frame);
                    if (envelope is not null)
                    {
                        yield return envelope;
                    }
                }
            }
            finally
            {
                await frames.DisposeAsync().ConfigureAwait(false);
            }

            if (!options.Reconnect)
            {
                if (dropped is not null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(dropped).Throw();
                }
                yield break;
            }

            await Task.Delay(Backoff(options, attempt), cancellationToken).ConfigureAwait(false);
            attempt++;
        }
    }

    private static AiChatEvent? ParseEnvelope(SseFrame frame)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<AiChatEvent>(frame.Data);
            return envelope is null || string.IsNullOrEmpty(envelope.EventName)
                ? null
                : envelope with { FrameId = frame.Id };
        }
        catch (JsonException)
        {
            return null; // not an envelope (a ServiceStack control frame)
        }
    }

    private static TimeSpan Backoff(AiChatStreamOptions options, int attempt)
    {
        var exp = Math.Min(options.MaxDelay.TotalMilliseconds, options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt));
#pragma warning disable CA5394 // jitter, not security
        return TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * exp);
#pragma warning restore CA5394
    }
}
