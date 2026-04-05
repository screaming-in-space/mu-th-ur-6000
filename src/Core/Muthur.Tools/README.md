# Muthur.Tools

Agent tools, isolated from the Temporal Worker for independent testability.

## Architecture

Each tool has two layers:

```
Domain Logic          → Pdf/PdfExtractor.cs, Documents/DocumentStore.cs
Handler + AIFunction  → Handlers/PdfHandler.cs, Handlers/DocumentStoreHandler.cs
Registry + Dispatch   → ToolRegistry.cs
```

**Domain logic** is pure — typed inputs, typed outputs, no JSON, no tool plumbing. Directly unit-testable.

**Handlers** expose a single typed method with `[Description]` attributes. This method serves as both the LLM schema (via `AIFunctionFactory.Create`) and the runtime execution path (via `AIFunction.InvokeAsync`). One code path, not two.

**ToolRegistry** auto-collects all `IToolHandler` implementations via DI, dispatches by name through `AIFunction.InvokeAsync` with distributed tracing (`MuthurTrace` spans), and records `ToolExecutions` metrics.

## Tools

| Tool | Handler | Domain | Purpose |
|------|---------|--------|---------|
| `pdf_extract_text` | `PdfHandler` | `PdfExtractor` | Extract text + metadata from PDF via PdfPig |
| `store_document` | `DocumentStoreHandler` | `DocumentStore` | Persist document text to Postgres |

## Adding a tool

1. Create domain logic in a new folder (e.g., `Search/SearchEngine.cs`) — typed in, typed out
2. Create handler in `Handlers/` implementing `IToolHandler`:
   - Public typed method with `[Description]` attributes — calls domain directly
   - `Register()` — returns `ToolRegistration(name, AIFunctionFactory.Create(TypedMethod, name))`
3. Add tool name constant to `AgentConstants`
4. Register in `ToolsExtensions.AddMuthurTools()`:
   ```csharp
   builder.Services.AddSingleton<SearchEngine>();
   builder.Services.AddSingleton<SearchHandler>();
   builder.Services.AddSingleton<IToolHandler>(sp => sp.GetRequiredService<SearchHandler>());
   ```

ToolRegistry, ToolActivities, and AgentWorkflow don't change.

### Workflow-injected parameters

Some tools receive parameters that the LLM doesn't provide — the workflow injects them (e.g., `store_document` gets `text` and `metadata` from the extraction cache). Use `AIFunctionFactoryOptions.ConfigureParameterBinding` with `ExcludeFromSchema = true` to keep these out of the LLM schema while still binding them from the arguments dictionary at invocation time.

## Key Types

| Type | Purpose |
|------|---------|
| `IToolHandler` | Contract — handlers implement this, registry auto-collects via DI |
| `ToolRegistration` | Pairs a tool name with its `AIFunction` |
| `ToolResult` | Typed result — both serialized JSON (Temporal boundary) and typed payload (in-process) |
| `ToolRegistry` | Auto-collects handlers, dispatches via `AIFunction.InvokeAsync` with tracing and metrics |

## Dependencies

- `Muthur.Contracts` — shared types (`AgentConstants`, `PdfExtractionResult`, `StoreDocumentResult`, `SerializerDefaults`)
- `Muthur.Data` — `IDocumentRepository` for document storage
- `Muthur.Telemetry` — `MuthurTrace` spans, `MuthurMetrics` counters
- `Microsoft.Extensions.AI` — `AIFunctionFactory`, `AIFunction`, `AITool`, `AIFunctionFactoryOptions`
- `CommunityToolkit.HighPerformance` — `ArrayPoolBufferWriter` for PDF extraction
- `PdfPig` — PDF text extraction
