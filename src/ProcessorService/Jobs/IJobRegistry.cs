using Contracts;
using Contracts.Processing;

namespace ProcessorService.Jobs;

/// <summary>
/// Thread-safe record of every job the Processor has accepted: status, progress, per-job cancellation and event sequence numbers.
/// <para>Status: Processing → Finished | Failed | Cancelled. The first terminal state wins: every <c>TryMark*</c> returns <c>false</c> once the job is terminal.</para>
/// <para>Finished jobs are kept (never evicted), so a late retry of the same jobId is recognised and not run twice.</para>
/// </summary>
public interface IJobRegistry
{
    /// <summary>Atomically registers the job as Processing with a fresh cancellation source.</summary>
    /// <returns><c>false</c> if the jobId is already registered (idempotent resubmit).</returns>
    bool TryRegister(ProcessingJob job);

    ProcessingJobStatus? GetStatus(string jobId);

    /// <summary>Status of every known job; sent as a snapshot after a WebSocket reconnect.</summary>
    IReadOnlyList<ProcessingJobStatus> GetAll();

    /// <summary>The token fires when the job is cancelled through <see cref="RequestCancel"/>.</summary>
    bool TryGetCancellationToken(string jobId, out CancellationToken cancellationToken);

    CancelOutcome RequestCancel(string jobId);

    void ReportProgress(string jobId, int processedDocuments);

    bool TryMarkFinished(string jobId, string resultsRef);

    bool TryMarkFailed(string jobId, ErrorCode errorCode, string? errorMessage);

    bool TryMarkCancelled(string jobId);

    /// <summary>Next per-job event sequence number (1, 2, 3, …).</summary>
    long NextSequence(string jobId);
}
