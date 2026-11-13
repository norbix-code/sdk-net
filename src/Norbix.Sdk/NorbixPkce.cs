using System.Security.Cryptography;
using System.Text;

namespace Norbix.Sdk;

/// <summary>A PKCE verifier and its S256 challenge.</summary>
/// <param name="CodeVerifier">43 characters <c>[A-Za-z0-9-_]</c>. Keep it (memory on a device, session on a server); never put it in a URL.</param>
/// <param name="CodeChallenge">base64url (no padding) SHA-256 of the verifier, 43 characters. Send it on the start.</param>
public sealed record NorbixPkcePair(string CodeVerifier, string CodeChallenge);

/// <summary>
/// PKCE (RFC 7636, S256) between the app and the Norbix gateway for social
/// sign-in and link identity. The start carries only the challenge
/// (<c>SocialSignInUrl(…, codeChallenge)</c>, <c>StartLinkIdentityRequest.CodeChallenge</c>);
/// the exchange and the link finish send the verifier
/// (<c>CodeVerifier</c>). A one-time code started with a challenge works only
/// with the matching verifier: CM-ERRORS-MEMBERSHIP-058 otherwise, and the
/// code is used up. A challenge that is not S256 is CM-ERRORS-MEMBERSHIP-059.
/// </summary>
public static class NorbixPkce
{
    /// <summary>A fresh verifier (32 bytes from the OS random source, base64url) and its S256 challenge.</summary>
    public static NorbixPkcePair Create()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return new NorbixPkcePair(verifier, ChallengeOf(verifier));
    }

    /// <summary>The S256 challenge of a verifier: base64url (no padding) of SHA-256 over its ASCII bytes.</summary>
    public static string ChallengeOf(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(codeVerifier);
        if (codeVerifier.Length is < 43 or > 128 || !codeVerifier.All(IsUnreserved))
        {
            throw new ArgumentException(
                "A PKCE code verifier is 43 to 128 characters of A-Z a-z 0-9 - . _ ~.", nameof(codeVerifier));
        }
        return Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
    }

    private static bool IsUnreserved(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '.' or '_' or '~';

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
