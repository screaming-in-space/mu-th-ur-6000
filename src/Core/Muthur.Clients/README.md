# Muthur.Clients

Typed HTTP client for the Muthur API.

## Usage

```csharp
// DI registration (opinionated defaults with connection pooling)
services.AddMuthurApiClient(new Uri("http://muthur-api"));

// Or custom configuration
services.AddMuthurApiClient(client => client.BaseAddress = new Uri("http://localhost:5180"));
```

## MuthurApiClient

| Method | HTTP | Endpoint | Returns |
|--------|------|----------|---------|
| `CreateSessionAsync` | POST | `/v1/agent/sessions` | `CreateSessionResponse` |
| `SendPromptAsync` | POST | `/v1/agent/sessions/{agentId}/prompt` | — |
| `GetAgentStateAsync` | GET | `/v1/agent/sessions/{agentId}` | `AgentState` |
| `ListDocumentsAsync` | GET | `/v1/documents` | `List<DocumentSummary>` |
| `SearchDocumentsAsync` | GET | `/v1/documents/search` | `List<SimilarChunk>` |

All methods throw `MuthurApiException` on non-success responses.

## Error handling

- `MuthurErrorHandler` — `DelegatingHandler` that intercepts 401/403 and throws immediately
- `MuthurApiException` — extends `HttpRequestException`, parses RFC 7807 problem details, truncates raw bodies at 500 chars
- Both overloads of `AddMuthurApiClient` wire the error handler into the pipeline

## Dependencies

- `Muthur.Contracts` — shared request/response types
- `Microsoft.Extensions.Http` — `IHttpClientFactory` and typed HttpClient support
