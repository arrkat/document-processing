using Contracts;
using Contracts.Events;
using Contracts.Processing;

namespace ProcessorService.Events;

/// <summary>Creates events with a fresh EventId, the job's next sequence number (<see cref="Jobs.IJobRegistry.NextSequence"/>) and the current time.</summary>
public interface IJobEventFactory
{
    JobStartedEvent Started(string jobId, int totalDocuments);

    JobProgressEvent Progress(string jobId, int processedDocuments, int totalDocuments);

    JobCompletedEvent Completed(string jobId, string resultsRef, int succeededDocuments, int failedDocuments);

    JobFailedEvent Failed(string jobId, ErrorCode errorCode, string? errorMessage);

    JobCancelledEvent Cancelled(string jobId, int processedDocuments);

    /// <summary>Event that re-states a job's current status; sent for every known job after a WebSocket reconnect.</summary>
    JobEvent Snapshot(ProcessingJobStatus status);
}
