# Muthur.Tools

Agent tools, isolated from the Temporal Worker for independent testability.

## Tools

| Tool | Handler | DI Required | Purpose |
|------|---------|-------------|---------|
| `extract_pdf_text` | `PdfHandler` (static) | No | Extract text + metadata from PDF via PdfPig |
| `store_document` | `DocumentStoreHandler` | Yes (`IDocumentRepository`) | Persist document text to Postgres |

## How it works

`ToolRegistry` is the central dispatch:
- Constructor registers each tool with `AIFunctionFactory.Create()` (generates JSON schema for LLM discovery) and a `_handlers` dictionary (maps name → async handler delegate)
- `GetTools()` returns `IReadOnlyList<AITool>` for `ChatOptions.Tools`
- `GetHandler(name)` returns the handler delegate for `ToolActivities` dispatch

## Adding a tool

1. Create a handler class in `Handlers/` (or static method for stateless tools)
2. In `ToolRegistry` constructor:
   - Add `_handlers["my_tool"] = myHandler.DoWorkAsync;`
   - Add `_tools.Add(AIFunctionFactory.Create(..., "my_tool"));` with `[Description]` attributes
3. If the handler needs DI, register it in Worker's `Program.cs` and add it to `ToolRegistry`'s constructor

The Worker's `ToolActivities` and `AgentWorkflow` don't change.

## Dependencies

- `Muthur.Contracts` - shared types
- `Muthur.Data` - `IDocumentRepository` for document storage
- `Microsoft.Extensions.AI` - `AIFunctionFactory`, `AITool`
- `PdfPig` - PDF text extraction
