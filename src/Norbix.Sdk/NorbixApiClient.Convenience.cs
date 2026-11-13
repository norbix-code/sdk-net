using Norbix.Sdk.Transport;
using Norbix.Sdk.Types.Api;

namespace Norbix.Sdk;

public sealed partial class NorbixApiClient
{
    /// <summary>
    /// Lightweight gateway call that returns runtime/environment information.
    /// This is useful for diagnostics and smoke tests.
    /// </summary>
    public Task<EchoResponse?> EchoAsync(CancellationToken cancellationToken = default)
    {
        return Transport.SendAsync<EchoResponse>(
            new NorbixRequestSpec
            {
                Target = NorbixTarget.Api,
                Path = "/{version}/echo",
                Method = "GET",
                Request = new Echo(),
                PathParams = Array.Empty<string>(),
                Scope = NorbixScope.Project,
            },
            cancellationToken);
    }

    /// <summary>
    /// The URL that starts a project social sign-in:
    /// <c>GET {api}/{version}/membership/auth/social/{provider}?projectId=…[&amp;env=…][&amp;mode=…][&amp;returnUrl=…]</c>.
    /// Not a call: send the browser there (on a phone, the system browser) and
    /// the gateway redirects to the provider. A navigation carries no headers,
    /// so the project id and a non-PROD env (<see cref="WithEnv"/>) travel in the
    /// query. The gateway sends the browser back to <paramref name="returnUrl"/>
    /// (or the mode's page) with <c>?code=…&amp;provider=…</c>; trade the code with
    /// <c>Membership.ExchangeSocialSignInCodeAsync</c> within 2 minutes.
    /// </summary>
    /// <param name="provider"><c>google</c>, <c>apple</c>, <c>microsoft</c>, <c>github</c>, <c>facebook</c>, <c>x</c> or <c>okta</c>.</param>
    /// <param name="mode">Name of the integration's OAuth mode. Default: the only mode, else the first by name.</param>
    /// <param name="returnUrl">App page to come back to: absolute https (http only on localhost), origin in the project's allowed origins.</param>
    public string SocialSignInUrl(string provider, string? mode = null, string? returnUrl = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(provider);
        return _transport.NavigationUrl(
            NorbixTarget.Api,
            "/{version}/membership/auth/social/" + Uri.EscapeDataString(provider),
            new KeyValuePair<string, string?>[] { new("mode", mode), new("returnUrl", returnUrl) });
    }
}
