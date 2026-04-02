using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Muthur.Tools.Handlers;

namespace Muthur.Tools;

/// <summary>
/// Central registry for agent tools. Maps tool names to handlers and provides
/// M.E.AI tool definitions for the LLM to discover available capabilities.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, Func<string, CancellationToken, Task<string>>> _handlers = [];
    private readonly List<AITool> _tools = [];

    public ToolRegistry(ILogger<ToolRegistry> logger, DocumentStoreHandler documentStoreHandler)
    {
        // PDF text extraction.
        _handlers["extract_pdf_text"] = PdfHandler.ExtractTextAsync;
        _tools.Add(AIFunctionFactory.Create(
            [Description("Extract text content and metadata from a PDF file. Returns the full text, page count, and document metadata.")]
            async (
                [Description("Absolute path to the PDF file")] string filePath,
                CancellationToken cancellationToken
            ) =>
            {
                var args = JsonSerializer.Serialize(new { FilePath = filePath });
                return await PdfHandler.ExtractTextAsync(args, cancellationToken).ConfigureAwait(false);
            },
            "extract_pdf_text"));

        // Document storage - persists extracted text to Postgres.
        // The LLM provides metadata only; the workflow injects cached extraction text.
        _handlers["store_document"] = documentStoreHandler.StoreAsync;
        _tools.Add(AIFunctionFactory.Create(
            [Description("Store a previously extracted document in the knowledge base for future search. " +
                         "Call this after extract_pdf_text to persist the document. " +
                         "The extracted text is injected automatically — only provide metadata. " +
                         "Returns the stored document ID.")]
            async (
                [Description("Document title")] string title,
                [Description("Original file path (same path used in extract_pdf_text)")] string sourcePath,
                [Description("Number of pages in the document")] int pageCount,
                CancellationToken cancellationToken
            ) =>
            {
                var args = JsonSerializer.Serialize(new
                {
                    Title = title,
                    SourcePath = sourcePath,
                    PageCount = pageCount,
                });
                return await documentStoreHandler.StoreAsync(args, cancellationToken).ConfigureAwait(false);
            },
            "store_document"));
    }

    public IReadOnlyList<AITool> GetTools() => _tools;

    public Func<string, CancellationToken, Task<string>>? GetHandler(string name) =>
        _handlers.GetValueOrDefault(name);
}
