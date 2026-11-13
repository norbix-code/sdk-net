#nullable enable annotations
#nullable disable warnings

namespace Norbix.Sdk.Types.Api;

// Hand-written companion to Generated/Api.dtos.cs for the social sign-in code
// exchange (POST /{version}/membership/auth/social/{Provider}/exchange).
//
// The route has no [Authenticate] and the export marks it INorbixOptionalAuth,
// so a client with a key or a JWT would attach it. The gateway still judges a
// token it gets on an open route: its global request filter refuses a JWT from
// before a password change (401, credential version), a token of another
// environment (403) and a removed team member (401). The exchange is how a
// user signs in again, so it must not carry an old token.
// INorbixUnauthenticated (it wins over INorbixOptionalAuth in the source
// generator) sends it with no Authorization header.

public partial class ExchangeSocialSignInCodeRequest : INorbixUnauthenticated { }
