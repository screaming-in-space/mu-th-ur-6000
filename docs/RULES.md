# Rules

Technical constraints and rejected patterns for mu-th-ur-6000.

## Design Principles

These apply everywhere. No exceptions.

- **Single Responsibility** — one class, one reason to change. One extension method, one registration path.
- **DRY** — one code path to each infrastructure concern. If `AddNpgsqlDataSource` is called in two places, one of them will drift. The one that drifts will be the one production uses.
- **KISS** — the simplest correct solution. If a named HttpClient works, don't use a typed client that conflicts with another DI registration.
- **YAGNI** — don't build it until you need it. Don't add a persistence lifetime until you've proven the password survives restarts.

When a bug is found, check whether the fix is in one place or scattered. If scattered, consolidate first, then fix. Tests must exercise the same code path production uses — a test that bypasses a registration is a false green.

## Stack

- .NET 10 / C# 14.0 - `LangVersion 14.0`, nullable enabled, implicit usings
- Central Package Management (`Directory.Packages.props`) - all NuGet versions are pinned centrally
- Temporal .NET SDK (`Temporalio.Extensions.Hosting` 1.12.0)
- Microsoft.Extensions.AI 10.4.1 / Microsoft.Extensions.AI.OpenAI 10.4.1
- PdfPig 0.1.15-alpha (NuGet package ID `PdfPig`, namespace `UglyToad.PdfPig`)
- Postgres with pgvector (`pgvector/pgvector:pg17` Docker image, `Pgvector` 0.3.2 NuGet)
- Redis via Aspire (`Aspire.StackExchange.Redis.DistributedCaching` 13.2.1)
- Dapper 2.1.72 for SQL queries (not EF Core)
- .NET Aspire 13.2.1 for orchestration
- Docker Desktop required for local dev (Temporal, Postgres, Redis run as containers)

## Temporal

### Do

- One activity per side effect. Each LLM call and each tool call is its own `ExecuteActivityAsync` - a separate Temporal checkpoint.
- Use `ContinueAsNew` after N turns (default 50). Event histories grow unbounded otherwise.
- Each turn builds conversation history locally in `ProcessPromptAsync` from the single prompt in the signal. The workflow is stateless — history is not carried across turns or in workflow state.
- Use `WaitConditionAsync` for signal-driven loops. Never `Task.Delay` to poll.
- Use `RetryPolicy` on tool activities. LLM activities get a single attempt with a long timeout.
- Use scoped activities (`AddScopedActivities<T>`) for DI - each execution gets a fresh scope.
- Use child workflows for multi-step background work (the vectorize pipeline). Set `ParentClosePolicy.Abandon` so child survives parent `ContinueAsNew`. Return the response to the user before forking the child.
- Give child workflows deterministic IDs (`ingest-{documentId}`) for idempotency.
- Use activity chaining to keep work running on a warm worker — avoids repeated scheduling overhead for sequential steps.
- Use long-running activities for work that must execute continuously (e.g., batch processing). The scheduling overhead (~50ms) is at the dispatch boundary, not inside the activity — once running, execution is at language speed.

### Don't

- Don't use `FunctionInvokingChatClient` inside a Temporal activity. It collapses all tool calls into one activity and you lose per-tool durability checkpoints.
- Don't hold `IChatClient` state across activity boundaries. Activities are stateless.
- Don't use `Thread.Sleep` or `Task.Delay` in workflow code. Use `Workflow.DelayAsync`.
- Don't call non-deterministic code (DateTime.Now, Guid.NewGuid, HTTP) directly in workflows. Wrap in activities.
- Don't reference Worker types from the Api project. Use untyped Temporal handles (string-based workflow/signal names).
- Don't use `with` expressions inside `Workflow.CreateContinueAsNewException` lambdas - they're expression trees. Create local variables first.
- Don't block the agent conversation on child workflow completion. Return the response first, then fork the vectorize pipeline with `Abandon`.

## Temporal Troubleshooting

- **Signal name mismatch is silent.** The .NET SDK strips the `Async` suffix from `[WorkflowSignal]` method names. `SendPromptAsync` registers as `"SendPrompt"`. If you call `handle.SignalAsync("SendPromptAsync", ...)`, the signal is delivered, accepted by the server, visible in the event history — and silently dropped. No error, no log. The workflow sits on `WaitConditionAsync` forever. Always use the stripped name: `"SendPrompt"`, not `"SendPromptAsync"`.
- **Same applies to queries.** `GetState` not `GetStateAsync` (though our query method isn't async, so it doesn't have the suffix — but be aware of the convention).
- **Put signal/query names in constants.** `AgentConstants` already owns the task queue name. Signal and query names should live there too. Type them once, reference everywhere.
- **`BackgroundServiceExceptionBehavior.Ignore`** means hosted services (including migrations and the Temporal worker) won't crash the host on failure. This is correct — but it means errors are only visible in logs. If the Worker starts but never processes workflows, check logs for connection errors.
- **`AddTemporalClient` must use the two-arg overload** `(connectionString, namespace)` — not the options-callback overload. The hosted worker expects the client to be fully configured via this path.

## M.E.AI / IChatClient / VectorData

### Priority order for AI abstractions

1. **Microsoft.Extensions.AI** (`IChatClient`, `IEmbeddingGenerator`, `AIFunctionFactory`) - first-party dotnet/extensions abstractions. Use these for all new LLM and embedding work.
2. **Microsoft.Extensions.VectorData** (`VectorStore`, `VectorStoreCollection<TKey, TRecord>`) - first-party vector store abstractions. Stable at 10.1.0. Provider packages are named `Microsoft.SemanticKernel.Connectors.*` but they implement M.E.AI interfaces and have no dependency on SK orchestration. Use these when you want annotated model-based vector stores instead of raw SQL.
3. **Microsoft Agent Framework** - multi-agent graph orchestration built on M.E.AI. Use for multi-agent coordination when the project needs it.
4. **Semantic Kernel** - still actively maintained, not deprecated. SK plugins are now `AIFunction` instances under the hood. For greenfield code, prefer M.E.AI abstractions directly - they're the foundation SK builds on. For existing SK codebases, there's no urgency to rewrite.

### Do

- Register `IChatClient` via `ChatClientBuilder` pipeline with `UseOpenTelemetry` and `UseLogging`.
- Register `IEmbeddingGenerator<string, Embedding<float>>` alongside `IChatClient` in `AiClientExtensions.cs`.
- Use `AIFunctionFactory.Create()` with `[Description]` attributes for tool registration.
- Read provider, model, API key, and endpoint from configuration (`AI:Provider`, `AI:Model`, `AI:ApiKey`, `AI:Endpoint`, `AI:EmbeddingModel`).
- Keep the pipeline registration in one place (`AiClientExtensions.cs` in ServiceDefaults).
- Consider `Microsoft.Extensions.VectorData` + `Microsoft.SemanticKernel.Connectors.PgVector` as an alternative to raw Dapper for vector store operations. The connector handles table creation, HNSW indexing, and search with annotated C# models. This repo uses Dapper for explicitness; the VectorData abstraction is a valid production path.

### Don't

- Don't add `FunctionInvokingChatClient` to the pipeline - the agentic loop in the workflow handles tool dispatch explicitly.
- Don't hardcode provider-specific API URLs. Use the provider switch pattern.

## PdfPig

### Do

- Use `PdfDocument.Open(path)` - returns pages, text, and metadata from pure managed code.
- Extract metadata from `document.Information` (Title, Author, Subject, Creator).
- Use `page.Text` for text extraction. It handles most standard PDF encodings.
- Serialize results as `PdfExtractionResult` with text, page count, and metadata dict.

### Don't

- Don't use the package name `UglyToad.PdfPig` on NuGet - the actual package ID is `PdfPig`. The namespace is `UglyToad.PdfPig`.
- Don't hold `PdfDocument` open longer than needed - wrap in `using`.
- Don't assume all PDFs have extractable text. Scanned documents return empty pages.

## Postgres + pgvector

### Do

- Use the `pgvector/pgvector:pg17` Docker image - ships with the vector extension pre-installed.
- Register `NpgsqlDataSource` through `AddMuthurPostgreSql()` in `Muthur.PostgreSql` — the single code path for Npgsql configuration, `PersistSecurityInfo`, and DbUp migrations. Never call `AddNpgsqlDataSource()` directly from other projects.
- `UseVector()` is configured via the `configureDataSource` callback. The extension is in the `Npgsql` namespace (not `Pgvector.Npgsql`).
- Register `Pgvector.Dapper.VectorTypeHandler` via `SqlMapper.AddTypeHandler(new VectorTypeHandler())` for Dapper compatibility.
- Use `vector(768)` columns for OpenAI `text-embedding-3-small` embeddings.
- Use HNSW index (`USING hnsw ... vector_cosine_ops`) for approximate nearest neighbor search.
- Use `1 - (embedding <=> @Embedding)` for cosine similarity score (0 = orthogonal, 1 = identical).
- Keep migrations idempotent (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`).
- Use Dapper + raw SQL for queries. No EF Core in this repo.

### Don't

- Don't use the default Aspire Postgres image - it lacks the vector extension. Always specify `pgvector/pgvector:pg17`.
- Don't use `Pgvector.Npgsql` as a NuGet package name - it doesn't exist. The extension lives in the `Pgvector` package under the `Npgsql` namespace.
- Don't use EF Core. This repo uses Dapper for all data access.
- Don't store embeddings as `float[]` in Postgres - use `Pgvector.Vector` wrapper type.

## Redis

### Do

- Use `IDistributedCache` for document caching (via `Aspire.StackExchange.Redis.DistributedCaching`).
- Use the `CachedDocumentRepository` decorator pattern - cache reads, invalidate on writes.
- Set reasonable TTLs (15 minutes default for document lookups).
- Invalidate list caches on writes (`cache.RemoveAsync("docs:list")`).

### Don't

- Don't cache vector search results - they change as new documents are ingested.
- Don't cache full document content - only metadata and summaries.

## Aspire

### Do

- Use `AddTemporalDevServer` to run Temporal as an Aspire-managed container resource.
- Use `AddPostgres().WithImage("pgvector/pgvector").WithImageTag("pg17").AddDatabase()` for Postgres with pgvector. No `ContainerLifetime.Persistent` — see above.
- Use `AddRedis()` for the cache layer.
- Use `.WithReference().WaitFor()` on both Worker and Api for all infrastructure resources.
- Use `WithLifetime(ContainerLifetime.Persistent)` only on containers without password-based auth (Redis, Temporal). Postgres with auto-generated passwords will fail on restart because the data volume retains the old password while Aspire generates a new one. Let Postgres recreate each session — the demo re-ingests data on every run anyway.
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
- Register tools in `ToolRegistry` constructor - `AIFunctionFactory.Create()` for LLM schema + `_handlers` dict for dispatch.
- Tools that need DI (e.g., `DocumentStoreHandler` needs `IDocumentRepository`) take dependencies via constructor injection.
- Static handlers (e.g., `PdfHandler`) don't need DI - call directly.

### Don't

- Don't put tool handlers in the Worker project. The Worker owns Temporal activities; Tools owns handlers.
- Don't change `AgentWorkflow` or `ToolActivities` when adding a new tool. Only touch `ToolRegistry`.

## Debugging

- **Decompose first, guess never.** When something isn't working end-to-end, don't keep restarting the full system hoping for different output. Write the smallest possible integration test that isolates the suspect layer. The test that finds the bug in 2 seconds is always better than the 47th AppHost restart.
- **The integration test harness exists — use it.** `Muthur.Integration.Tests` has a shared Aspire fixture with live Temporal, Postgres, Redis, API, and Worker. Write a focused test against the specific layer that's failing: Temporal signals, repo queries, API routes, LLM calls. Run it. Read the assertion failure. Fix the code.
- **Strip the stack.** If the workflow isn't processing, don't debug through Console → API → Temporal → Worker. Connect a `TemporalClient` directly, start a workflow, signal it, query it. If that works, the problem is upstream. If it doesn't, the problem is in the workflow. Binary search, not full-stack prayer.

## NuGet

- **Central Package Management** - all NuGet versions are pinned in `Directory.Packages.props` at the repo root. Individual `.csproj` files use `<PackageReference Include="..." />` without `Version`. To update a package version, edit `Directory.Packages.props` — never add `Version` to a csproj.
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
