using CommunityToolkit.HighPerformance.Buffers;
using Muthur.Contracts;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Muthur.Tools.Pdf;

/// <summary>
/// Pure PDF text extraction using PdfPig. No JSON, no tool plumbing.
/// Uses <see cref="ArrayPoolBufferWriter{T}"/> to avoid LOH allocations for large documents.
/// </summary>
public static class PdfExtractor
{
    private const int AvgCharsPerPage = 2000;
    private const int MinBufferSize = 4096;
    private const int MetadataKeyCount = 4;

    private static ReadOnlySpan<char> PagePrefix => "--- Page ";
    private static ReadOnlySpan<char> PageSuffix => " ---";

    /// <summary>
    /// Extracts text and metadata from a PDF file.
    /// </summary>
    /// <param name="filePath">Absolute path to the PDF file.</param>
    /// <returns>Extracted text, page count, and document metadata.</returns>
    public static PdfExtractionResult Extract(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("FilePath is required for PDF extraction");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"PDF file not found: {filePath}", filePath);
        }

        using var document = PdfDocument.Open(filePath);

        var estimatedChars = document.NumberOfPages * AvgCharsPerPage;
        using var buffer = new ArrayPoolBufferWriter<char>(Math.Max(MinBufferSize, estimatedChars));
        var metadata = new Dictionary<string, string>(MetadataKeyCount);

        ExtractMetadata(document.Information, metadata);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendPageHeader(buffer, page.Number);
            AppendLine(buffer, page.Text);
            AppendNewLine(buffer);
        }

        var text = buffer.WrittenSpan.TrimEnd().ToString();
        return new PdfExtractionResult(text, document.NumberOfPages, metadata);
    }

    private static void ExtractMetadata(
        DocumentInformation info,
        Dictionary<string, string> metadata)
    {
        if (info.Title is not null) { metadata["title"] = info.Title; }
        if (info.Author is not null) { metadata["author"] = info.Author; }
        if (info.Subject is not null) { metadata["subject"] = info.Subject; }
        if (info.Creator is not null) { metadata["creator"] = info.Creator; }
    }

    /// <summary>Writes <c>--- Page N ---\n</c> without allocating an interpolated string.</summary>
    private static void AppendPageHeader(ArrayPoolBufferWriter<char> buffer, int pageNumber)
    {
        Append(buffer, PagePrefix);

        Span<char> numBuf = stackalloc char[10];
        if (pageNumber.TryFormat(numBuf, out var charsWritten))
        {
            Append(buffer, numBuf[..charsWritten]);
        }

        Append(buffer, PageSuffix);
        AppendNewLine(buffer);
    }

    private static void AppendLine(ArrayPoolBufferWriter<char> buffer, string value)
    {
        Append(buffer, value.AsSpan());
        AppendNewLine(buffer);
    }

    private static void Append(ArrayPoolBufferWriter<char> buffer, ReadOnlySpan<char> value)
    {
        var span = buffer.GetSpan(value.Length);
        value.CopyTo(span);
        buffer.Advance(value.Length);
    }

    private static void AppendNewLine(ArrayPoolBufferWriter<char> buffer)
    {
        Append(buffer, Environment.NewLine.AsSpan());
    }
}
