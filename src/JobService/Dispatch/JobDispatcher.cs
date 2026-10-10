using JobService.Persistence;
using JobService.Processing;
using JobService.Settings;
using Microsoft.Extensions.Options;

namespace JobService.Dispatch;

/// <summary>
/// Drains <see cref="IJobDispatchQueue"/> and submits each job to the Processor, retrying Busy/Unavailable with exponential backoff.
/// After <see cref="DispatchOptions.MaxAttempts"/> failures (or a Rejected response) the job becomes Failed.
/// Skips jobs that are no longer Pending (e.g. cancelled while waiting).
/// </summary>
public sealed class JobDispatcher(
    IJobDispatchQueue queue,
    IJobRepository repository,
    IProcessorClient processorClient,
    TimeProvider timeProvider,
    IOptions<DispatchOptions> options,
    ILogger<JobDispatcher> logger) : BackgroundService
{
    /// <summary>Dispatches one job, including retries. Public so it can be tested without the hosting loop.</summary>
    public Task DispatchAsync(string jobId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    // TODO: consume queue.ReadAllAsync and call DispatchAsync (bounded parallelism so one retrying job doesn't block the rest).
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
