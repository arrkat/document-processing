using BlobStorage;
using Contracts;
using Contracts.Jobs;
using Contracts.Processing;
using JobService.Domain;
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
    private readonly WatchdogOptions _options = options.Value;

    /// <summary>One reconciliation pass. Public so it can be tested and triggered on WebSocket reconnect.</summary>
    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var job in await repository.ListByStatusAsync(JobStatus.Processing, cancellationToken))
        {
            if (now - job.State.LastActivityAt >= _options.StaleAfter)
                await ReconcileAsync(job.Metadata.Id, cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.ScanInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ScanAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Watchdog scan failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task ReconcileAsync(string jobId, CancellationToken cancellationToken)
    {
        if (await blobStore.ExistsAsync(BlobKey.JobResults(jobId), cancellationToken))
        {
            await UpdateAsync(jobId, (processing, now) => processing.Complete(now), "results found", cancellationToken);
            return;
        }

        ProcessingJobStatus? status;
        try
        {
            status = await processorClient.GetStatusAsync(jobId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Job {JobId} has been silent and the Processor is unreachable ({Reason}); keeping it Processing", jobId, ex.Message);
            return;
        }

        switch (status?.Status)
        {
            case null:
                await UpdateAsync(jobId, (processing, now) => processing.Fail(ErrorCode.ProcessorTimeout,
                    "The Processor no longer knows this job (it probably restarted).", now), "Processor lost the job", cancellationToken);
                break;
            case ProcessingStatus.Finished:
                await UpdateAsync(jobId, (processing, now) => processing.Complete(now), "Processor reports Finished", cancellationToken);
                break;
            case ProcessingStatus.Failed:
                await UpdateAsync(jobId, (processing, now) => processing.Fail(status.ErrorCode ?? ErrorCode.Unexpected, status.ErrorMessage, now),
                    "Processor reports Failed", cancellationToken);
                break;
            case ProcessingStatus.Cancelled:
                await UpdateAsync(jobId, (processing, now) => processing.Cancel(now) with { ProcessedDocuments = status.ProcessedDocuments },
                    "Processor reports Cancelled", cancellationToken);
                break;
            case ProcessingStatus.Processing:
                logger.LogDebug("Job {JobId} is quiet but still processing", jobId);
                break;
        }
    }

    private async Task UpdateAsync(string jobId, Func<ProcessingState, DateTimeOffset, JobState> transition, string reason,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var update = await repository.TryUpdateStateAsync(jobId,
            state => state is ProcessingState processing ? transition(processing, now) : null, cancellationToken);

        if (update.Status == StateUpdateStatus.Updated)
            logger.LogInformation("Watchdog resolved silent job {JobId} as {Status}: {Reason}", jobId, update.State!.Status, reason);
    }
}
