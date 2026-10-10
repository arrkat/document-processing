using System.Threading.Channels;
using Contracts;
using Contracts.Processing;
using JobService.Domain;
using JobService.Persistence;
using JobService.Processing;
using JobService.Settings;
using Microsoft.Extensions.Options;

namespace JobService.Dispatch;

/// <summary>
/// The Job Service's single waiting line. Sends one job at a time, in submission (FIFO) order, so jobs start in order:
/// <list type="bullet">
/// <item>Before every attempt: if the job is no longer Pending (cancelled, or events already moved it on), drop it.</item>
/// <item>Accepted (202, or 200 already known) → the Processor has started the job: move it Pending → Processing.
/// If that update finds the job already Cancelled (cancel raced with the dispatch), send the Processor a compensating cancel.</item>
/// <item>Busy (503) → not a failure and not an attempt: keep the same job at the head and wait
/// <see cref="DispatchOptions.BusyRetryDelay"/> or until <see cref="IDispatchWakeUp.SlotFreed"/>, whichever comes first.</item>
/// <item>Unavailable (connection error / timeout / 5xx) → exponential backoff; after <see cref="DispatchOptions.MaxAttempts"/>
/// → Failed(ProcessorUnavailable).</item>
/// <item>Rejected (400) → Failed(ProcessorRejected), no retry.</item>
/// </list>
/// The Processor is idempotent on jobId, so a retry after a lost reply never runs the job twice.
/// </summary>
public sealed class JobDispatcher(
    ChannelReader<DispatchRequest> queue,
    IJobRepository repository,
    IProcessorClient processorClient,
    TimeProvider timeProvider,
    IOptions<DispatchOptions> options,
    ILogger<JobDispatcher> logger) : BackgroundService, IDispatchWakeUp
{
    private readonly DispatchOptions _options = options.Value;

    // At most one pending wake-up: several terminal events while nothing is waiting collapse into a single early retry.
    // Deliberately not disposed: its wait handle is never used, and an event arriving during shutdown may still call SlotFreed().
    private readonly SemaphoreSlim _slotFreed = new(initialCount: 0, maxCount: 1);

    /// <summary>Dispatches one job, including retries. Public so it can be tested without the hosting loop.</summary>
    public async Task DispatchAsync(DispatchRequest request, CancellationToken cancellationToken)
    {
        var failedAttempts = 0;
        var backoff = _options.InitialDelay;

        while (true)
        {
            var job = await repository.GetAsync(request.JobId, cancellationToken);
            if (job?.State is not PendingState)
            {
                logger.LogInformation("Job {JobId} is no longer Pending ({Status}); not dispatching", request.JobId, job?.State.Status);
                return;
            }

            var result = await processorClient.SubmitAsync(
                new ProcessJobRequest(job.Metadata.Id, job.Metadata.DocumentsRef, job.Metadata.TotalDocuments), cancellationToken);

            switch (result)
            {
                case DispatchResult.Accepted:
                    await MarkHandedOverAsync(request.JobId, cancellationToken);
                    return;

                case DispatchResult.Rejected:
                    await FailAsync(request.JobId, ErrorCode.ProcessorRejected, "The Processor rejected the job as invalid.", cancellationToken);
                    return;

                case DispatchResult.Busy:
                    // Not a failure: wait for a free slot (or the busy delay) and retry the same job.
                    logger.LogDebug("Processor busy; job {JobId} waits at the head of the line", request.JobId);
                    await _slotFreed.WaitAsync(_options.BusyRetryDelay, cancellationToken);
                    continue;

                case DispatchResult.Unavailable:
                    failedAttempts++;
                    if (failedAttempts >= _options.MaxAttempts)
                    {
                        await FailAsync(request.JobId, ErrorCode.ProcessorUnavailable,
                            $"The Processor could not be reached after {failedAttempts} attempts.", cancellationToken);
                        return;
                    }

                    logger.LogWarning("Processor unreachable for job {JobId} (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}",
                        request.JobId, failedAttempts, _options.MaxAttempts, backoff);
                    await Task.Delay(backoff, timeProvider, cancellationToken);
                    backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, _options.MaxDelay.Ticks));
                    continue;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown dispatch result.");
            }
        }
    }

    public void SlotFreed()
    {
        try
        {
            _slotFreed.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake-up is already pending.
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // One job at a time, in order: the next job is not sent until this one is handed over or has failed.
            await foreach (var request in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await DispatchAsync(request, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Dispatching job {JobId} failed unexpectedly", request.JobId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    /// <summary>The Processor accepted the job: it is Processing now. If a cancel won the race, take the job back from the Processor.</summary>
    private async Task MarkHandedOverAsync(string jobId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var update = await repository.TryUpdateStateAsync(jobId,
            state => state is PendingState pending ? pending.Start(now, pending.LastEventSequence) : null, cancellationToken);

        if (update.Status == StateUpdateStatus.Updated)
        {
            logger.LogInformation("Job {JobId} handed over to the Processor", jobId);
            return;
        }

        if (update.State is CancelledState)
        {
            var answer = await processorClient.CancelAsync(jobId, cancellationToken);
            logger.LogInformation("Job {JobId} was cancelled while being dispatched; compensating cancel sent: {Answer}", jobId, answer);
        }
        // Otherwise a processing event already moved it on; nothing to do.
    }

    private async Task FailAsync(string jobId, ErrorCode errorCode, string message, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var update = await repository.TryUpdateStateAsync(jobId,
            state => state is PendingState pending ? pending.Fail(errorCode, message, now) : null, cancellationToken);

        if (update.Status == StateUpdateStatus.Updated)
            logger.LogWarning("Job {JobId} failed: {ErrorCode} {Message}", jobId, errorCode, message);
    }
}
