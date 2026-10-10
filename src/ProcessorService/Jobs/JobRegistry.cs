using Contracts;
using Contracts.Processing;

namespace ProcessorService.Jobs;

public sealed class JobRegistry(TimeProvider timeProvider) : IJobRegistry
{
    public bool TryRegister(ProcessingJob job) => throw new NotImplementedException();

    public void Unregister(string jobId) => throw new NotImplementedException();

    public ProcessingJobStatus? GetStatus(string jobId) => throw new NotImplementedException();

    public IReadOnlyList<ProcessingJobStatus> GetAll() => throw new NotImplementedException();

    public bool TryGetCancellationToken(string jobId, out CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public CancelOutcome RequestCancel(string jobId) => throw new NotImplementedException();

    public bool TryMarkStarted(string jobId) => throw new NotImplementedException();

    public void ReportProgress(string jobId, int processedDocuments) => throw new NotImplementedException();

    public bool TryMarkCompleted(string jobId, string resultsRef) => throw new NotImplementedException();

    public bool TryMarkFailed(string jobId, ErrorCode errorCode, string? errorMessage) => throw new NotImplementedException();

    public bool TryMarkCancelled(string jobId) => throw new NotImplementedException();

    public long NextSequence(string jobId) => throw new NotImplementedException();
}
