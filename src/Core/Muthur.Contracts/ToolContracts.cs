namespace Muthur.Contracts;

/// <summary>Deserialized arguments from the extract_pdf_text tool call.</summary>
public sealed record ExtractPdfArgs(string? FilePath);

/// <summary>
/// Deserialized arguments from the store_document tool call.
/// The LLM sends metadata only; the workflow injects cached extraction text via <see cref="Text"/>.
/// </summary>
public sealed record StoreDocumentArgs(
    string? Title,
    string? SourcePath,
    string? Text,
    int PageCount = 0,
    Dictionary<string, string>? Metadata = null);

/// <summary>Deserialized result from the store_document tool handler.</summary>
public sealed record StoreDocumentResult(Guid? DocumentId);
