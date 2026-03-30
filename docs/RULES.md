# Rules

Technical constraints and rejected patterns for mu-th-ur-6000.

## Stack

- .NET 10 / C# 14.0 — `LangVersion 14.0`, nullable enabled, implicit usings
- Temporal .NET SDK (`Temporalio.Extensions.Hosting` 1.12.0)
- Microsoft.Extensions.AI 9.7.0 / Microsoft.Extensions.AI.OpenAI 10.4.1
- PdfPig 0.1.15-alpha (NuGet package ID `PdfPig`, namespace `UglyToad.PdfPig`)
- .NET Aspire 13.2.0 for orchestration
- Docker Desktop required for local dev (Temporal runs as container)

## Temporal

### Do

- One activity per side effect. Each LLM call and each tool call is its own `ExecuteActivityAsync` — a separate Temporal checkpoint.
- Use `ContinueAsNew` after N turns (default 50). Event histories grow unbounded otherwise.
- Carry conversation history in signals, not workflow state. The workflow is stateless.
- Use `WaitConditionAsync` for signal-driven loops. Never `Task.Delay` to poll.
- Use `RetryPolicy` on tool activities. LLM activities get a single attempt with a long timeout.
- Use scoped activities (`AddScopedActivities<T>`) for DI — each execution gets a fresh scope.

### Don't

- Don't use `FunctionInvokingChatClient` inside a Temporal activity. It collapses all tool calls into one activity and you lose per-tool durability checkpoints.
- Don't hold `IChatClient` state across activity boundaries. Activities are stateless.
- Don't use `Thread.Sleep` or `Task.Delay` in workflow code. Use `Workflow.DelayAsync`.
- Don't call non-deterministic code (DateTime.Now, Guid.NewGuid, HTTP) directly in workflows. Wrap in activities.
- Don't reference Worker types from the Api project. Use untyped Temporal handles (string-based workflow/signal names).
- Don't use `with` expressions inside `Workflow.CreateContinueAsNewException` lambdas — they're expression trees. Create local variables first.

## M.E.AI / IChatClient

### Do

- Register `IChatClient` via `ChatClientBuilder` pipeline with `UseOpenTelemetry` and `UseLogging`.
- Use `AIFunctionFactory.Create()` with `[Description]` attributes for tool registration.
- Read provider, model, API key, and endpoint from configuration (`AI:Provider`, `AI:Model`, `AI:ApiKey`, `AI:Endpoint`).
- Keep the pipeline registration in one place (`AiClientExtensions.cs` in ServiceDefaults).

### Don't

- Don't use Semantic Kernel for new code. SK is in maintenance mode; M.E.AI + Agent Framework is the path forward.
- Don't add `FunctionInvokingChatClient` to the pipeline — the agentic loop in the workflow handles tool dispatch explicitly.
- Don't hardcode provider-specific API URLs. Use the provider switch pattern.

## PdfPig

### Do

- Use `PdfDocument.Open(path)` — returns pages, text, and metadata from pure managed code.
- Extract metadata from `document.Information` (Title, Author, Subject, Creator).
- Use `page.Text` for text extraction. It handles most standard PDF encodings.
- Serialize results as `PdfExtractionResult` with text, page count, and metadata dict.

### Don't

- Don't use the package name `UglyToad.PdfPig` on NuGet — the actual package ID is `PdfPig`. The namespace is `UglyToad.PdfPig`.
- Don't hold `PdfDocument` open longer than needed — wrap in `using`.
- Don't assume all PDFs have extractable text. Scanned documents return empty pages.

## Aspire

### Do

- Use `AddTemporalDevServer` to run Temporal as an Aspire-managed container resource.
- Use `.WithReference(temporal).WaitFor(temporal)` on the Worker so it doesn't start until Temporal is healthy.
- Use `EnsureDockerAsync()` in the AppHost to auto-launch Docker Desktop if not running.
- Mark non-service project references with `IsAspireProjectResource="false"` in the AppHost csproj.
- Use `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` in launch profiles for HTTP-only local dev.

### Don't

- Don't declare `Aspire.AppHost.Sdk` as a `PackageReference`. It's an `<Sdk>` element.
- Don't reference the Worker project from the Api project. They share only Contracts.

## NuGet

- The repo has a `nuget.config` with `<clear />` that removes all global sources (including private Azure DevOps feeds from other projects). Only `api.nuget.org` is configured.
- If you get NU1301 401 errors, the `<clear />` is missing or a global config is leaking.

## Naming

- Project names: `Muthur.*` (not `MuThUr`)
- Worker project: `Muthur.Bishop.Worker` (the middle name is deliberate)
- Temporal resource name: `muthur-temporal-dev`
- Temporal task queue: `mu-th-ur-agent` (defined in `AgentConstants.TaskQueue`)
- Prose/article references: "MU-TH-UR 6000" (Alien canon form)
