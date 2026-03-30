# Structure

Project architecture and file organization for mu-th-ur-6000.

## Solution Layout

```
mu-th-ur-6000/
├── docs/
│   ├── RULES.md                    # Technical constraints, rejected patterns
│   └── STRUCTURE.md                # This file
├── src/
│   ├── Aspire.Hosting.Temporal/    # Aspire extension for Temporal container
│   ├── Muthur.AppHost/             # Aspire orchestration host
│   ├── Muthur.Api/                 # Minimal API — HTTP endpoints
│   ├── Muthur.Bishop.Worker/       # Temporal worker — workflows + activities
│   ├── Muthur.Contracts/           # Shared types — no dependencies
│   └── Muthur.ServiceDefaults/     # Shared DI, M.E.AI pipeline
├── samples/                        # Sample PDFs for testing
├── .claude/
│   └── launch.json                 # Preview tool config
├── CLAUDE.md                       # Agent entry point
├── Directory.Build.props           # net10.0, C# 14.0, nullable
├── global.json                     # SDK pin
├── nuget.config                    # Isolated NuGet sources (<clear/>)
└── Muthur.slnx                     # Solution manifest
```

## Projects

### Muthur.AppHost

Aspire orchestration. Starts Temporal as a container, then the API and Worker.

| File | Purpose |
|------|---------|
| `Program.cs` | `EnsureDockerAsync()`, `AddTemporalDevServer`, project wiring |

**Depends on:** Aspire.Hosting.Temporal, Muthur.Api (project ref), Muthur.Bishop.Worker (project ref)

### Aspire.Hosting.Temporal

Aspire extension — adds Temporal dev server as a container resource with health checks.

| File | Purpose |
|------|---------|
| `TemporalResource.cs` | `ContainerResource` + `IResourceWithConnectionString` |
| `TemporalResourceBuilderExtensions.cs` | `AddTemporalDevServer()` — image, ports, health check |
| `DockerDesktopExtensions.cs` | `EnsureDockerAsync()` — auto-launches Docker Desktop |

**Depends on:** `Aspire.Hosting` 13.2.0

### Muthur.Api

Minimal API host. Three endpoints, no Worker dependency.

| File | Purpose |
|------|---------|
| `Program.cs` | Web host, service registration |
| `Routes/Agent.cs` | `POST /v1/agent/sessions`, `POST .../prompt`, `GET .../state` |

**Depends on:** Muthur.Contracts, Muthur.ServiceDefaults

### Muthur.Bishop.Worker

Temporal worker. Hosts the agentic loop workflow and all activities.

| File | Purpose |
|------|---------|
| `Program.cs` | Generic host, Temporal worker registration, DI |
| `Workflows/AgentWorkflow.cs` | Signal-driven agentic loop with `ContinueAsNew` |
| `Activities/LlmActivities.cs` | Calls `IChatClient.GetResponseAsync`, extracts tool calls |
| `Activities/ToolActivities.cs` | Routes tool calls by name through `ToolRegistry` |
| `Activities/PdfActivities.cs` | PdfPig text extraction — static, no DI |
| `Activities/ToolRegistry.cs` | `AIFunctionFactory.Create()` registration + name→handler dispatch |

**Depends on:** Muthur.Contracts, Muthur.ServiceDefaults, Temporalio.Extensions.Hosting

### Muthur.Contracts

Shared types. Zero dependencies. Referenced by Api and Worker.

| File | Purpose |
|------|---------|
| `AgentConstants.cs` | Task queue name, role strings, turn limit, workflow ID factory |
| `AgentInput.cs` | `AgentWorkflowInput`, `LlmActivityInput/Output`, `ToolCallRequest/Result`, `ConversationMessage` |
| `AgentSignals.cs` | `PromptSignal`, `AgentState` |
| `PdfExtractionResult.cs` | `PdfExtractionResult(Text, PageCount, Metadata)` |

**Depends on:** nothing

### Muthur.ServiceDefaults

Shared DI extensions. Owns the M.E.AI pipeline and Aspire service defaults.

| File | Purpose |
|------|---------|
| `Extensions.cs` | `AddServiceDefaults()` — OpenTelemetry, health checks, service discovery |
| `AiClientExtensions.cs` | `AddAgentChatClient()` — provider detection, `ChatClientBuilder` pipeline |

**Depends on:** Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI

## Dependency Graph

```
AppHost
├── Aspire.Hosting.Temporal  (IsAspireProjectResource=false)
├── Api
│   ├── Contracts
│   └── ServiceDefaults
└── Bishop.Worker
    ├── Contracts
    └── ServiceDefaults
```

Api and Worker share Contracts + ServiceDefaults but never reference each other.
The Api talks to workflows via untyped Temporal handles (string-based names).

## Data Flow

```
HTTP request → Api (Routes/Agent.cs)
  → Temporal client: StartWorkflowAsync / SignalAsync / QueryAsync
  → AgentWorkflow (signal queue → WaitConditionAsync)
    → LlmActivities.CallLlmAsync (IChatClient → LLM provider)
    → if tool calls: ToolActivities.ExecuteToolAsync → ToolRegistry → PdfActivities
    → loop back to LLM until no tool calls
    → return final response via AgentState query
```

## Ports (local dev)

| Service | Port | Source |
|---------|------|--------|
| Aspire Dashboard | 15137 | launchSettings.json |
| Temporal gRPC | dynamic | Aspire container mapping |
| Temporal UI | dynamic | Aspire container mapping |
| Muthur.Api | dynamic | Aspire-assigned |
