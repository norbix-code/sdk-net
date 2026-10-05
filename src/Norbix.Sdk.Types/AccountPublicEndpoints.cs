#nullable enable annotations
#nullable disable warnings

namespace Norbix.Sdk.Types.Hub;

// Hand-written companions to Generated/Hub.dtos.cs for the four account
// routes a person calls before they have a session.
//
// The gateway has no [Authenticate] on these routes (Hub.Account:
// Account/Create.cs, Account/Team/Verify.cs, Project/GetRegions.cs,
// Account/Verify.cs), and it never reads the X-CM-AccountId header. The
// export marks them INorbixOptionalAuth, so a signed-in client would still
// attach its token, and VerifyAccount — which has an AccountId field — looks
// account-scoped. INorbixUnauthenticated (it wins over INorbixOptionalAuth
// in the source generator) makes the client send them with no Authorization
// header and without asking for an AccountId on the client. VerifyAccount
// carries the account id once, in its own query (request.AccountId).

public partial class CreateAccount : INorbixUnauthenticated { }

public partial class CreateTeamMemberFromInvitation : INorbixUnauthenticated { }

public partial class GetAccountRegions : INorbixUnauthenticated { }

public partial class VerifyAccount : INorbixUnauthenticated { }
