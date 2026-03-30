# Muthur.Bishop.Worker

Temporal worker hosting the agentic loop workflow, document ingestion child workflow, and all activities.

## Workflows

### AgentWorkflow

Signal-driven agentic loop:

1. Waits for `PromptSignal` via `WaitConditionAsync`
2. Calls `LlmActivities.CallLlmAsync` - sends conversation history to the LLM
3. If the LLM returns tool calls: dispatches each as a `ToolActivities.ExecuteToolAsync` activity
4. Appends tool results to conversation history, loops back to step 2
5. When no tool calls remain: returns the final response
6. After 50 turns: `ContinueAsNew` with a fresh event history

When the `store_document` tool completes, the response goes back to the user immediately. Then the workflow forks `DocumentIngestionWorkflow` as a child workflow with `ParentClosePolicy.Abandon` — the vectorize pipeline runs in the background and survives `ContinueAsNew`.

### DocumentIngestionWorkflow (the "Vectorize" pipeline)

Child workflow that chunks, embeds, and stores vectors:

1. `ChunkTextAsync` — split text into ~500-token chunks with 50-token overlap
2. `GenerateEmbeddingsAsync` — call embedding model via `IEmbeddingGenerator`
3. `StoreChunksAsync` — bulk INSERT chunks + embeddings to Postgres with pgvector

Each step is individually checkpointed. If embedding fails, chunking doesn't re-run.

## Files

| File | Purpose |
|------|---------|
| `Program.cs` | Generic host setup - DI for tools, data, embeddings, Temporal registration |
| `Workflows/AgentWorkflow.cs` | Signal-driven agentic loop + child workflow trigger |
| `Workflows/DocumentIngestionWorkflow.cs` | Chunk → embed → store pipeline |
| `Activities/LlmActivities.cs` | `IChatClient.GetResponseAsync` + tool call extraction |
| `Activities/ToolActivities.cs` | Name-based tool dispatch via `ToolRegistry` |
| `Activities/IngestionActivities.cs` | Chunk text, generate embeddings, store chunks |

## Configuration

Reads `ConnectionStrings:muthur-temporal-dev` from Aspire, falling back to `Temporal:Address` or `localhost:7233`. AI settings from `AI:Provider`, `AI:Model`, `AI:ApiKey`, `AI:Endpoint`, `AI:EmbeddingModel`.
