# Muthur.Utilities

Reusable agent orchestration, decoupled from hosting.

## AgentRunner

Orchestrates a single agent job: create session, send prompt, poll until done.

```csharp
var result = await runner.RunAsync(new AgentJobRequest(
    Prompt: "Extract and store this PDF: /path/to/doc.pdf",
    SystemPrompt: "You are a document processing agent...",
    PollTimeout: TimeSpan.FromMinutes(10)),
    cancellationToken);

if (result.FinalState is { } state)
{
    Console.WriteLine(state.LastResponse);
}
```

### AgentJobRequest

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Prompt` | `string` | required | The user prompt to send |
| `SystemPrompt` | `string?` | `null` | Optional system prompt for the session |
| `PollTimeout` | `TimeSpan` | 5 minutes | Max time to wait for completion |

### AgentRunResult

| Property | Type | Purpose |
|----------|------|---------|
| `AgentId` | `string` | The created session's agent ID |
| `WorkflowId` | `string` | The Temporal workflow ID |
| `FinalState` | `AgentState?` | Final state, or `null` if timed out |

## Registration

```csharp
services.AddMuthurApiClient(new Uri("http://muthur-api"));
services.AddAgentRunner();
```

## Dependencies

- `Muthur.Clients` — typed HTTP client for the API
