using System.Text.Json;

namespace Muthur.Contracts;

/// <summary>Result of caching a PDF extraction. Contains the LLM summary and optional parsed extraction.</summary>
public sealed record ExtractionCacheResult(
    string LlmSummary,
    PdfExtractionResult? Extraction = null,
    string? FilePath = null)
{
    /// <summary>Whether the extraction was successfully parsed and cached.</summary>
    public bool IsCached => Extraction is not null && FilePath is not null;
}

/// <summary>
/// Pure functions for processing tool results in the agentic workflow.
/// Extracted from AgentWorkflow for testability — no Temporal API dependencies.
/// </summary>
public static class ToolResultProcessor
{
    /// <summary>
    /// Parses an extraction result and produces a summary for the LLM.
    /// The caller caches the full extraction in workflow state.
    /// Returns a passthrough result with the raw <paramref name="toolResult"/> on any parse failure.
    /// </summary>
    public static ExtractionCacheResult CacheExtraction(
        string toolResult, Dictionary<string, object?> toolArguments)
    {
        try
        {
            var extraction = JsonSerializer.Deserialize<PdfExtractionResult>(
                toolResult, SerializerDefaults.CaseInsensitive);
            var filePath = toolArguments.GetValueOrDefault("filePath")?.ToString();

            if (extraction is null || filePath is null)
            { return new(toolResult); }

            var summary = JsonSerializer.Serialize(new
            {
                Status = "extracted",
                extraction.PageCount,
                extraction.Metadata,
                TextLength = extraction.Text.Length,
                SourcePath = filePath,
                Message = "Text extracted successfully. Call store_document to persist."
            });

            return new(summary, extraction, filePath);
        }
        catch (JsonException)
        {
            return new(toolResult);
        }
    }

    /// <summary>
    /// Enriches store_document arguments with cached extraction text and metadata.
    /// The LLM sends metadata only; the workflow injects the full content.
    /// Returns the original arguments on cache miss or any error.
    /// </summary>
    public static Dictionary<string, object?> EnrichStoreArguments(
        Dictionary<string, object?> arguments,
        IReadOnlyDictionary<string, PdfExtractionResult> extractions)
    {
        try
        {
            var sourcePath = arguments.GetValueOrDefault("sourcePath")?.ToString();
            if (sourcePath is null) return arguments;

            var existingText = arguments.GetValueOrDefault("text")?.ToString();
            if (!string.IsNullOrEmpty(existingText)) return arguments;

            if (!extractions.TryGetValue(sourcePath, out var extraction)) return arguments;

            var enriched = new Dictionary<string, object?>(arguments)
            {
                ["text"] = extraction.Text,
                ["metadata"] = extraction.Metadata
            };

            if (!enriched.TryGetValue("pageCount", out var value) || value is null or 0)
            {
                enriched["pageCount"] = extraction.PageCount;
            }

            var title = arguments.GetValueOrDefault("title")?.ToString();
            if (string.IsNullOrEmpty(title))
            {
                enriched["title"] = extraction.Metadata.GetValueOrDefault("title");
            }

            return enriched;
        }
        catch (Exception e) when (e is JsonException or InvalidCastException or KeyNotFoundException)
        {
            return arguments;
        }
    }

    /// <summary>
    /// Parses tool arguments and result into a <see cref="DocumentIngestionInput"/>
    /// for the ingestion child workflow. Returns null if the document ID is missing
    /// or on any parse failure.
    /// </summary>
    public static DocumentIngestionInput? ParseIngestionInput(
        string agentId, Dictionary<string, object?> toolArguments, string toolResult)
    {
        try
        {
            var parsedResult = JsonSerializer.Deserialize<StoreDocumentResult>(
                toolResult, SerializerDefaults.CaseInsensitive);
            if (parsedResult?.DocumentId is null) { return null; }

            var sourcePath = toolArguments.GetValueOrDefault("sourcePath")?.ToString() ?? "";
            var text = toolArguments.GetValueOrDefault("text")?.ToString() ?? "";
            var pageCount = toolArguments.GetValueOrDefault("pageCount") switch
            {
                int i => i,
                long l => (int)l,
                JsonElement je when je.TryGetInt32(out var v) => v,
                _ => 0
            };

            var metadata = ParseMetadata(toolArguments.GetValueOrDefault("metadata"));

            return new DocumentIngestionInput(
                agentId,
                parsedResult.DocumentId.Value,
                sourcePath,
                text,
                pageCount,
                metadata);
        }
        catch (Exception e) when (e is JsonException or InvalidCastException or KeyNotFoundException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> ParseMetadata(object? raw) => raw switch
    {
        Dictionary<string, string> dict => dict,
        JsonElement { ValueKind: JsonValueKind.Object } element =>
            element.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? ""),
        _ => []
    };
}
