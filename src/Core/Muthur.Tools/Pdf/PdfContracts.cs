namespace Muthur.Tools.Pdf;

/// <summary>Deserialized arguments from the pdf_extract_text tool call.</summary>
public sealed record ExtractPdfJob(string? FilePath);
