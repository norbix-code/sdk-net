using Norbix.Sdk.Types;

namespace Norbix.Sdk.Transport;

/// <summary>
/// Internal abstraction the source-generated module classes use to talk to
/// the gateways. <b>Not part of the public API.</b> Lets tests substitute an
/// in-memory transport via <c>InternalsVisibleTo("Norbix.Sdk.Tests")</c>
/// without spinning up an HTTP server.
/// </summary>
internal interface INorbixTransport
{
    /// <summary>Send a request and deserialize the response.</summary>
    /// <typeparam name="TResponse">Response payload type, or <c>EmptyResponse</c> for verbless endpoints.</typeparam>
    Task<TResponse?> SendAsync<TResponse>(
        NorbixRequestSpec spec,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes the call the source-generated module wants the transport to make.
/// </summary>
internal readonly struct NorbixRequestSpec
{
    public NorbixTarget Target { get; init; }
    public string Path { get; init; }
    public string Method { get; init; }
    public object? Request { get; init; }
    public IReadOnlyList<string> PathParams { get; init; }
    public NorbixScope Scope { get; init; }
}

internal enum NorbixTarget
{
    Api,
    Hub,
}

internal enum NorbixScope
{
    Project,
    Account,
    Public,
    Unauthenticated,
}
