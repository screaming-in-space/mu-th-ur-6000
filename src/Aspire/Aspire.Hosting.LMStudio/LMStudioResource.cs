using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Represents an externally-managed LM Studio instance exposing an OpenAI-compatible API.
/// Connection string format: <c>Endpoint=http://localhost:1234;Model=chat-model;EmbeddingModel=embedding-model</c>
/// </summary>
public class LMStudioResource(string name, string endpoint, string modelName)
    : Resource(name), IResourceWithConnectionString
{
    public string Endpoint { get; } = endpoint;

    public string ModelName =>
        this.TryGetLastAnnotation<ModelNameAnnotation>(out var annotation)
            ? annotation.ModelName
            : modelName;

    public string? EmbeddingModelName =>
        this.TryGetLastAnnotation<EmbeddingModelNameAnnotation>(out var annotation)
            ? annotation.EmbeddingModelName
            : null;

    public ReferenceExpression ConnectionStringExpression =>
        EmbeddingModelName is not null
            ? ReferenceExpression.Create($"Endpoint={Endpoint};Model={ModelName};EmbeddingModel={EmbeddingModelName}")
            : ReferenceExpression.Create($"Endpoint={Endpoint};Model={ModelName}");
}

public record ModelNameAnnotation(string ModelName) : IResourceAnnotation;
public record EmbeddingModelNameAnnotation(string EmbeddingModelName) : IResourceAnnotation;
