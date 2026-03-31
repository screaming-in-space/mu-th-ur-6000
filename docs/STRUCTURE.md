# Structure

Project architecture and file organization for mu-th-ur-6000.

## Solution Layout

```
mu-th-ur-6000/
├── docs/
│   ├── RULES.md                    # Technical constraints, rejected patterns
│   └── STRUCTURE.md                # This file
├── src/
│   ├── Aspire/                     # Infrastructure & orchestration
│   │   ├── Aspire.Hosting.Temporal/  # Aspire extension for Temporal container
│   │   └── Muthur.AppHost/          # Aspire orchestration host
│   ├── Apps/                       # User-facing applications
│   │   └── Muthur.Console/           # Demo CLI - kicks off agent jobs
│   ├── Core/                       # Shared libraries - no host dependencies
│   │   ├── Muthur.Clients/           # Typed HTTP client for the Muthur API
│   │   ├── Muthur.Contracts/         # Shared types - no dependencies
│   │   ├── Muthur.Data/              # Postgres + Redis - document storage + vector search
│   │   ├── Muthur.Logging/           # Serilog structured logging
│   │   ├── Muthur.ServiceDefaults/   # Shared DI, M.E.AI pipeline, telemetry
│   │   ├── Muthur.Telemetry/         # Custom ActivitySource, Meter, Activity extensions
│   │   ├── Muthur.Tools/             # Agent tools - PDF extraction, document storage
│   │   └── Muthur.Utilities/         # Reusable agent orchestration (AgentRunner)
│   └── Hosts/                      # Runnable services
│       ├── Muthur.Api/               # Minimal API - HTTP endpoints + OpenAPI
│       └── Muthur.Bishop.Worker/     # Temporal worker - workflows + activities
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

Aspire orchestration. Starts Temporal, Postgres, and Redis containers, then the API and Worker.

| File | Purpose |
|------|---------|
| `Program.cs` | `EnsureDockerAsync()`, resource wiring, `.WaitFor()` chains |

**Depends on:** Aspire.Hosting.Temporal, Aspire.Hosting.PostgreSQL, Aspire.Hosting.Redis, Muthur.Api (project ref), Muthur.Bishop.Worker (project ref), Muthur.Console (project ref)

### Aspire.Hosting.Temporal

Aspire extension - adds Temporal dev server as a container resource with health checks. Auto-launches Docker Desktop if not running.

| File | Purpose |
|------|---------|
| `TemporalResource.cs` | `ContainerResource` + `IResourceWithConnectionString` |
| `TemporalResourceBuilderExtensions.cs` | `AddTemporalDevServer()` - image, ports, health check |
| `DockerDesktopExtensions.cs` | `EnsureDockerAsync()` - cross-platform Docker Desktop launcher |

**Depends on:** `Aspire.Hosting` 13.2.0

### Muthur.Api

Minimal API host. Agent lifecycle + document access endpoints.

| File | Purpose |
|------|---------|
| `Program.cs` | Web host, service registration, data layer + embedding generator |
| `Routes/Agent.cs` | `POST /v1/agent/sessions`, `POST .../prompt`, `GET .../state` |
| `Routes/Documents.cs` | `GET /v1/documents`, `GET .../{id}`, `GET .../{id}/content`, `GET .../search` |

**Depends on:** Muthur.Contracts, Muthur.Data, Muthur.ServiceDefaults

### Muthur.Bishop.Worker

Temporal worker. Hosts the agent workflow, ingestion child workflow, and all activities.

| File | Purpose |
|------|---------|
| `Program.cs` | Generic host, Temporal worker registration, DI for tools + data |
| `Workflows/AgentWorkflow.cs` | Signal-driven agentic loop with `ContinueAsNew` + child workflow trigger |
| `Workflows/DocumentIngestionWorkflow.cs` | Child workflow: chunk → embed → store |
| `Activities/LlmActivities.cs` | `IChatClient.GetResponseAsync` + tool call extraction |
| `Activities/ToolActivities.cs` | Name-based tool dispatch via `ToolRegistry` |
| `Activities/IngestionActivities.cs` | Chunk text, generate embeddings, store chunks |

**Depends on:** Muthur.Contracts, Muthur.Data, Muthur.ServiceDefaults, Muthur.Tools, Temporalio.Extensions.Hosting

### Muthur.Console

Thin demo CLI host. Configures DI and delegates to `AgentRunner`.

| File | Purpose |
|------|---------|
| `Program.cs` | Host builder, job config, result display |

**Depends on:** Muthur.Utilities, Muthur.ServiceDefaults, Microsoft.Extensions.Hosting

### Muthur.Clients

Typed HTTP client for the Muthur API. Error handling, RFC 7807 parsing, DI registration.

| File | Purpose |
|------|---------|
| `MuthurApiClient.cs` | Typed HttpClient - sessions, prompts, state, documents, search |
| `MuthurApiException.cs` | Exception with RFC 7807 body parsing, factory method |
| `MuthurErrorHandler.cs` | DelegatingHandler - 401/403 fail-fast |
| `MuthurClientExtensions.cs` | DI registration with SocketsHttpHandler connection pooling |

**Depends on:** Muthur.Contracts, Microsoft.Extensions.Http

### Muthur.Contracts

Shared types. Zero dependencies. Referenced by Api, Worker, Data, Tools, and Clients.

| File | Purpose |
|------|---------|
| `AgentConstants.cs` | Task queue name, role strings, turn limit, workflow ID factory |
| `AgentInput.cs` | `AgentWorkflowInput`, `LlmActivityInput/Output`, `ToolCallRequest/Result`, `ConversationMessage` |
| `AgentSignals.cs` | `PromptSignal`, `AgentState` |
| `ApiModels.cs` | `CreateSessionRequest/Response`, `SendPromptRequest`, `DocumentContentResponse` |
| `PdfExtractionResult.cs` | `PdfExtractionResult(Text, PageCount, Metadata)` |
| `DocumentModels.cs` | `DocumentRecord`, `DocumentSummary`, `DocumentChunkRecord`, `SimilarChunk` |
| `DocumentIngestionInput.cs` | `DocumentIngestionInput`, `TextChunk` |

**Depends on:** nothing

### Muthur.Data

Persistence layer. Owns Postgres + Redis, document storage, vector search.

| File | Purpose |
|------|---------|
| `DataExtensions.cs` | `AddMuthurData()` - NpgsqlDataSource with pgvector, Redis cache, repos, migration |
| `MigrationService.cs` | IHostedService - idempotent CREATE TABLE + pgvector extension on startup |
| `IDocumentRepository.cs` | Interface - store, get, list, vector search |
| `DocumentRepository.cs` | Dapper + NpgsqlDataSource - CRUD + pgvector cosine similarity |
| `CachedDocumentRepository.cs` | IDistributedCache decorator over DocumentRepository with TTL |

**Depends on:** Muthur.Contracts, Aspire.Npgsql, Aspire.StackExchange.Redis.DistributedCaching, Dapper, Pgvector

### Muthur.Tools

Agent tools. Isolated from the Worker for independent testability.

| File | Purpose |
|------|---------|
| `ToolRegistry.cs` | `AIFunctionFactory.Create()` registration + name→handler dispatch |
| `Handlers/PdfHandler.cs` | PdfPig text extraction - static, no DI |
| `Handlers/DocumentStoreHandler.cs` | Store document text in Postgres via `IDocumentRepository` |

**Depends on:** Muthur.Contracts, Muthur.Data, Microsoft.Extensions.AI, PdfPig

### Muthur.Utilities

Reusable agent orchestration. Decoupled from hosting — usable from console apps, tests, or background services.

| File | Purpose |
|------|---------|
| `AgentRunner.cs` | Create session → send prompt → poll until done |
| `UtilityExtensions.cs` | `AddAgentRunner()` DI registration |

**Depends on:** Muthur.Clients

### Muthur.Telemetry

Custom OpenTelemetry instrumentation. ActivitySource, Meter, fluent Activity extensions.

| File | Purpose |
|------|---------|
| `MuthurTrace.cs` | Static `ActivitySource("Muthur")` with `StartSpan()` helper |
| `MuthurMetrics.cs` | Static `Meter("Muthur")` - sessions, tool executions, documents, LLM duration |
| `ActivityExtensions.cs` | Null-safe fluent extensions: `WithTag`, `WithBaggage`, `RecordError`, `SetSuccess` |
| `TelemetryExtensions.cs` | `AddMuthurTelemetry()` - registers sources, service resource metadata |

**Depends on:** OpenTelemetry.Extensions.Hosting

### Muthur.ServiceDefaults

Shared DI extensions. Owns the M.E.AI pipeline, Aspire service defaults, and telemetry wiring.

| File | Purpose |
|------|---------|
| `Extensions.cs` | `AddServiceDefaults()` - Serilog, OpenTelemetry, telemetry, health checks, service discovery |
| `AiClientExtensions.cs` | `AddAgentChatClient()` + `AddAgentEmbeddingGenerator()` |

**Depends on:** Muthur.Logging, Muthur.Telemetry, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI

### Muthur.Logging

Serilog structured logging with optional JSON output and OTLP export.

| File | Purpose |
|------|---------|
| `LoggingExtensions.cs` | `AddStructuredLogging()` - console + OTLP, microsecond timestamps, JSON mode |

**Depends on:** Serilog.Extensions.Hosting, Serilog.Sinks.Console, Serilog.Formatting.Compact, Serilog.Sinks.OpenTelemetry

## Dependency Graph

```
Aspire/
  AppHost
  ├── Aspire.Hosting.Temporal  (IsAspireProjectResource=false)
  ├── Hosts/Api
  ├── Hosts/Bishop.Worker
  └── Apps/Console

Apps/
  Console
  ├── Core/Utilities
  │   └── Core/Clients
  │       └── Core/Contracts
  └── Core/ServiceDefaults
      ├── Core/Logging
      └── Core/Telemetry

Hosts/
  Api
  ├── Core/Contracts
  ├── Core/Data
  │   └── Core/Contracts
  └── Core/ServiceDefaults
      ├── Core/Logging
      └── Core/Telemetry

  Bishop.Worker
  ├── Core/Contracts
  ├── Core/Data
  │   └── Core/Contracts
  ├── Core/ServiceDefaults
  │   ├── Core/Logging
  │   └── Core/Telemetry
  └── Core/Tools
      ├── Core/Contracts
      └── Core/Data
```

Api and Worker (both in `Hosts/`) share everything in `Core/` but never reference each other.
The Api talks to workflows via untyped Temporal handles (string-based names).
Tools is isolated from the Worker - the Worker owns Temporal activities, Tools owns handlers.
Console talks to the Api via `Muthur.Clients` - never references Hosts or Aspire directly.

## Data Flow

### Agent Conversation (animation steps 1–6)

```
1. User sends prompt → Temporal (signal)
2. Temporal dispatches → Doc Worker (workflow)
3. Doc Worker calls → LLM (activity)
4. Doc Worker calls → Tool Registry (extract_pdf_text, store_document)
5. Tool writes document → PostgreSQL
6. Response returned → User
```

### Vectorize Pipeline (child workflow)

```
Agent calls store_document tool
  → DocumentStoreHandler → INSERT into documents table (fast, one activity)
  → Response returned to user immediately
  → AgentWorkflow detects store_document result
  → Forks DocumentIngestionWorkflow as child (ParentClosePolicy.Abandon)
    → IngestionActivities.ChunkTextAsync (~500-token chunks with overlap)
    → IngestionActivities.GenerateEmbeddingsAsync (IEmbeddingGenerator → embedding model)
    → IngestionActivities.StoreChunksAsync (bulk INSERT with pgvector embeddings)
    → Document cached in Redis
```

Note: The animation calls this the "Vec Worker" / "Vectorize" pipeline.
The code type is `DocumentIngestionWorkflow` — same thing, different lens.

### Vectorize Pipeline (animation steps 7–10)

```
7. Temporal forks → Vec Worker (child workflow, background)
8. Vec Worker stores vectors → PostgreSQL (pgvector)
9. Vec Worker calls → embedding model (LLM activity)
10. Vec Worker caches → Redis
```

### Document Search (API, not animated)

```
GET /v1/documents/search?q=...
  → Api generates query embedding via IEmbeddingGenerator
  → DocumentRepository.SearchSimilarAsync (pgvector cosine similarity via HNSW index)
  → Returns ranked chunks with document titles and scores
```

## Aspire Resources (local dev)

| Resource | Image | Persistent | Health Check |
|----------|-------|-----------|--------------|
| `muthur-temporal-dev` | `temporalio/admin-tools:latest` | Yes | HTTP on UI port (8233) |
| `muthur-postgres` / `muthur-db` | `pgvector/pgvector:pg17` | Yes | TCP |
| `muthur-cache` | Redis | Yes | TCP |

## Ports (local dev)

| Service | Port | Source |
|---------|------|--------|
| Aspire Dashboard | 15137 | launchSettings.json |
| Temporal gRPC | dynamic | Aspire container mapping |
| Temporal UI | dynamic | Aspire container mapping |
| Postgres | dynamic | Aspire container mapping |
| Redis | dynamic | Aspire container mapping |
| Muthur.Api | dynamic | Aspire-assigned |
