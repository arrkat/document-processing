using Contracts;
using Contracts.Processing;

namespace ProcessorService.Jobs;

/// <summary>
/// Thread-safe record of every job the Processor knows: status, progress, per-job cancellation and event sequence numbers.
/// Status follows Queued → Processing → Completed | Failed | Cancelled (Queued → Cancelled); the first terminal state wins
/// and all <c>TryMark*</c> methods return <c>false</c> once the job is terminal.
/// A job is registered (with its <see cref="CancellationTokenSource"/>) before it is enqueued, so it can be cancelled while queued.
/// </summary>
public interface IJobRegistry
{
    /// <returns><c>false</c> if the job id is already registered (idempotent resubmit).</returns>
    bool TryRegister(ProcessingJob job);

    /// <summary>Removes a job that was registered but could not be enqueued (queue full).</summary>
    void Unregister(string jobId);

    ProcessingJobStatus? GetStatus(string jobId);

    /// <summary>Current status of all known jobs; used to re-send state after a WebSocket reconnect.</summary>
    IReadOnlyList<ProcessingJobStatus> GetAll();

    /// <summary>Token that fires when the job is cancelled. Unknown job → <see cref="CancellationToken.None"/> is not returned; check the bool.</summary>
    bool TryGetCancellationToken(string jobId, out CancellationToken cancellationToken);

    CancelOutcome RequestCancel(string jobId);

    bool TryMarkStarted(string jobId);

    void ReportProgress(string jobId, int processedDocuments);

    bool TryMarkCompleted(string jobId, string resultsRef);

    bool TryMarkFailed(string jobId, ErrorCode errorCode, string? errorMessage);

    bool TryMarkCancelled(string jobId);

    /// <summary>Next per-job event sequence number (1, 2, 3, …).</summary>
    long NextSequence(string jobId);
}
