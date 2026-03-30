# MU-TH-UR 6000

A durable AI agent that runs inside [Temporal](https://temporal.io), calls tools, survives crashes, stores documents with vector embeddings, and picks up where it left off.

Built with the Temporal .NET SDK, [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions), Postgres + pgvector, Redis, and .NET Aspire. Ships with two tools: PDF text extraction via [PdfPig](https://github.com/UglyToad/PdfPig) and document storage with vector search.

Companion repo to the [threadunsafe.dev article](https://threadunsafe.dev/articles/inference/temporal-ai-agent).

## Quick start

```bash
# Clone
git clone https://github.com/screaming-in-space/mu-th-ur-6000.git
cd mu-th-ur-6000

# Add your API key (OpenAI, Anthropic, or any OpenAI-compatible endpoint)
dotnet user-secrets --project src/Muthur.Bishop.Worker set "AI:ApiKey" "sk-..."

# Run - starts Docker, Temporal, Postgres, Redis, Worker, and API via Aspire
dotnet run --project src/Muthur.AppHost
```

Requires Docker Desktop (auto-launched if not running) and .NET 10 SDK.

## What happens

1. Aspire starts three containers (Temporal, Postgres with pgvector, Redis) and waits for health checks
2. The Doc Worker connects to Temporal and registers both workflows
3. The API exposes endpoints for agent sessions and document access
4. Each prompt enters the agentic loop: LLM → tool decision → tool execution → back to LLM → until done
5. Every LLM call and every tool call is a Temporal activity checkpoint
6. When the agent stores a document, the response goes back to the user immediately
7. In the background, Temporal forks a vectorize child workflow that chunks the text, calls the embedding model, and stores vectors in pgvector for semantic search

## Try it

```bash
# Start a session
curl -X POST http://localhost:<api-port>/v1/agent/sessions \
  -H "Content-Type: application/json" \
  -d '{"systemPrompt": "You are a research assistant. Extract PDFs and store them for search."}'

# Send a prompt
curl -X POST http://localhost:<api-port>/v1/agent/sessions/<agent-id>/prompt \
  -H "Content-Type: application/json" \
  -d '{"content": "Extract text from /path/to/paper.pdf and store it in the knowledge base."}'

# Check state
curl http://localhost:<api-port>/v1/agent/sessions/<agent-id>

# Search stored documents
curl "http://localhost:<api-port>/v1/documents/search?q=neural+network+latency&limit=5"

# List all documents
curl http://localhost:<api-port>/v1/documents
```

The API port is assigned by Aspire - check the dashboard at `http://localhost:15137`.

## Architecture

```
Muthur.AppHost             Aspire orchestration - Temporal + Postgres + Redis containers
Aspire.Hosting.Temporal    Temporal dev server as Aspire resource + Docker launcher
Muthur.Api                 Minimal API - agent + document endpoints
Muthur.Bishop.Worker       Temporal worker - AgentWorkflow + DocumentIngestionWorkflow
Muthur.Tools               Agent tools - PDF extraction + document storage (isolated)
Muthur.Data                Postgres + Redis - repository, vector search, caching, migrations
Muthur.Contracts           Shared records - no dependencies
Muthur.ServiceDefaults     M.E.AI pipeline + Aspire defaults + Serilog
Muthur.Logging             Structured logging - console + OTLP, microsecond timestamps
```

## Temporal patterns demonstrated

| Pattern | Where |
|---------|-------|
| **Signals** | `AgentWorkflow.SendPromptAsync` - async prompt delivery |
| **Queries** | `AgentWorkflow.GetState` - read-only state access |
| **ContinueAsNew** | After 50 turns - fresh event history, agent keeps running |
| **Child workflows** | `DocumentIngestionWorkflow` - chunk → embed → store, fire-and-forget |

## Configuration

Set via user secrets or environment variables on the Worker:

| Key | Default | Purpose |
|-----|---------|---------|
| `AI:Provider` | `openai` | `openai`, `anthropic`, or any OpenAI-compatible |
| `AI:Model` | `gpt-4.1` | Chat model name |
| `AI:EmbeddingModel` | `text-embedding-3-small` | Embedding model (1536 dimensions) |
| `AI:ApiKey` | - | API key (required) |
| `AI:Endpoint` | - | Custom endpoint URL (optional) |
| `Logging:Format` | - | Set to `json` for structured JSON output |

## Adding a tool

1. Create a handler class in `Muthur.Tools/Handlers/`
2. Register it in `ToolRegistry` - `AIFunctionFactory.Create()` for LLM schema + `_handlers` for dispatch
3. If it needs DI, add constructor injection and register in Worker's `Program.cs`
4. Done. No changes to `AgentWorkflow` or `ToolActivities`.

## API endpoints

### Agent

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/v1/agent/sessions` | Start new agent workflow |
| `POST` | `/v1/agent/sessions/{id}/prompt` | Send prompt via Temporal signal |
| `GET` | `/v1/agent/sessions/{id}` | Query agent state |

### Documents

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/v1/documents` | List stored documents (Redis-cached) |
| `GET` | `/v1/documents/{id}` | Get document metadata (Redis-cached) |
| `GET` | `/v1/documents/{id}/content` | Get full document text |
| `GET` | `/v1/documents/search?q=...&limit=5` | Vector similarity search |

## Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Temporalio.Extensions.Hosting` | 1.12.0 | Temporal .NET SDK + hosted worker |
| `Microsoft.Extensions.AI` | 9.7.0 | `IChatClient` + `IEmbeddingGenerator` |
| `Microsoft.Extensions.AI.OpenAI` | 10.4.1 | OpenAI-compatible provider |
| `PdfPig` | 0.1.15-alpha | PDF text extraction |
| `Pgvector` | 0.3.2 | pgvector type mapping for Npgsql |
| `Dapper` | 2.1.66 | SQL queries |
| `Aspire.Hosting.AppHost` | 13.2.0 | Aspire orchestration |
| `Aspire.Npgsql` | 13.2.0 | Postgres via Aspire |
| `Aspire.StackExchange.Redis.DistributedCaching` | 13.2.0 | Redis cache via Aspire |

## License

MIT
