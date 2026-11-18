#nullable enable annotations
#nullable disable warnings

namespace Norbix.Sdk.Types.Hub;

// Hand-written companion to Generated/Hub.dtos.cs (the Hub copy of the same
// routes) for the public project config and legal pages (GET /{version}/public/projects/{ProjectId}/config
// and /legal/{Kind}).
//
// The gateway does not authenticate these routes
// (Services.Api/Heartbeat/PublicProjectConfig.cs, PublicProjectLegal.cs): an
// Admin Portal or a sign-in page reads them before anyone signs in, and the
// project id travels in the path. The export marks them INorbixOptionalAuth,
// which still makes a client with no key and no token refuse the call before
// it is sent. INorbixUnauthenticated (it wins over INorbixOptionalAuth in the
// source generator) sends them with no Authorization header and needs no
// credentials, like the brand asset of the same family
// (GetPublicProjectBrandAsset, already marked by the export).

public partial class GetPublicProjectConfig : INorbixUnauthenticated { }

public partial class GetPublicProjectLegal : INorbixUnauthenticated { }
