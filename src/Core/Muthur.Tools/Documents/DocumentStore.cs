using Microsoft.Extensions.Logging;
using Muthur.Data;

namespace Muthur.Tools.Documents;

/// <summary>
/// Pure document storage logic. Validates inputs and persists to the repository.
/// No JSON, no tool plumbing — mirrors <see cref="Pdf.PdfExtractor"/> pattern.
/// </summary>
public sealed class DocumentStore(
    ILogger<DocumentStore> logger,
    IDocumentRepository repository)
{
    /// <summary>
    /// Validates and stores a document, returning its assigned ID.
    /// </summary>
    public async Task<Guid> StoreAsync(
        StoreDocumentJob job,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(job.SourcePath))
        {
            throw new ArgumentException("SourcePath is required for document storage");
        }

        logger.LogInformation("Storing document — {Title}, {SourcePath}", job.Title, job.SourcePath);

        var id = await repository.StoreDocumentAsync(
            job.Title,
            job.SourcePath,
            job.Text ?? "",
            job.PageCount,
            job.Metadata ?? [],
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Document stored — {DocumentId}", id);

        return id;
    }
}
