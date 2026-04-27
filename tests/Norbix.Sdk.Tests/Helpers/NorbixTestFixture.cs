using Norbix.Sdk;

namespace Norbix.Sdk.Tests.Helpers;

/// <summary>
/// Test fixture that builds a real <see cref="NorbixClient"/> wired to an
/// in-memory mock handler. The handler is hidden — tests only see the
/// fixture's <see cref="Client"/>, configuration callback, and
/// <see cref="RecordedRequests"/>.
/// </summary>
/// <remarks>
/// Public surface stays clean: tests never touch <c>HttpClient</c> or
/// <c>HttpMessageHandler</c>. The fixture reaches the internal NorbixClient
/// constructor via <c>InternalsVisibleTo("Norbix.Sdk.Tests")</c>.
/// </remarks>
internal sealed class NorbixTestFixture : IDisposable
{
    private readonly MockHttpHandler _handler = new();

    public NorbixClient Client { get; }
    public NorbixClientOptions Options { get; }

    private NorbixTestFixture(NorbixClientOptions options)
    {
        Options = options;
        // Default fallback — endpoints not configured by the test return {}.
        _handler.RespondJsonDefault(new { });
        Client = new NorbixClient(options, _handler);
    }

    /// <summary>
    /// Build a fixture for a typical project-scope client (API key + project,
    /// no account). Override the options inline if a test needs more.
    /// </summary>
    public static NorbixTestFixture Create(Action<NorbixClientOptions>? configure = null)
    {
        var opts = new NorbixClientOptions
        {
            ApiKey = "test-api-key",
            ProjectId = "test-project",
            HubVersion = "v2",
            ApiVersion = "v2",
        };
        configure?.Invoke(opts);
        return new NorbixTestFixture(opts);
    }

    /// <summary>Register a JSON response for a route. Returns <c>this</c> for chaining.</summary>
    public NorbixTestFixture Respond(
        string pathSuffix,
        object body,
        System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK
    )
    {
        _handler.RespondJson(pathSuffix, body, status);
        return this;
    }

    /// <summary>
    /// Make all unmatched requests return 204. Useful for endpoint smoke tests
    /// that care about the outgoing request, not response deserialization.
    /// </summary>
    public NorbixTestFixture RespondNoContentDefault()
    {
        _handler.RespondNoContentDefault();
        return this;
    }

    /// <summary>Return this JSON body for the next request, regardless of URL.</summary>
    public NorbixTestFixture RespondNext(
        object? body,
        System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK
    )
    {
        _handler.RespondJsonNext(body, status);
        return this;
    }

    /// <summary>All requests captured during the test, in the order they fired.</summary>
    public IReadOnlyList<RecordedRequest> RecordedRequests => _handler.Requests;

    public RecordedRequest? LastRequest =>
        RecordedRequests.Count == 0 ? null : RecordedRequests[^1];

    public void Dispose() => Client.Dispose();
}
