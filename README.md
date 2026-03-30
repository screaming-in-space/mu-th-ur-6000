# MU-TH-UR 6000

A durable AI agent that runs inside [Temporal](https://temporal.io), calls tools, survives crashes, and picks up where it left off.

Built with the Temporal .NET SDK, [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions), and .NET Aspire. Ships with one practical tool: PDF text extraction via [PdfPig](https://github.com/UglyToad/PdfPig).

Companion repo to the [threadunsafe.dev article](https://threadunsafe.dev/articles/inference/temporal-ai-agent).

## Quick start

```bash
# Clone
git clone https://github.com/screaming-in-space/mu-th-ur-6000.git
cd mu-th-ur-6000

# Add your API key (OpenAI, Anthropic, or any OpenAI-compatible endpoint)
dotnet user-secrets --project src/Muthur.Bishop.Worker set "AI:ApiKey" "sk-..."

# Run — starts Docker, Temporal container, Worker, and API via Aspire
dotnet run --project src/Muthur.AppHost
```

Requires Docker Desktop (auto-launched if not running) and .NET 10 SDK.

## What happens

1. Aspire starts a Temporal dev server container and waits for it to be healthy
2. The Worker connects and registers the `AgentWorkflow`
3. The API exposes three endpoints for creating sessions, sending prompts, and querying state
4. Each prompt enters the agentic loop: LLM call &rarr; tool decision &rarr; tool execution &rarr; back to LLM &rarr; until done
5. Every LLM call and every tool call is a Temporal activity checkpoint — if the process crashes, only the in-flight activity re-executes

## Try it

```bash
# Start a session
curl -X POST http://localhost:<api-port>/v1/agent/sessions \
  -H "Content-Type: application/json" \
  -d '{"systemPrompt": "You are a research assistant with access to PDF extraction."}'

# Send a prompt
curl -X POST http://localhost:<api-port>/v1/agent/sessions/<agent-id>/prompt \
  -H "Content-Type: application/json" \
  -d '{"content": "Extract the text from /path/to/paper.pdf and summarize the key findings."}'

# Check state
curl http://localhost:<api-port>/v1/agent/sessions/<agent-id>
```

The API port is assigned by Aspire — check the dashboard at `http://localhost:15137`.

## Architecture

```
Muthur.AppHost          Aspire orchestration — Temporal container + project wiring
Aspire.Hosting.Temporal Temporal dev server as Aspire container resource
Muthur.Api              Minimal API — 3 endpoints, untyped Temporal handles
Muthur.Bishop.Worker    Temporal worker — AgentWorkflow + LLM/Tool activities
Muthur.Contracts        Shared records — no dependencies
Muthur.ServiceDefaults  M.E.AI IChatClient pipeline + Aspire service defaults
```

The API and Worker share only `Muthur.Contracts`. The API never references Worker types — it talks to workflows via string-based Temporal handles.

## Configuration

Set via user secrets or environment variables on the Worker:

| Key | Default | Purpose |
|-----|---------|---------|
| `AI:Provider` | `openai` | `openai`, `anthropic`, or any OpenAI-compatible |
| `AI:Model` | `gpt-4o` | Model name |
| `AI:ApiKey` | — | API key (required) |
| `AI:Endpoint` | — | Custom endpoint URL (optional, for LM Studio / Ollama) |
| `Temporal:Address` | from Aspire | Override Temporal host:port |
| `Temporal:Namespace` | `default` | Temporal namespace |

## Adding a tool

1. Write the handler — a static async method that takes a JSON arguments string and returns a string
2. Register it in `ToolRegistry.cs` with `AIFunctionFactory.Create()` and a handler mapping
3. Done. The workflow dispatches by name. No changes to `AgentWorkflow` or `ToolActivities`.

## What this is and isn't

**Is:** A working skeleton you can extend — clone, add your API key, run.

**Isn't:** Production infrastructure. Missing: auth, rate limiting, context persistence, monitoring dashboards. The agent loop itself is production-ready. The plumbing around it is not.

## Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Temporalio.Extensions.Hosting` | 1.12.0 | Temporal .NET SDK + hosted worker |
| `Microsoft.Extensions.AI` | 9.7.0 | `IChatClient` abstraction |
| `Microsoft.Extensions.AI.OpenAI` | 10.4.1 | OpenAI-compatible provider |
| `PdfPig` | 0.1.15-alpha | PDF text extraction (namespace: `UglyToad.PdfPig`) |
| `Aspire.Hosting.AppHost` | 13.2.0 | Aspire orchestration |

## License

MIT
