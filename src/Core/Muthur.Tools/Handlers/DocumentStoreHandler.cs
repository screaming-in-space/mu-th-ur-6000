using System.ComponentModel;
using Microsoft.Extensions.AI;
using Muthur.Contracts;
using Muthur.Tools.Documents;

namespace Muthur.Tools.Handlers;

/// <summary>
/// Tool handler for document storage. The typed method is both the LLM schema
/// and the runtime execution path via <see cref="AIFunction.InvokeAsync"/>.
/// Workflow-injected parameters (text, metadata) are excluded from the LLM schema
/// but still bound by <see cref="AIFunction"/> when present in the arguments dictionary.
/// </summary>
public sealed class DocumentStoreHandler(DocumentStore store) : IToolHandler
{
    /// <summary>Names of parameters the workflow injects — hidden from the LLM schema.</summary>
    private static readonly HashSet<string> WorkflowInjectedParams = ["text", "metadata"];

    public ToolRegistration Register() => new(
        AgentConstants.ToolStoreDocument,
        AIFunctionFactory.Create(StoreDocumentAsync, new AIFunctionFactoryOptions
        {
            Name = AgentConstants.ToolStoreDocument,
            ConfigureParameterBinding = param =>
                param.Name is not null && WorkflowInjectedParams.Contains(param.Name)
                    ? new() { ExcludeFromSchema = true }
                    : default
        }));

    /// <summary>
    /// Store a previously extracted document. The LLM provides title, sourcePath,
    /// and pageCount. The workflow enriches the arguments dictionary with text and
    /// metadata from the extraction cache before dispatch.
    /// </summary>
    [Description("Store a previously extracted document in the knowledge base for future search. " +
                 "Call this after extract_pdf_text to persist the document. " +
                 "The extracted text is injected automatically — only provide metadata. " +
                 "Returns the stored document ID.")]
    public async Task<StoreDocumentResult> StoreDocumentAsync(
        [Description("Document title")] string title,
        [Description("Original file path (same path used in extract_pdf_text)")] string sourcePath,
        [Description("Number of pages in the document")] int pageCount,
        string? text = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var job = new StoreDocumentJob(title, sourcePath, text, pageCount, metadata);
        var id = await store.StoreAsync(job, cancellationToken).ConfigureAwait(false);
        return new StoreDocumentResult(id);
    }
}
