using System.Text.Json;
using Norbix.Sdk.Transport;
using Norbix.Sdk.Types.Hub;

namespace Norbix.Sdk;

/// <summary>
/// Beta: options for <see cref="NorbixHubClient.WaitForWorkflowRunAsync"/>.
/// Workflows are in beta; bugs may occur.
/// </summary>
public sealed class WaitForWorkflowRunOptions
{
    /// <summary>How long to keep polling before giving up. Default 60 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>The pause between two reads of the run. Default 1 second.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Beta: thrown by <see cref="NorbixHubClient.WaitForWorkflowRunAsync"/> when the run is still
/// running after the timeout. <see cref="LastRun"/> is the last answer read.
/// It is not a <see cref="NorbixException"/> (that type is sealed); the gateway call itself
/// succeeded, only the wait ran out.
/// </summary>
public sealed class WorkflowRunTimeoutException : TimeoutException
{
    /// <summary>The run as it was read the last time (its <c>Status</c> is still <c>running</c>).</summary>
    public WorkflowRunDto? LastRun { get; }

    /// <summary>The timeout that ran out.</summary>
    public TimeSpan Timeout { get; }

    public WorkflowRunTimeoutException()
    {
    }

    public WorkflowRunTimeoutException(string message)
        : base(message)
    {
    }

    public WorkflowRunTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public WorkflowRunTimeoutException(WorkflowRunDto lastRun, TimeSpan timeout)
        : base(
            $"Workflow run '{lastRun?.RunId}' is still '{lastRun?.Status}' after {timeout.TotalSeconds:0.###} s.")
    {
        LastRun = lastRun;
        Timeout = timeout;
    }
}

/// <summary>Beta: reading a workflow run. Workflows are in beta; bugs may occur.</summary>
public static class WorkflowRunExtensions
{
    /// <summary>
    /// Beta: the output of the latest succeeded execution of a step — the highest <c>Pass</c>
    /// (loops), then the highest <c>Attempt</c> (retries) among the executions whose
    /// <c>Status</c> is <c>succeeded</c> — read as <typeparamref name="T"/> with the SDK's
    /// JSON options. <c>default</c> when the step never succeeded or has no output.
    /// </summary>
    public static T? StepOutput<T>(this WorkflowRunDto run, string stepId)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

        var execution = run.Steps?
            .Where(s =>
                string.Equals(s.StepId, stepId, StringComparison.Ordinal)
                && string.Equals(s.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Pass)
            .ThenByDescending(s => s.Attempt)
            .FirstOrDefault();

        return execution?.Output switch
        {
            null => default,
            T typed => typed,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => default,
            JsonElement element => element.Deserialize<T>(HttpTransport.JsonOptions),
            var other => JsonSerializer
                .SerializeToElement(other, HttpTransport.JsonOptions)
                .Deserialize<T>(HttpTransport.JsonOptions),
        };
    }
}
