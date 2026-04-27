namespace Norbix.Sdk.Auth;

/// <summary>
/// Credentials accepted by the SDK login helper. Mirrors the
/// gateway's <c>Authenticate</c> request DTO but without the ServiceStack
/// types in its public surface.
/// </summary>
public sealed class LoginCredentials
{
    /// <summary>Auth provider, e.g. <c>"credentials"</c>. Defaults to <c>"credentials"</c>.</summary>
    public string Provider { get; set; } = "credentials";

    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool? RememberMe { get; set; }

    /// <summary>OAuth-style access token + secret, used for federated providers.</summary>
    public string? AccessToken { get; set; }
    public string? AccessTokenSecret { get; set; }

    public Dictionary<string, string>? Meta { get; set; }
}

/// <summary>Result returned by the SDK login helper.</summary>
public sealed class LoginResponse
{
    public string? BearerToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, object?>? Extras { get; set; }
}
