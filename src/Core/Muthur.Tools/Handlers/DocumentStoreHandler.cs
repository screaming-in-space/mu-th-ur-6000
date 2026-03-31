using System.Text.Json;
using Muthur.Data;

namespace Muthur.Tools.Handlers;

/// <summary>
/// Stores extracted document text in Postgres. Returns the document ID.
/// The agent workflow starts a child ingestion workflow after this tool completes
/// to handle chunking and embedding generation asynchronously.
/// </summary>
public sealed class DocumentStoreHandler(IDocumentRepository repository)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<string> StoreAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<StoreDocumentArgs>(arguments, JsonOptions)
            ?? throw new ArgumentException("Invalid store_document arguments");

        var id = await repository.StoreDocumentAsync(
            args.Title,
            args.SourcePath,
            args.Text,
            args.PageCount,
            args.Metadata ?? []);

        return JsonSerializer.Serialize(new { DocumentId = id });
    }

    private sealed record StoreDocumentArgs(
        string? Title,
        string SourcePath,
        string Text,
        int PageCount = 0,
        Dictionary<string, string>? Metadata = null);
}
