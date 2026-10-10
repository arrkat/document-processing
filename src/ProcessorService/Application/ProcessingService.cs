using Contracts.Processing;
using ProcessorService.Jobs;
using ProcessorService.Queue;
using ProcessorService.Validation;

namespace ProcessorService.Application;

public sealed class ProcessingService(
    IProcessJobRequestValidator validator,
    IJobRegistry registry,
    IProcessingQueue queue,
    ILogger<ProcessingService> logger) : IProcessingService
{
    public SubmitOutcome Submit(ProcessJobRequest? request) => throw new NotImplementedException();

    public ProcessingJobStatus? GetStatus(string jobId) => throw new NotImplementedException();

    public CancelOutcome Cancel(string jobId) => throw new NotImplementedException();
}
