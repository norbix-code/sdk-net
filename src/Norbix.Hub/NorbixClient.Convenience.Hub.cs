using Norbix.Sdk.Transport;
using Norbix.Sdk.Types.Hub;

namespace Norbix.Sdk;

public sealed partial class NorbixClient
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
                Target = NorbixTarget.Hub,
                Path = "/{version}/echo",
                Method = "GET",
                Request = new Echo(),
                PathParams = Array.Empty<string>(),
                Scope = NorbixScope.Project,
            },
            cancellationToken);
    }
}

