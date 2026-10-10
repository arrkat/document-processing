using BlobStorage;
using JobService.Persistence;
using JobService.Settings;
using Microsoft.Extensions.Options;

namespace JobService.Results;

/// <summary>
/// Verifies the results blob (readable, valid JSON, one result per document), then calls <see cref="Domain.ProcessingState.Complete"/>.
/// The results are not copied anywhere: the blob stays their single home, and "Completed" guarantees it can be read.
/// Idempotent: a duplicate request re-verifies harmlessly and the second Complete is declined by the state.
/// Retries reads; after <see cref="ResultsIngestionOptions.MaxAttempts"/> failures the job becomes Failed(ResultsUnavailable).
/// </summary>
public sealed class ResultsIngestor(
    IResultsIngestionQueue queue,
    IJobRepository repository,
    IBlobStore blobStore,
    TimeProvider timeProvider,
    IOptions<ResultsIngestionOptions> options,
    ILogger<ResultsIngestor> logger) : BackgroundService
{
    /// <summary>Ingests one job's results, including retries. Public so it can be tested without the hosting loop.</summary>
    public Task IngestAsync(string jobId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    // TODO: consume queue.ReadAllAsync and call IngestAsync.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
