using System.Threading.Channels;
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
    /// <summary>Dispatches one job, including retries. Public so it can be tested without the hosting loop.</summary>
    public Task DispatchAsync(DispatchRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public void SlotFreed() => throw new NotImplementedException();

    // TODO: read the queue and call DispatchAsync for one job at a time (FIFO).
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
