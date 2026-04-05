namespace Muthur.Tools.Documents;

/// <summary>
/// Input for the store_document tool.
/// The LLM sends metadata only; the workflow injects cached extraction text via <see cref="Text"/>.
/// </summary>
public sealed record StoreDocumentJob(
    string? Title,
    string? SourcePath,
    string? Text,
    int PageCount = 0,
    Dictionary<string, string>? Metadata = null);
