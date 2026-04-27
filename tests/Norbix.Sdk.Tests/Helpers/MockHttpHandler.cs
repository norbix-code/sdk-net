using System.Net;
using System.Net.Http;

namespace Norbix.Sdk.Tests.Helpers;

/// <summary>
/// Internal-to-tests handler that records every outgoing request and returns
/// a configured response. Tests never see this directly — they go through
/// <see cref="NorbixTestFixture"/>.
/// </summary>
internal sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _queuedResponses = new();
    private readonly List<RequestResponder> _responders = new();
    public List<RecordedRequest> Requests { get; } = new();

    /// <summary>
    /// Register a JSON response for any request whose path ends with <paramref name="pathSuffix"/>.
    /// Last one registered wins (so tests can override defaults set by the fixture).
    /// </summary>
    public MockHttpHandler RespondJson(
        string pathSuffix,
        object body,
        HttpStatusCode status = HttpStatusCode.OK
    )
    {
        _responders.Insert(
            0,
            new RequestResponder
            {
                Match = req =>
                    req.RequestUri?.AbsolutePath.EndsWith(pathSuffix, StringComparison.Ordinal)
                    == true,
                Build = () => Json(body, status),
            }
        );
        return this;
    }

    /// <summary>Default fallback — returns 200 with empty JSON.</summary>
    public MockHttpHandler RespondJsonDefault(object body)
    {
        _responders.Add(
            new RequestResponder { Match = _ => true, Build = () => Json(body, HttpStatusCode.OK) }
        );
        return this;
    }

    /// <summary>Default fallback — returns 204 so callers skip response deserialization.</summary>
    public MockHttpHandler RespondNoContentDefault()
    {
        _responders.Insert(
            0,
            new RequestResponder
            {
                Match = _ => true,
                Build = () => new HttpResponseMessage(HttpStatusCode.NoContent),
            }
        );
        return this;
    }

    /// <summary>Return this JSON body for the next request, regardless of URL.</summary>
    public MockHttpHandler RespondJsonNext(object? body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _queuedResponses.Enqueue(() => Json(body, status));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (request.Content is not null)
        {
            // Buffer so test code can read the body after the call returns.
            await request.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
        }
        Requests.Add(request.ToRecorded());

        if (_queuedResponses.Count > 0)
        {
            return _queuedResponses.Dequeue()();
        }

        foreach (var r in _responders)
        {
            if (r.Match(request))
                return r.Build();
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage Json(object? body, HttpStatusCode status)
    {
        var json = body is null ? "" : System.Text.Json.JsonSerializer.Serialize(body);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private sealed class RequestResponder
    {
        public required Func<HttpRequestMessage, bool> Match { get; init; }
        public required Func<HttpResponseMessage> Build { get; init; }
    }
}
