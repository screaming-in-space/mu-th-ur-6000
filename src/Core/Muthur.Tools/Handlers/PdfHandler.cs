using System.Text.Json;
using Muthur.Contracts;
using Muthur.Tools.Pdf;

namespace Muthur.Tools.Handlers;

/// <summary>
/// Tool handler bridge for PDF extraction. Deserializes tool arguments,
/// delegates to <see cref="PdfExtractor"/>, and serializes the result.
/// </summary>
public static class PdfHandler
{
    public static Task<string> ExtractTextAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var args = JsonSerializer.Deserialize<ExtractPdfArgs>(arguments, SerializerDefaults.CaseInsensitive)
            ?? throw new ArgumentException("Invalid PDF extraction arguments");

        var result = PdfExtractor.Extract(args.FilePath!);

        return Task.FromResult(JsonSerializer.Serialize(result));
    }
}
