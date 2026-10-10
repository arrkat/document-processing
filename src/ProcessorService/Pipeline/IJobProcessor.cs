using ProcessorService.Jobs;

namespace ProcessorService.Pipeline;

/// <summary>
/// Runs one job end to end: mark started + JobStarted → load documents blob (missing → JobFailed(InputUnavailable)) →
/// analyse documents with bounded per-job parallelism, each under the global <see cref="Concurrency.IDocumentConcurrencyLimiter"/> →
/// throttled JobProgress → write results blob → mark completed + JobCompleted.
/// Cancellation → JobCancelled; unexpected exception → JobFailed(Unexpected). Never throws for job-level failures.
/// </summary>
public interface IJobProcessor
{
    Task ProcessAsync(ProcessingJob job, CancellationToken stoppingToken);
}
