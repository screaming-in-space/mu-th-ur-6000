using Microsoft.Extensions.Logging;
using Muthur.Clients;
using Muthur.Contracts;

namespace Muthur.Agents;

/// <summary>
/// Orchestrates a single agent job: create session, send prompt, poll until done.
/// Decoupled from hosting — usable from console apps, tests, or background services.
/// </summary>
public sealed class AgentRunner(ILogger<AgentRunner> logger, MuthurApiClient client)
{
    /// <summary>
    /// Creates a session, sends a prompt, and polls until the agent finishes or the timeout expires.
    /// </summary>
    public async Task<AgentRunResult> RunAsync(
        AgentJobRequest job,
        CancellationToken cancellationToken = default)
    {
        // 1. Create session.
        logger.LogInformation("Creating agent session...");

        var session = await client.CreateSessionAsync(job.SystemPrompt, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Session created: AgentId={AgentId}, WorkflowId={WorkflowId}",
            session.AgentId, session.WorkflowId);

        // 2. Send prompt.
        logger.LogInformation("Sending prompt: {Prompt}", job.Prompt);

        await client.SendPromptAsync(session.AgentId, job.Prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // 3. Poll until done.
        var state = await PollUntilCompleteAsync(session.AgentId, job.PollTimeout, cancellationToken)
            .ConfigureAwait(false);

        return new AgentRunResult(session.AgentId, session.WorkflowId, state);
    }

    /// <summary>
    /// Polls agent state until the workflow has processed at least one turn and is idle,
    /// or the timeout expires.
    /// </summary>
    /// <returns>The final <see cref="AgentState"/>, or <c>null</c> if timed out.</returns>
    public async Task<AgentState?> PollUntilCompleteAsync(
        string agentId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        pollCts.CancelAfter(timeout);

        try
        {
            while (!pollCts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), pollCts.Token).ConfigureAwait(false);

                var state = await client.GetAgentStateAsync(agentId, pollCts.Token).ConfigureAwait(false);

                logger.LogInformation("Turn {Turn} | Processing={IsProcessing}",
                    state.TurnCount, state.IsProcessing);

                // TurnCount > 0 means the workflow has processed a prompt.
                // IsProcessing == false means it's done with the current turn.
                // This avoids the race where we poll before the signal is picked up.
                if (state.TurnCount > 0 && !state.IsProcessing)
                {
                    return state;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Poll timed out after {Timeout}", timeout);
        }

        return null;
    }
}

/// <summary>Describes an agent job to run.</summary>
public sealed record AgentJobRequest
{
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }

    /// <summary>Max time to wait for completion. Defaults to 5 minutes.</summary>
    public TimeSpan PollTimeout { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>Result of a completed agent run.</summary>
public sealed record AgentRunResult(
    string AgentId,
    string WorkflowId,
    AgentState? FinalState);
