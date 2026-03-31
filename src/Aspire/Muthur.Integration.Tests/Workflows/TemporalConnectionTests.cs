using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Muthur.Contracts;
using Muthur.Integration.Tests.Infrastructure;
using Temporalio.Client;

namespace Muthur.Integration.Tests.Workflows;

/// <summary>
/// Direct Temporal tests — bypass the API, talk to Temporal directly.
/// Diagnoses whether the workflow is actually executing.
/// </summary>
[Collection("Muthur")]
public sealed class TemporalConnectionTests(MuthurFixture platform)
{
    [Fact]
    public async Task CanConnectToTemporal()
    {
        var connectionString = await platform.App.GetConnectionStringAsync("muthur-temporal-dev");
        Assert.NotNull(connectionString);
        Assert.NotEmpty(connectionString);

        // Connect directly as a client.
        var client = await TemporalClient.ConnectAsync(
            new TemporalClientConnectOptions(connectionString)
            {
                Namespace = "default"
            });

        Assert.NotNull(client);
    }

    [Fact]
    public async Task CanStartWorkflowAndSignal()
    {
        var connectionString = await platform.App.GetConnectionStringAsync("muthur-temporal-dev");
        Assert.NotNull(connectionString);

        var client = await TemporalClient.ConnectAsync(
            new TemporalClientConnectOptions(connectionString)
            {
                Namespace = "default"
            });

        var agentId = Guid.NewGuid().ToString("N")[..8];
        var workflowId = AgentConstants.WorkflowId(agentId);

        // Start the workflow — same as the API does.
        var handle = await client.StartWorkflowAsync(
            "AgentWorkflow",
            [new AgentWorkflowInput(agentId, "You are a test agent.")],
            new WorkflowOptions(workflowId, AgentConstants.TaskQueue));

        Assert.NotNull(handle);

        // Signal it.
        await handle.SignalAsync(
            "SendPrompt",
            [new PromptSignal("Say hello.")]);

        // Query state — should reflect the signal was received.
        // Wait up to 30 seconds for the workflow to pick up the signal.
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));

            var state = await handle.QueryAsync<AgentState>("GetState", []);

            // If IsProcessing is true, the workflow picked up the signal.
            if (state.IsProcessing || state.TurnCount > 0)
            {
                Assert.True(true, $"Workflow is active: IsProcessing={state.IsProcessing}, TurnCount={state.TurnCount}");
                return;
            }
        }

        // If we get here, the workflow never picked up the signal.
        var finalState = await handle.QueryAsync<AgentState>("GetState", []);
        Assert.Fail($"Workflow never processed the signal after 30s. State: IsProcessing={finalState.IsProcessing}, TurnCount={finalState.TurnCount}, LastResponse={finalState.LastResponse}");
    }
}
