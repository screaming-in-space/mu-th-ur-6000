# Muthur.Contracts

Shared types. Zero dependencies. Referenced by Api, Worker, Data, and Tools.

## Agent Records

| Type | Purpose |
|------|---------|
| `AgentWorkflowInput` | Workflow input — agent ID and optional system prompt |
| `LlmActivityInput` | Conversation messages + system prompt for LLM activity |
| `LlmActivityOutput` | LLM response text + extracted tool call requests |
| `ToolCallRequest` | Tool call ID, name, and `Dictionary<string, object?>` arguments |
| `ConversationMessage` | Role + content + optional tool calls/ID |
| `PromptSignal` | Signal payload — user content + optional system prompt override |
| `AgentState` | Query result — is processing, turn count, last response |
| `PdfExtractionResult` | Extracted text, page count, metadata dictionary |

## Document Records

| Type | Purpose |
|------|---------|
| `DocumentRecord` | Full document metadata for API responses |
| `DocumentSummary` | Lightweight summary for list endpoints |
| `SimilarChunk` | Vector search result — chunk text, document ID, title, similarity score |
| `StoreDocumentResult` | Tool result — stored document ID |
| `DocumentIngestionInput` | Input for the ingestion child workflow |
| `TextChunk` | A text chunk with its position index |

## Tool Result Processing

| Type | Purpose |
|------|---------|
| `ToolResultProcessor` | Pure functions for extraction caching, argument enrichment, ingestion input parsing |
| `ExtractionCacheResult` | Result of caching a PDF extraction — LLM summary + optional parsed extraction |

## Constants & Utilities

| Type | Purpose |
|------|---------|
| `AgentConstants` | Task queue, role strings, tool names, 50-turn ContinueAsNew threshold, workflow ID factory |
| `SerializerDefaults` | Shared `JsonSerializerOptions` for case-insensitive property matching |

## Why this project exists

The Api needs to construct `PromptSignal` and read `AgentState` without referencing the Worker. The Data project needs `DocumentRecord` and `TextChunk` without referencing the Worker. `ToolResultProcessor` contains pure functions extracted from AgentWorkflow for testability — no Temporal dependencies. Contracts is the shared language between all projects.
