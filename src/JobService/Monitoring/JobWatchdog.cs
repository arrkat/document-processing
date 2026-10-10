using BlobStorage;
using JobService.Persistence;
using JobService.Processing;
using JobService.Results;
using JobService.Settings;
using Microsoft.Extensions.Options;

namespace JobService.Monitoring;

/// <summary>
/// Reconciles Processing jobs with no activity for <see cref="WatchdogOptions.StaleAfter"/>, so a lost event never leaves a job stuck:
/// results blob exists → enqueue ingestion; Processor reports Failed/Cancelled → apply it;
/// Processor does not know the job (restarted) → Failed(ProcessorTimeout); Processor unreachable → try again on the next scan.
/// </summary>
public sealed class JobWatchdog(
    IJobRepository repository,
    IBlobStore blobStore,
    IProcessorClient processorClient,
    IResultsIngestionQueue resultsQueue,
    TimeProvider timeProvider,
    IOptions<WatchdogOptions> options,
    ILogger<JobWatchdog> logger) : BackgroundService
{
    /// <summary>One reconciliation pass. Public so it can be tested and triggered on WebSocket reconnect.</summary>
    public Task ScanAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    // TODO: call ScanAsync every options.ScanInterval (PeriodicTimer created from timeProvider).
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
