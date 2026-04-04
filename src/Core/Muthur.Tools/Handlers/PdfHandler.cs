using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Muthur.Contracts;
using Muthur.Tools.Pdf;

namespace Muthur.Tools.Handlers;

/// <summary>
/// Tool handler bridge for PDF extraction. Deserializes tool arguments,
/// delegates to <see cref="PdfExtractor"/>, and serializes the result.
/// </summary>
public class PdfHandler : IToolHandler
{
    public AIFunction Register(
        Dictionary<string, Func<string, ToolExecutionContext, Task<ToolResult>>> handlers)
    {
        handlers[AgentConstants.ToolExtractPdf] = ExtractTextAsync;
        return AIFunctionFactory.Create(ExtractPdfTextAsync, AgentConstants.ToolExtractPdf);
    }

    /// <summary>JSON bridge for Temporal activity dispatch path.</summary>
    public Task<ToolResult> ExtractTextAsync(string arguments, ToolExecutionContext context)
    {
        var args = JsonSerializer.Deserialize<ExtractPdfJob>(arguments, SerializerDefaults.CaseInsensitive)
            ?? throw new ArgumentException("Invalid PDF extraction arguments");

        var result = PdfExtractor.Extract(
            args.FilePath ?? throw new ArgumentException("FilePath is required"),
            context.CancellationToken);

        return Task.FromResult(ToolResult.From(result));
    }

    [Description("Extract text content and metadata from a PDF file. " +
                 "Returns the full text, page count, and document metadata.")]
    private Task<PdfExtractionResult> ExtractPdfTextAsync(
        [Description("Absolute path to the PDF file")] string filePath,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(PdfExtractor.Extract(filePath, cancellationToken));
    }
}
