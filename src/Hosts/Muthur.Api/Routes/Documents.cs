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

        // List all stored documents.
        group.MapGet("/", async (IDocumentRepository repo) =>
        {
            var docs = await repo.ListDocumentsAsync();
            return Results.Ok(docs);
        })
        .WithName("ListDocuments")
        .WithDescription("List all stored documents.")
        .Produces<IReadOnlyList<DocumentSummary>>();

        // Get document metadata by ID.
        group.MapGet("/{id:guid}", async (Guid id, IDocumentRepository repo) =>
        {
            var doc = await repo.GetDocumentAsync(id);
            return doc is null ? Results.NotFound() : Results.Ok(doc);
        })
        .WithName("GetDocument")
        .WithDescription("Get document metadata by ID.")
        .Produces<DocumentRecord>()
        .Produces(StatusCodes.Status404NotFound);

        // Get full document content.
        group.MapGet("/{id:guid}/content", async (Guid id, IDocumentRepository repo) =>
        {
            var content = await repo.GetDocumentContentAsync(id);
            return content is null ? Results.NotFound() : Results.Ok(new DocumentContentResponse(content));
        })
        .WithName("GetDocumentContent")
        .WithDescription("Get the full text content of a document.")
        .Produces<DocumentContentResponse>()
        .Produces(StatusCodes.Status404NotFound);

        // Vector similarity search across document chunks.
        group.MapGet("/search", async (
            string q,
            int? limit,
            IDocumentRepository repo,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) =>
        {
            var embedding = await embeddingGenerator.GenerateAsync(q);
            var queryVector = embedding.Vector.ToArray();
            var results = await repo.SearchSimilarAsync(queryVector, limit ?? 5);
            return Results.Ok(results);
        })
        .WithName("SearchDocuments")
        .WithDescription("Vector similarity search across document chunks using pgvector.")
        .Produces<IReadOnlyList<SimilarChunk>>();
    }
}
