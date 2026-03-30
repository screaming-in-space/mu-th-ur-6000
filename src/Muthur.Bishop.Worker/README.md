# Muthur.Bishop.Worker

Temporal worker hosting the agentic loop workflow and all activities.

## The agentic loop

`AgentWorkflow` is a signal-driven Temporal workflow:

1. Waits for `PromptSignal` via `WaitConditionAsync`
2. Calls `LlmActivities.CallLlmAsync` — sends conversation history to the LLM
3. If the LLM returns tool calls: dispatches each as a `ToolActivities.ExecuteToolAsync` activity
4. Appends tool results to conversation history, loops back to step 2
5. When no tool calls remain: returns the final response
6. After 50 turns: `ContinueAsNew` with a fresh event history

Every LLM call and every tool call is a separate Temporal checkpoint. If the process crashes mid-loop, Temporal replays from the event history — completed activities return their cached results instantly.

## Files

| File | Purpose |
|------|---------|
| `Program.cs` | Generic host setup — DI, Temporal worker registration |
| `Workflows/AgentWorkflow.cs` | Signal-driven agentic loop with `ContinueAsNew` |
| `Activities/LlmActivities.cs` | `IChatClient.GetResponseAsync` + tool call extraction |
| `Activities/ToolActivities.cs` | Name-based tool dispatch via `ToolRegistry` |
| `Activities/PdfActivities.cs` | PdfPig text extraction — static, no DI |
| `Activities/ToolRegistry.cs` | `AIFunctionFactory.Create()` registration + handler dictionary |

## Adding a tool

1. Write a static async handler: `async Task<string> MyTool(string arguments)`
2. In `ToolRegistry`, add an `AIFunctionFactory.Create()` call with `[Description]` attributes
3. Add a handler mapping: `_handlers["my_tool"] = MyTool;`

The workflow and activity dispatch code don't change.

## Configuration

The Worker reads `ConnectionStrings:muthur-temporal-dev` from Aspire, falling back to `Temporal:Address` or `localhost:7233`. AI provider settings come from `AI:Provider`, `AI:Model`, `AI:ApiKey`, `AI:Endpoint`.
