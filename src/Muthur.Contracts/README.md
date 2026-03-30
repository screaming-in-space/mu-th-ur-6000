# Muthur.Contracts

Shared types. Zero dependencies. Referenced by Api, Worker, Data, and Tools.

## Agent Records

| Type | Purpose |
|------|---------|
| `AgentWorkflowInput` | Workflow input — agent ID and optional system prompt |
| `LlmActivityInput` | Conversation messages + system prompt for LLM activity |
| `LlmActivityOutput` | LLM response text + extracted tool call requests |
| `ToolCallRequest` | Tool call ID, name, and JSON arguments |
| `ToolCallResult` | Tool execution result string |
| `ConversationMessage` | Role + content + optional tool calls/ID |
| `PromptSignal` | Signal payload — user content + optional system prompt override |
| `AgentState` | Query result — is processing, turn count, last response |
| `PdfExtractionResult` | Extracted text, page count, metadata dictionary |

## Document Records

| Type | Purpose |
|------|---------|
| `DocumentRecord` | Full document metadata for API responses |
| `DocumentSummary` | Lightweight summary for list endpoints |
| `DocumentChunkRecord` | A chunk of text without its embedding |
| `SimilarChunk` | Vector search result — chunk text, document ID, title, similarity score |
| `DocumentIngestionInput` | Input for the ingestion child workflow |
| `TextChunk` | A text chunk with its position index |

## Constants

`AgentConstants` defines the task queue name (`mu-th-ur-agent`), role strings, the 50-turn `ContinueAsNew` threshold, and the workflow ID factory.

## Why this project exists

The Api needs to construct `PromptSignal` and read `AgentState` without referencing the Worker. The Data project needs `DocumentRecord` and `TextChunk` without referencing the Worker. Contracts is the shared language between all projects.
