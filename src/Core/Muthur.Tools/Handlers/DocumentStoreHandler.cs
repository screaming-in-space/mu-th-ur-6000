using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Muthur.Contracts;
using Muthur.Tools.Documents;

namespace Muthur.Tools.Handlers;

/// <summary>
/// Tool handler bridge for document storage. Deserializes tool arguments,
/// delegates to <see cref="DocumentStore"/>, and serializes the result.
/// </summary>
public sealed class DocumentStoreHandler(DocumentStore store) : IToolHandler
{
    public AIFunction Register(
        Dictionary<string, Func<string, ToolExecutionContext, Task<ToolResult>>> handlers)
    {
        handlers[AgentConstants.ToolStoreDocument] = StoreAsync;
        return AIFunctionFactory.Create(StoreDocumentAsync, AgentConstants.ToolStoreDocument);
    }

    /// <summary>JSON bridge for Temporal activity dispatch path.</summary>
    public async Task<ToolResult> StoreAsync(string arguments, ToolExecutionContext context)
    {
        var args = JsonSerializer.Deserialize<StoreDocumentJob>(arguments, SerializerDefaults.CaseInsensitive)
            ?? throw new ArgumentException("Invalid store_document arguments");

        var id = await store.StoreAsync(args, context.CancellationToken).ConfigureAwait(false);

        return ToolResult.From(new StoreDocumentResult(id));
    }

    /// <summary>LLM tool definition — calls domain logic directly with typed parameters.</summary>
    [Description("Store a previously extracted document in the knowledge base for future search. " +
                 "Call this after extract_pdf_text to persist the document. " +
                 "The extracted text is injected automatically — only provide metadata. " +
                 "Returns the stored document ID.")]
    private async Task<StoreDocumentResult> StoreDocumentAsync(
        [Description("Document title")] string title,
        [Description("Original file path (same path used in extract_pdf_text)")] string sourcePath,
        [Description("Number of pages in the document")] int pageCount,
        CancellationToken cancellationToken)
    {
        var args = new StoreDocumentJob(title, sourcePath, null, pageCount);
        var id = await store.StoreAsync(args, cancellationToken).ConfigureAwait(false);
        return new StoreDocumentResult(id);
    }
}
