# Muthur.Tools

Agent tools, isolated from the Temporal Worker for independent testability.

## Architecture

Each tool has three layers:

```
Domain Logic          → Pdf/PdfExtractor.cs, Documents/DocumentStore.cs
Handler Bridge        → Handlers/PdfHandler.cs, Handlers/DocumentStoreHandler.cs
Registry + Dispatch   → ToolRegistry.cs
```

**Domain logic** is pure — typed inputs, typed outputs, no JSON, no tool plumbing. Directly unit-testable.

**Handler bridges** own two code paths:
- **JSON bridge** (`StoreAsync(string, ToolExecutionContext)`) — deserializes args, calls domain, serializes result as `ToolResult`. Used by Temporal activity dispatch.
- **LLM definition** (private method with `[Description]` attributes) — calls domain directly with typed parameters. Used by `AIFunctionFactory.Create` for LLM tool schema generation.

**ToolRegistry** auto-collects all `IToolHandler` implementations via DI, dispatches by name with distributed tracing (`MuthurTrace` spans), and records `ToolExecutions` metrics.

## Tools

| Tool | Handler | Domain | Purpose |
|------|---------|--------|---------|
| `extract_pdf_text` | `PdfHandler` | `PdfExtractor` | Extract text + metadata from PDF via PdfPig |
| `store_document` | `DocumentStoreHandler` | `DocumentStore` | Persist document text to Postgres |

## Adding a tool

1. Create domain logic in a new folder (e.g., `Search/SearchEngine.cs`) — typed in, typed out
2. Create handler in `Handlers/` implementing `IToolHandler`:
   - `Register()` — wire handler + `AIFunctionFactory.Create(TypedMethod, ToolName)`
   - JSON bridge method — deserialize, call domain, return `ToolResult.From(result)`
   - Private typed method with `[Description]` attributes — call domain directly
3. Add tool name constant to `AgentConstants`
4. Register in `ToolsExtensions.AddMuthurTools()`:
   ```csharp
   builder.Services.AddSingleton<SearchEngine>();
   builder.Services.AddSingleton<SearchHandler>();
   builder.Services.AddSingleton<IToolHandler>(sp => sp.GetRequiredService<SearchHandler>());
   ```

ToolRegistry, ToolActivities, and AgentWorkflow don't change.

## Key Types

| Type | Purpose |
|------|---------|
| `IToolHandler` | Contract — handlers implement this, registry auto-collects via DI |
| `ToolExecutionContext` | Carries correlation metadata (tool name, agent ID, call ID, cancellation token) |
| `ToolResult` | Typed result — both serialized JSON (Temporal boundary) and typed payload (in-process) |
| `ToolRegistry` | Auto-collects handlers, dispatches with tracing and metrics |

## Dependencies

- `Muthur.Contracts` — shared types (`ExtractPdfArgs`, `StoreDocumentArgs`, `StoreDocumentResult`, `SerializerDefaults`)
- `Muthur.Data` — `IDocumentRepository` for document storage
- `Muthur.Telemetry` — `MuthurTrace` spans, `MuthurMetrics` counters
- `Microsoft.Extensions.AI` — `AIFunctionFactory`, `AITool`
- `CommunityToolkit.HighPerformance` — `ArrayPoolBufferWriter` for PDF extraction
- `PdfPig` — PDF text extraction
