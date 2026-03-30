# Muthur.Contracts

Shared types. Zero dependencies. Referenced by both Api and Worker.

## Records

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

## Constants

`AgentConstants` defines the task queue name (`mu-th-ur-agent`), role strings, the 50-turn `ContinueAsNew` threshold, and the workflow ID factory.

## Why this project exists

The Api needs to construct `PromptSignal` and read `AgentState` without referencing the Worker. Contracts is the shared language between them.
