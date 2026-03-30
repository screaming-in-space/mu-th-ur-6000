# Muthur.Api

Minimal API host. Three endpoints for agent lifecycle management.

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/v1/agent/sessions` | Start a new agent workflow. Returns `agentId` and `workflowId`. |
| `POST` | `/v1/agent/sessions/{agentId}/prompt` | Send a prompt to a running agent via Temporal signal. |
| `GET` | `/v1/agent/sessions/{agentId}` | Query current agent state (processing, turn count, last response). |

## Design decisions

**No Worker dependency.** The API references only `Muthur.Contracts`. Workflow interactions use untyped Temporal handles — `StartWorkflowAsync("AgentWorkflow", ...)`, `SignalAsync("SendPromptAsync", ...)`, `QueryAsync<AgentState>("GetState", ...)`. This keeps the API deployable independently of the Worker.

**Temporal client via DI.** `ITemporalClient` is injected into route handlers. The connection string comes from Aspire service discovery or falls back to configuration.

## Dependencies

- `Muthur.Contracts` — shared records
- `Muthur.ServiceDefaults` — Aspire service defaults, Temporal client registration
- `Temporalio.Client` — for `ITemporalClient`
