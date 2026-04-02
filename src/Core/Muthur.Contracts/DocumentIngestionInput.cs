namespace Muthur.Contracts;

/// <summary>Input for the document ingestion child workflow.</summary>
public sealed record DocumentIngestionInput(
    string AgentId,
    Guid DocumentId,
    string SourcePath,
    string Text,
    int PageCount,
    Dictionary<string, string> Metadata);

/// <summary>A text chunk with its position in the document.</summary>
public sealed record TextChunk(
    int Index,
    string Text);
