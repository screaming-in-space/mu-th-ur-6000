using System.Text;
using MuThUr.Contracts;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MuThUr.Worker.Activities;

/// <summary>
/// PDF text extraction using PdfPig. Extracts text page-by-page with metadata.
/// This is the practical tool — feeds directly into a knowledge ingestion pipeline.
/// </summary>
public static class PdfActivities
{
    public static async Task<string> ExtractTextAsync(string arguments)
    {
        var args = System.Text.Json.JsonSerializer.Deserialize<PdfExtractArgs>(arguments)
            ?? throw new ArgumentException("Invalid PDF extraction arguments");

        if (!File.Exists(args.FilePath))
            return System.Text.Json.JsonSerializer.Serialize(new PdfExtractionResult(
                $"File not found: {args.FilePath}", 0, new()));

        using var document = PdfDocument.Open(args.FilePath);

        var text = new StringBuilder();
        var metadata = new Dictionary<string, string>();

        // Extract document-level metadata.
        var info = document.Information;
        if (info.Title is not null) metadata["title"] = info.Title;
        if (info.Author is not null) metadata["author"] = info.Author;
        if (info.Subject is not null) metadata["subject"] = info.Subject;
        if (info.Creator is not null) metadata["creator"] = info.Creator;

        // Extract text page by page.
        foreach (Page page in document.GetPages())
        {
            text.AppendLine($"--- Page {page.Number} ---");
            text.AppendLine(page.Text);
            text.AppendLine();
        }

        var result = new PdfExtractionResult(
            text.ToString().Trim(),
            document.NumberOfPages,
            metadata);

        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    private sealed record PdfExtractArgs(string FilePath);
}
