namespace Norbix.Sdk.Types;

/// <summary>
/// Marker interface for a request DTO that returns no body (HTTP 204).
/// Replaces ServiceStack's <c>IReturnVoid</c> so consumers don't see
/// transport-internal types.
/// </summary>
public interface INorbixRequest
{
}

/// <summary>
/// Marker interface for a request DTO that returns <typeparamref name="TResponse"/>.
/// Replaces ServiceStack's <c>IReturn&lt;T&gt;</c>.
/// </summary>
/// <typeparam name="TResponse">The shape returned by the gateway on success.</typeparam>
public interface INorbixRequest<TResponse> : INorbixRequest
{
}

/// <summary>
/// Marks a request as account-scoped. The generator emits methods on the
/// client that throw <c>NorbixException(NORBIX_ACCOUNT_SCOPE_REQUIRED)</c>
/// when called without an <c>AccountId</c> on the client options.
/// </summary>
public interface INorbixAccountScoped
{
    string? AccountId { get; set; }
}
