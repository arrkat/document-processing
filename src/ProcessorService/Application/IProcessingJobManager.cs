using Contracts.Processing;
using ProcessorService.Jobs;

namespace ProcessorService.Application;

/// <summary>
/// The Processor's application service: the use cases behind the /api/processing-jobs endpoints (the "waiter").
/// It validates and routes; admission and processing belong to <see cref="Processing.IJobRunner"/>, job state to <see cref="Jobs.IJobRegistry"/>.
/// </summary>
public interface IProcessingJobManager
{
    /// <summary>Validates the request; only a valid request is handed to <see cref="Processing.IJobRunner.TryStart"/>, which owns admission.</summary>
    SubmitProcessingJobResult Submit(ProcessJobRequest? request);

    ProcessingJobStatus? GetStatus(string jobId);

    CancelProcessingJobResult Cancel(string jobId);
}
