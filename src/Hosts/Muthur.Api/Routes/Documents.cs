using Microsoft.Extensions.AI;
using Muthur.Data;

namespace Muthur.Api.Routes;

public static class DocumentRoutes
{
    public static void MapDocumentRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/documents");

        // List all stored documents.
        group.MapGet("/", async (IDocumentRepository repo) =>
        {
            var docs = await repo.ListDocumentsAsync();
            return Results.Ok(docs);
        });

        // Get document metadata by ID.
        group.MapGet("/{id:guid}", async (Guid id, IDocumentRepository repo) =>
        {
            var doc = await repo.GetDocumentAsync(id);
            return doc is null ? Results.NotFound() : Results.Ok(doc);
        });

        // Get full document content.
        group.MapGet("/{id:guid}/content", async (Guid id, IDocumentRepository repo) =>
        {
            var content = await repo.GetDocumentContentAsync(id);
            return content is null ? Results.NotFound() : Results.Ok(new { Content = content });
        });

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
        });
    }
}
