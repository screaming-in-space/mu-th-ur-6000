namespace Muthur.Contracts;

/// <summary>Result of extracting text from a PDF document.</summary>
public sealed record PdfExtractionResult(
    string Text,
    int PageCount,
    Dictionary<string, string> Metadata);
