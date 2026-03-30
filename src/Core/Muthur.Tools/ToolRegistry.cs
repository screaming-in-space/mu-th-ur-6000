using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Muthur.Tools.Handlers;

namespace Muthur.Tools;

/// <summary>
/// Central registry for agent tools. Maps tool names to handlers and provides
/// M.E.AI tool definitions for the LLM to discover available capabilities.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, Func<string, Task<string>>> _handlers = [];
    private readonly List<AITool> _tools = [];

    public ToolRegistry(DocumentStoreHandler documentStoreHandler)
    {
        // PDF text extraction.
        _handlers["extract_pdf_text"] = PdfHandler.ExtractTextAsync;
        _tools.Add(AIFunctionFactory.Create(
            [Description("Extract text content and metadata from a PDF file. Returns the full text, page count, and document metadata.")]
            async (
                [Description("Absolute path to the PDF file")] string filePath
            ) =>
            {
                var args = JsonSerializer.Serialize(new { FilePath = filePath });
                return await PdfHandler.ExtractTextAsync(args);
            },
            "extract_pdf_text"));

        // Document storage - persists extracted text to Postgres.
        _handlers["store_document"] = documentStoreHandler.StoreAsync;
        _tools.Add(AIFunctionFactory.Create(
            [Description("Store extracted document text and metadata in the knowledge base. " +
                         "Call this after extracting text from a PDF to persist it for future search. " +
                         "Returns the stored document ID.")]
            async (
                [Description("Document title")] string title,
                [Description("Original file path")] string sourcePath,
                [Description("Full extracted text content")] string text,
                [Description("Number of pages in the document")] int pageCount,
                [Description("Document metadata as JSON string")] string? metadata
            ) =>
            {
                var args = JsonSerializer.Serialize(new
                {
                    Title = title,
                    SourcePath = sourcePath,
                    Text = text,
                    PageCount = pageCount,
                    Metadata = string.IsNullOrEmpty(metadata)
                        ? new Dictionary<string, string>()
                        : JsonSerializer.Deserialize<Dictionary<string, string>>(metadata)
                });
                return await documentStoreHandler.StoreAsync(args);
            },
            "store_document"));
    }

    public IReadOnlyList<AITool> GetTools() => _tools;

    public Func<string, Task<string>>? GetHandler(string name) =>
        _handlers.GetValueOrDefault(name);
}
