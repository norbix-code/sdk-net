namespace Norbix.Sdk.Types;

/// <summary>
/// Error envelope returned by the gateway on failure. Mirrors the
/// ServiceStack <c>ResponseStatus</c> shape so consumers can deserialize
/// errors without taking a ServiceStack runtime dependency.
/// </summary>
public sealed class ResponseStatus
{
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
    public List<ResponseError>? Errors { get; set; }
    public Dictionary<string, string>? Meta { get; set; }
}

/// <summary>One per-field error inside <see cref="ResponseStatus"/>.</summary>
public sealed class ResponseError
{
    public string? ErrorCode { get; set; }
    public string? FieldName { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string>? Meta { get; set; }
}

/// <summary>
/// Wrapper for an envelope with both a typed payload and an optional
/// <see cref="ResponseStatus"/>. The gateway returns either this shape or a
/// raw payload depending on the endpoint; the SDK handles both.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
public class ResponseBase<T>
{
    public T? Result { get; set; }
    public ResponseStatus? ResponseStatus { get; set; }
}

/// <summary>
/// Empty success response. Returned for endpoints that have no payload.
/// </summary>
public sealed class EmptyResponse
{
    public ResponseStatus? ResponseStatus { get; set; }
}

/// <summary>
/// Standard "I created something" response. Carries the generated id of the
/// new entity.
/// </summary>
public sealed class IdResponse
{
    public string? Id { get; set; }
    public ResponseStatus? ResponseStatus { get; set; }
}
