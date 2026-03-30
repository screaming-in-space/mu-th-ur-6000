using Muthur.Bishop.Worker.Activities;
using Muthur.Contracts;
using Temporalio.Workflows;

namespace Muthur.Bishop.Worker.Workflows;

/// <summary>
/// Child workflow that chunks document text, generates embeddings,
/// and stores them in Postgres with pgvector. Each step is a Temporal
/// activity checkpoint - if embedding fails, chunking doesn't re-run.
/// </summary>
[Workflow]
public class DocumentIngestionWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(DocumentIngestionInput input)
    {
        // Step 1: Split text into chunks.
        var chunks = await Workflow.ExecuteActivityAsync(
            (IngestionActivities act) => act.ChunkTextAsync(input.Text),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

        if (chunks.Length == 0) return;

        // Step 2: Generate embeddings for all chunks.
        var embeddings = await Workflow.ExecuteActivityAsync(
            (IngestionActivities act) => act.GenerateEmbeddingsAsync(chunks),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
                RetryPolicy = new Temporalio.Common.RetryPolicy
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(5)
                }
            });

        // Step 3: Bulk insert chunks + embeddings.
        await Workflow.ExecuteActivityAsync(
            (IngestionActivities act) => act.StoreChunksAsync(input.DocumentId, chunks, embeddings),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(2),
                RetryPolicy = new Temporalio.Common.RetryPolicy
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(2)
                }
            });
    }
}
