using System.Text;
using System.Text.Json;
using Muthur.Contracts;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Muthur.Tools.Handlers;

/// <summary>
/// PDF text extraction using PdfPig. Extracts text page-by-page with metadata.
/// </summary>
public static class PdfHandler
{
    public static async Task<string> ExtractTextAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<PdfExtractArgs>(arguments)
            ?? throw new ArgumentException("Invalid PDF extraction arguments");

        if (!File.Exists(args.FilePath))
            return JsonSerializer.Serialize(new PdfExtractionResult(
                $"File not found: {args.FilePath}", 0, new()));

        using var document = PdfDocument.Open(args.FilePath);

        var text = new StringBuilder();
        var metadata = new Dictionary<string, string>();

        var info = document.Information;
        if (info.Title is not null) metadata["title"] = info.Title;
        if (info.Author is not null) metadata["author"] = info.Author;
        if (info.Subject is not null) metadata["subject"] = info.Subject;
        if (info.Creator is not null) metadata["creator"] = info.Creator;

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

        return await Task.FromResult(JsonSerializer.Serialize(result));
    }

    private sealed record PdfExtractArgs(string FilePath);
}
