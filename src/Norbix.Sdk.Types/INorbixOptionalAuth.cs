namespace Norbix.Sdk.Types;

/// <summary>
/// Marks a request that the gateway does not authenticate but that answers
/// JSON — a public link route such as the one-click unsubscribe, the
/// preferences page behind a signed unsubscribe link, or a signed preview
/// link. The client sends its token when it has one and sends the request
/// without one when it has none, instead of refusing the call.
/// </summary>
/// <remarks>
/// Written onto the generated request types by the type generator, from the
/// gateway's own metadata (<c>requiresAuth</c> is not <c>true</c>). The
/// stronger <see cref="INorbixUnauthenticated"/> (never send a token) wins
/// when both would apply.
/// </remarks>
public interface INorbixOptionalAuth { }
