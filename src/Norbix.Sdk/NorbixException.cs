using Norbix.Sdk.Types;

namespace Norbix.Sdk;

/// <summary>
/// Exception thrown by the SDK for both server- and client-side failures.
/// Mirrors the TypeScript SDK's <c>NorbixError</c>: a <see cref="StatusCode"/>
/// (or 0 for client-only failures), a structured <see cref="Code"/>, and the
/// raw response payload when available.
/// </summary>
public sealed class NorbixException : Exception
{
    /// <summary>HTTP status from the gateway. <c>0</c> when the failure was client-side.</summary>
    public int StatusCode { get; }

    /// <summary>Structured error code. Either a ServiceStack error code from the gateway
    /// or one of the SDK's well-known <c>NORBIX_*</c> codes.</summary>
    public string? Code { get; }

    /// <summary>Per-field validation errors when present.</summary>
    public IReadOnlyList<ResponseError> FieldErrors { get; }

    /// <summary>The URL the SDK was about to call (or did call) when the error happened.</summary>
    public string? Url { get; }

    /// <summary>Raw response body, parsed as JSON when possible.</summary>
    public object? RawBody { get; }

    public NorbixException(
        string message,
        int statusCode = 0,
        string? code = null,
        IReadOnlyList<ResponseError>? fieldErrors = null,
        string? url = null,
        object? rawBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        FieldErrors = fieldErrors ?? Array.Empty<ResponseError>();
        Url = url;
        RawBody = rawBody;
    }
}

/// <summary>Stable string codes the SDK uses for client-side errors.</summary>
public static class NorbixErrorCodes
{
    public const string NotAuthenticated = "NORBIX_NOT_AUTHENTICATED";
    public const string AccountScopeRequired = "NORBIX_ACCOUNT_SCOPE_REQUIRED";
    public const string MissingPathParam = "NORBIX_MISSING_PATH_PARAM";
    public const string NetworkError = "NORBIX_NETWORK_ERROR";
}
