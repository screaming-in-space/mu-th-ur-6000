# Rules

Technical constraints and rejected patterns for mu-th-ur-6000.

## Stack

- .NET 10 / C# 14.0 — `LangVersion 14.0`, nullable enabled, implicit usings
- Temporal .NET SDK (`Temporalio.Extensions.Hosting` 1.12.0)
- Microsoft.Extensions.AI 9.7.0 / Microsoft.Extensions.AI.OpenAI 10.4.1
- PdfPig 0.1.15-alpha (NuGet package ID `PdfPig`, namespace `UglyToad.PdfPig`)
- Postgres with pgvector (`pgvector/pgvector:pg17` Docker image, `Pgvector` 0.3.2 NuGet)
- Redis via Aspire (`Aspire.StackExchange.Redis.DistributedCaching` 13.2.0)
- Dapper 2.1.66 for SQL queries (not EF Core)
- .NET Aspire 13.2.0 for orchestration
- Docker Desktop required for local dev (Temporal, Postgres, Redis run as containers)

## Temporal

### Do

- One activity per side effect. Each LLM call and each tool call is its own `ExecuteActivityAsync` — a separate Temporal checkpoint.
- Use `ContinueAsNew` after N turns (default 50). Event histories grow unbounded otherwise.
- Carry conversation history in signals, not workflow state. The workflow is stateless.
- Use `WaitConditionAsync` for signal-driven loops. Never `Task.Delay` to poll.
- Use `RetryPolicy` on tool activities. LLM activities get a single attempt with a long timeout.
- Use scoped activities (`AddScopedActivities<T>`) for DI — each execution gets a fresh scope.
- Use child workflows for multi-step background work (document ingestion). Set `ParentClosePolicy.Abandon` so child survives parent `ContinueAsNew`.
- Give child workflows deterministic IDs (`ingest-{documentId}`) for idempotency.

### Don't

- Don't use `FunctionInvokingChatClient` inside a Temporal activity. It collapses all tool calls into one activity and you lose per-tool durability checkpoints.
- Don't hold `IChatClient` state across activity boundaries. Activities are stateless.
- Don't use `Thread.Sleep` or `Task.Delay` in workflow code. Use `Workflow.DelayAsync`.
- Don't call non-deterministic code (DateTime.Now, Guid.NewGuid, HTTP) directly in workflows. Wrap in activities.
- Don't reference Worker types from the Api project. Use untyped Temporal handles (string-based workflow/signal names).
- Don't use `with` expressions inside `Workflow.CreateContinueAsNewException` lambdas — they're expression trees. Create local variables first.
- Don't block the agent conversation on child workflow completion. Fire-and-forget with `Abandon`.

## M.E.AI / IChatClient

### Do

- Register `IChatClient` via `ChatClientBuilder` pipeline with `UseOpenTelemetry` and `UseLogging`.
- Register `IEmbeddingGenerator<string, Embedding<float>>` alongside `IChatClient` in `AiClientExtensions.cs`.
- Use `AIFunctionFactory.Create()` with `[Description]` attributes for tool registration.
- Read provider, model, API key, and endpoint from configuration (`AI:Provider`, `AI:Model`, `AI:ApiKey`, `AI:Endpoint`, `AI:EmbeddingModel`).
- Keep the pipeline registration in one place (`AiClientExtensions.cs` in ServiceDefaults).

### Don't

- Don't use Semantic Kernel for new code. SK is in maintenance mode; M.E.AI + Agent Framework is the path forward.
- Don't add `FunctionInvokingChatClient` to the pipeline — the agentic loop in the workflow handles tool dispatch explicitly.
- Don't hardcode provider-specific API URLs. Use the provider switch pattern.

## PdfPig

### Do

- Use `PdfDocument.Open(path)` — returns pages, text, and metadata from pure managed code.
- Extract metadata from `document.Information` (Title, Author, Subject, Creator).
- Use `page.Text` for text extraction. It handles most standard PDF encodings.
- Serialize results as `PdfExtractionResult` with text, page count, and metadata dict.

### Don't

- Don't use the package name `UglyToad.PdfPig` on NuGet — the actual package ID is `PdfPig`. The namespace is `UglyToad.PdfPig`.
- Don't hold `PdfDocument` open longer than needed — wrap in `using`.
- Don't assume all PDFs have extractable text. Scanned documents return empty pages.

## Postgres + pgvector

### Do

- Use the `pgvector/pgvector:pg17` Docker image — ships with the vector extension pre-installed.
- Register `NpgsqlDataSource` via Aspire's `AddNpgsqlDataSource()` with `dsb.UseVector()`. The `UseVector()` extension is in the `Npgsql` namespace (not `Pgvector.Npgsql` — that's the internal namespace, not the import you need).
- Register `Pgvector.Dapper.VectorTypeHandler` via `SqlMapper.AddTypeHandler(new VectorTypeHandler())` for Dapper compatibility.
- Use `vector(1536)` columns for OpenAI `text-embedding-3-small` embeddings.
- Use HNSW index (`USING hnsw ... vector_cosine_ops`) for approximate nearest neighbor search.
- Use `1 - (embedding <=> @Embedding)` for cosine similarity score (0 = orthogonal, 1 = identical).
- Keep migrations idempotent (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`).
- Use Dapper + raw SQL for queries. No EF Core in this repo.

### Don't

- Don't use the default Aspire Postgres image — it lacks the vector extension. Always specify `pgvector/pgvector:pg17`.
- Don't use `Pgvector.Npgsql` as a NuGet package name — it doesn't exist. The extension lives in the `Pgvector` package under the `Npgsql` namespace.
- Don't use EF Core. This repo uses Dapper for all data access.
- Don't store embeddings as `float[]` in Postgres — use `Pgvector.Vector` wrapper type.

## Redis

### Do

- Use `IDistributedCache` for document caching (via `Aspire.StackExchange.Redis.DistributedCaching`).
- Use the `CachedDocumentRepository` decorator pattern — cache reads, invalidate on writes.
- Set reasonable TTLs (15 minutes default for document lookups).
- Invalidate list caches on writes (`cache.RemoveAsync("docs:list")`).

### Don't

- Don't cache vector search results — they change as new documents are ingested.
- Don't cache full document content — only metadata and summaries.

## Aspire

### Do

- Use `AddTemporalDevServer` to run Temporal as an Aspire-managed container resource.
- Use `AddPostgres().WithImage("pgvector/pgvector").WithImageTag("pg17").AddDatabase()` for Postgres with pgvector.
- Use `AddRedis()` for the cache layer.
- Use `.WithReference().WaitFor()` on both Worker and Api for all infrastructure resources.
- Use `WithLifetime(ContainerLifetime.Persistent)` on all containers — survives AppHost restarts.
- Use `EnsureDockerAsync()` in the AppHost to auto-launch Docker Desktop if not running.
- Mark non-service project references with `IsAspireProjectResource="false"` in the AppHost csproj.
- Use `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` in launch profiles for HTTP-only local dev.

### Don't

- Don't declare `Aspire.AppHost.Sdk` as a `PackageReference`. It's an `<Sdk>` element.
- Don't reference the Worker project from the Api project. They share Contracts and Data, not workflow types.

## Tools

### Do

- Keep tools in the `Muthur.Tools` project, isolated from the Worker.
- Each tool handler is a class in `Handlers/` with a single async method.
- Register tools in `ToolRegistry` constructor — `AIFunctionFactory.Create()` for LLM schema + `_handlers` dict for dispatch.
- Tools that need DI (e.g., `DocumentStoreHandler` needs `IDocumentRepository`) take dependencies via constructor injection.
- Static handlers (e.g., `PdfHandler`) don't need DI — call directly.

### Don't

- Don't put tool handlers in the Worker project. The Worker owns Temporal activities; Tools owns handlers.
- Don't change `AgentWorkflow` or `ToolActivities` when adding a new tool. Only touch `ToolRegistry`.

## NuGet

- The repo has a `nuget.config` with `<clear />` that removes all global sources (including private Azure DevOps feeds from other projects). Only `api.nuget.org` is configured.
- If you get NU1301 401 errors, the `<clear />` is missing or a global config is leaking.

## Naming

- Project names: `Muthur.*` (not `MuThUr`)
- Worker project: `Muthur.Bishop.Worker` (the middle name is deliberate)
- Temporal resource: `muthur-temporal-dev`
- Postgres resource: `muthur-postgres` / database: `muthur-db`
- Redis resource: `muthur-cache`
- Temporal task queue: `mu-th-ur-agent` (defined in `AgentConstants.TaskQueue`)
- Prose/article references: "MU-TH-UR 6000" (Alien canon form)
