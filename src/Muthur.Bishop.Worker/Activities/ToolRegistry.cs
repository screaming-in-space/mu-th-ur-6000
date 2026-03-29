using System.ComponentModel;
using Microsoft.Extensions.AI;
using MuThUr.Contracts;

namespace MuThUr.Worker.Activities;

/// <summary>
/// Central registry for agent tools. Maps tool names to handlers and provides
/// M.E.AI tool definitions for the LLM to discover available capabilities.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, Func<string, Task<string>>> _handlers = new();
    private readonly List<AITool> _tools = [];

    public ToolRegistry()
    {
        // Register the PDF extraction tool using AIFunctionFactory.
        _handlers["extract_pdf_text"] = PdfActivities.ExtractTextAsync;

        _tools.Add(AIFunctionFactory.Create(
            [Description("Extract text content and metadata from a PDF file. Returns the full text, page count, and document metadata.")]
            async (
                [Description("Absolute path to the PDF file")] string filePath
            ) =>
            {
                var args = System.Text.Json.JsonSerializer.Serialize(new { FilePath = filePath });
                return await PdfActivities.ExtractTextAsync(args);
            },
            "extract_pdf_text"));
    }

    public IReadOnlyList<AITool> GetTools() => _tools;

    public Func<string, Task<string>>? GetHandler(string name) =>
        _handlers.GetValueOrDefault(name);
}
