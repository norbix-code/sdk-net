using Norbix.Sdk.Transport;
using Norbix.Sdk.Types.Hub;

namespace Norbix.Sdk;

public sealed partial class NorbixHubClient
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

    /// <summary>
    /// The details of one marketplace function run: the project log lines filed
    /// under the run's correlation id (<c>marketplace:run:started</c>, <c>step</c>,
    /// then <c>completed</c>, <c>failed</c> or <c>skipped</c>). Pass
    /// <see cref="MarketplaceFunctionRunDto.CorrelationId"/> of a row from
    /// <c>Code.GetMarketplaceFunctionRunsAsync</c>. Only a row with
    /// <see cref="MarketplaceFunctionRunDto.HasDetails"/> has lines — they are
    /// written only while the environment's Norbix Logging is on.
    /// Same call as <c>Logs.GetLogsByCorrelationIdAsync</c>
    /// (GET /{version}/logs/audit?targetCorrelationId=…).
    /// </summary>
    public Task<GetLogsByCorrelationIdResponse?> GetMarketplaceFunctionRunDetailsAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return Logs.GetLogsByCorrelationIdAsync(
            new GetLogsByCorrelationId { TargetCorrelationId = correlationId },
            cancellationToken);
    }
}
