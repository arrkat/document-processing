using Contracts;
using Contracts.Events;
using Contracts.Processing;
using ProcessorService.Jobs;

namespace ProcessorService.Events;

public sealed class JobEventFactory(IJobRegistry registry, TimeProvider timeProvider) : IJobEventFactory
{
    public JobStartedEvent Started(string jobId, int totalDocuments) => throw new NotImplementedException();

    public JobProgressEvent Progress(string jobId, int processedDocuments, int totalDocuments) => throw new NotImplementedException();

    public JobCompletedEvent Completed(string jobId, string resultsRef) =>
        throw new NotImplementedException();

    public JobFailedEvent Failed(string jobId, ErrorCode errorCode, string? errorMessage) => throw new NotImplementedException();

    public JobCancelledEvent Cancelled(string jobId, int processedDocuments) => throw new NotImplementedException();

    public JobEvent Snapshot(ProcessingJobStatus status) => throw new NotImplementedException();
}
