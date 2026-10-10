using BlobStorage;
using JobService.Persistence;
using JobService.Settings;
using Microsoft.Extensions.Options;

namespace JobService.Results;

/// <summary>
/// Reads the results blob, saves the results, then calls <see cref="Domain.ProcessingState.Complete"/>.
/// Idempotent: duplicate requests re-read the blob harmlessly and the second Complete is declined by the state.
/// Retries blob reads; after <see cref="ResultsIngestionOptions.MaxAttempts"/> failures the job becomes Failed(ResultsUnavailable).
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
