using Contracts.Processing;
using ProcessorService.Jobs;

namespace ProcessorService.Application;

/// <summary>Use cases behind the /api/processing-jobs endpoints.</summary>
public interface IProcessingService
{
    /// <summary>Validate → register (idempotent on jobId) → enqueue; unregisters if the queue is full.</summary>
    SubmitOutcome Submit(ProcessJobRequest? request);

    ProcessingJobStatus? GetStatus(string jobId);

    CancelOutcome Cancel(string jobId);
}
