using BlobStorage;
using JobService.Persistence;
using JobService.Processing;
using JobService.Settings;
using Microsoft.Extensions.Options;

namespace JobService.Monitoring;

/// <summary>
/// Reconciles Processing jobs with no activity for <see cref="WatchdogOptions.StaleAfter"/>, so a lost event never leaves a job stuck.
/// In order:
/// <list type="number">
/// <item>results blob exists → Completed (the Processor writes it atomically before reporting, so it is complete);</item>
/// <item>ask the Processor: Finished → Completed · Failed / Cancelled → apply it · still Processing → leave it;</item>
/// <item>the Processor does not know the job (it restarted) → Failed(ProcessorTimeout);</item>
/// <item>the Processor is unreachable → leave it Processing (the last known truth) and log a warning; check again next scan.</item>
/// </list>
/// It resolves a job only when it learns the truth (the blob, or the Processor's answer). It never guesses: an unreachable
/// Processor may still be working (e.g. a network split), so failing the job would be a lie. Clients see the silence through
/// <see cref="Contracts.Jobs.JobSummary.LastUpdatedAt"/>.
/// </summary>
public sealed class JobWatchdog(
    IJobRepository repository,
    IBlobStore blobStore,
    IProcessorClient processorClient,
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
