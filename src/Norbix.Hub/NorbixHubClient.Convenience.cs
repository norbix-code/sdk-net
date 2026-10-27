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

    /// <summary>
    /// Beta: wait for a workflow run to finish or park. Reads the run
    /// (GET /{version}/code/workflows/runs/{RunId}, <c>Code.GetCodeWorkflowRunAsync</c>) every
    /// <see cref="WaitForWorkflowRunOptions.PollInterval"/> (default 1 s) and returns it as soon
    /// as its <c>Status</c> is <c>succeeded</c>, <c>failed</c>, <c>cancelled</c> or
    /// <c>waiting</c>. Still running after <see cref="WaitForWorkflowRunOptions.Timeout"/>
    /// (default 60 s) → <see cref="WorkflowRunTimeoutException"/> with the last run read.
    /// An answer with no run → <see cref="NorbixException"/>.
    /// Workflows are in beta; bugs may occur.
    /// </summary>
    public async Task<WorkflowRunDto> WaitForWorkflowRunAsync(
        string runId,
        WaitForWorkflowRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var timeout = options?.Timeout ?? TimeSpan.FromSeconds(60);
        var pollInterval = options?.PollInterval ?? TimeSpan.FromSeconds(1);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        while (true)
        {
            var response = await Code
                .GetCodeWorkflowRunAsync(new GetCodeWorkflowRun { RunId = runId }, cancellationToken)
                .ConfigureAwait(false);
            var run = response?.Run
                ?? throw new NorbixException($"The answer for workflow run '{runId}' has no run.");

            if (IsSettledWorkflowRunStatus(run.Status))
            {
                return run;
            }

            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
            if (elapsed >= timeout)
            {
                throw new WorkflowRunTimeoutException(run, timeout);
            }

            var pause = timeout - elapsed < pollInterval ? timeout - elapsed : pollInterval;
            await Task.Delay(pause, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsSettledWorkflowRunStatus(string? status) =>
        status is not null
        && (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "waiting", StringComparison.OrdinalIgnoreCase));
}
