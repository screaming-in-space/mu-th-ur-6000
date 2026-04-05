using System.ComponentModel;
using Microsoft.Extensions.AI;
using Muthur.Contracts;
using Muthur.Tools.Pdf;

namespace Muthur.Tools.Handlers;

/// <summary>
/// Tool handler for PDF extraction. The typed method is both the LLM schema
/// (via <see cref="DescriptionAttribute"/>) and the runtime execution path
/// (via <see cref="AIFunction.InvokeAsync"/>).
/// </summary>
public class PdfHandler : IToolHandler
{
    public ToolRegistration Register() => new(
        AgentConstants.ToolPdfExtractText,
        AIFunctionFactory.Create(ExtractPdfTextAsync, AgentConstants.ToolPdfExtractText));

    [Description("Extract text content and metadata from a PDF file. " +
                 "Returns the full text, page count, and document metadata.")]
    public Task<PdfExtractionResult> ExtractPdfTextAsync(
        [Description("Absolute path to the PDF file")] string filePath,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(PdfExtractor.Extract(filePath, cancellationToken));
    }
}
