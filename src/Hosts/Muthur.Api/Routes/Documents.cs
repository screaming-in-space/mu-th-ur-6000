using Microsoft.Extensions.AI;
using Muthur.Contracts;
using Muthur.Data;

namespace Muthur.Api.Routes;

public static class DocumentRoutes
{
    public static void MapDocumentRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/documents")
            .WithTags("Documents");

        group.MapGet("/", ListDocumentsAsync)
            .WithName("ListDocuments")
            .WithDescription("List all stored documents.")
            .Produces<IReadOnlyList<DocumentSummary>>();

        group.MapGet("/{id:guid}", GetDocumentAsync)
            .WithName("GetDocument")
            .WithDescription("Get document metadata by ID.")
            .Produces<DocumentRecord>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/content", GetDocumentContentAsync)
            .WithName("GetDocumentContent")
            .WithDescription("Get the full text content of a document.")
            .Produces<DocumentContentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/search", SearchDocumentsAsync)
            .WithName("SearchDocuments")
            .WithDescription("Vector similarity search across document chunks using pgvector.")
            .Produces<IReadOnlyList<SimilarChunk>>();
    }

    private static async Task<IResult> ListDocumentsAsync(
        IDocumentRepository repo,
        CancellationToken cancellationToken)
    {
        var docs = await repo.ListDocumentsAsync(cancellationToken);
        return Results.Ok(docs);
    }

    private static async Task<IResult> GetDocumentAsync(
        Guid id,
        IDocumentRepository repo,
        CancellationToken cancellationToken)
    {
        var doc = await repo.GetDocumentAsync(id, cancellationToken);
        return doc is null ? Results.NotFound() : Results.Ok(doc);
    }

    private static async Task<IResult> GetDocumentContentAsync(
        Guid id,
        IDocumentRepository repo,
        CancellationToken cancellationToken)
    {
        var content = await repo.GetDocumentContentAsync(id, cancellationToken);
        return content is null ? Results.NotFound() : Results.Ok(new DocumentContentResponse(content));
    }

    private static async Task<IResult> SearchDocumentsAsync(
        string q,
        int? limit,
        IDocumentRepository repo,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.BadRequest(new { Error = "Query parameter 'q' is required" });
        }

        if (q.Length > 1000)
        {
            return Results.BadRequest(new { Error = "Query must not exceed 1000 characters" });
        }

        var clampedLimit = Math.Clamp(limit ?? 5, 1, 100);

        var embedding = await embeddingGenerator.GenerateAsync(q, cancellationToken: cancellationToken);
        var queryVector = embedding.Vector.ToArray();
        var results = await repo.SearchSimilarAsync(queryVector, clampedLimit, cancellationToken);
        return Results.Ok(results);
    }
}
