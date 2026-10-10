using System.Collections.Concurrent;
using Contracts;
using Contracts.Processing;

namespace ProcessorService.Jobs;

public sealed class JobRegistry : IJobRegistry
{
    private readonly ConcurrentDictionary<string, Entry> _jobs = new(StringComparer.Ordinal);

    public bool TryRegister(ProcessingJob job) => _jobs.TryAdd(job.JobId, new Entry(job));

    public ProcessingJobStatus? GetStatus(string jobId) => _jobs.TryGetValue(jobId, out var entry) ? entry.Snapshot() : null;

    public IReadOnlyList<ProcessingJobStatus> GetAll() => _jobs.Values.Select(entry => entry.Snapshot()).ToList();

    public bool TryGetCancellationToken(string jobId, out CancellationToken cancellationToken)
    {
        cancellationToken = _jobs.TryGetValue(jobId, out var entry) ? entry.Cancellation.Token : CancellationToken.None;
        return entry is not null;
    }

    public CancelProcessingJobResult RequestCancel(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var entry))
            return CancelProcessingJobResult.NotFound;

        lock (entry.Gate)
        {
            if (entry.Status != ProcessingStatus.Processing)
                return CancelProcessingJobResult.AlreadyFinished;
        }

        // Outside the lock: Cancel() runs registered callbacks synchronously, and code must never run foreign
        // callbacks while holding a lock. If the job finishes in between, the cancel is a harmless no-op
        // (the first terminal state wins).
        entry.Cancellation.Cancel();
        return CancelProcessingJobResult.CancellationRequested;
    }

    public void ReportProgress(string jobId, int processedDocuments)
    {
        var entry = Get(jobId);
        lock (entry.Gate)
        {
            if (entry.Status == ProcessingStatus.Processing)
                entry.ProcessedDocuments = Math.Max(entry.ProcessedDocuments, processedDocuments);
        }
    }

    public bool TryMarkFinished(string jobId, string resultsRef) =>
        TryFinish(jobId, ProcessingStatus.Finished, entry =>
        {
            entry.ProcessedDocuments = entry.TotalDocuments;
            entry.ResultsRef = resultsRef;
        });

    public bool TryMarkFailed(string jobId, ErrorCode errorCode, string? errorMessage) =>
        TryFinish(jobId, ProcessingStatus.Failed, entry =>
        {
            entry.ErrorCode = errorCode;
            entry.ErrorMessage = errorMessage;
        });

    public bool TryMarkCancelled(string jobId) => TryFinish(jobId, ProcessingStatus.Cancelled, _ => { });

    public long NextSequence(string jobId) => Interlocked.Increment(ref Get(jobId).Sequence);

    private bool TryFinish(string jobId, ProcessingStatus terminal, Action<Entry> apply)
    {
        var entry = Get(jobId);
        lock (entry.Gate)
        {
            if (entry.Status != ProcessingStatus.Processing)
                return false; // the first terminal state wins

            entry.Status = terminal;
            apply(entry);
            return true;
        }
    }

    private Entry Get(string jobId) =>
        _jobs.TryGetValue(jobId, out var entry)
            ? entry
            : throw new InvalidOperationException($"Job '{jobId}' is not registered.");

    /// <summary>Mutable per-job record. Its fields are guarded by <see cref="Gate"/> (except <see cref="Sequence"/>, which is Interlocked).</summary>
    private sealed class Entry(ProcessingJob job)
    {
        /// <summary>The only lock for this entry. A dedicated object, never <c>this</c>, so no other code can take the same lock.</summary>
        public object Gate { get; } = new();

        public string JobId { get; } = job.JobId;
        public int TotalDocuments { get; } = job.DocumentCount;

        // Never disposed: it has no timer, and finished jobs stay in the registry.
        public CancellationTokenSource Cancellation { get; } = new();

        public ProcessingStatus Status { get; set; } = ProcessingStatus.Processing;
        public int ProcessedDocuments { get; set; }
        public string? ResultsRef { get; set; }
        public ErrorCode? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public long Sequence;

        public ProcessingJobStatus Snapshot()
        {
            lock (Gate)
                return new ProcessingJobStatus(JobId, Status, ProcessedDocuments, TotalDocuments, ResultsRef, ErrorCode, ErrorMessage);
        }
    }
}
