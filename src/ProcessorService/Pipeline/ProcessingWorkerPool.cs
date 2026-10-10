using Microsoft.Extensions.Options;
using ProcessorService.Queue;
using ProcessorService.Settings;

namespace ProcessorService.Pipeline;

/// <summary>
/// Runs <see cref="ProcessingOptions.WorkerCount"/> workers, each reading jobs from <see cref="IProcessingQueue"/> and calling
/// <see cref="IJobProcessor.ProcessAsync"/>. Jobs cancelled while queued are skipped.
/// On shutdown: stop reading, let in-flight jobs observe the stopping token (→ JobFailed(ProcessorShuttingDown)).
/// </summary>
public sealed class ProcessingWorkerPool(
    IProcessingQueue queue,
    IJobProcessor processor,
    IOptions<ProcessingOptions> options,
    ILogger<ProcessingWorkerPool> logger) : BackgroundService
{
    // TODO: start WorkerCount consumer loops over queue.ReadAllAsync and await them all.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
