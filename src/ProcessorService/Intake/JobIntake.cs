using Contracts.Processing;
using ProcessorService.Jobs;
using ProcessorService.Processing;
using ProcessorService.Validation;

namespace ProcessorService.Intake;

public sealed class JobIntake(
    IProcessJobRequestValidator validator,
    IJobRegistry registry,
    IJobRunner runner,
    ILogger<JobIntake> logger) : IJobIntake
{
    public SubmitOutcome Submit(ProcessJobRequest? request) => throw new NotImplementedException();

    public ProcessingJobStatus? GetStatus(string jobId) => throw new NotImplementedException();

    public CancelOutcome Cancel(string jobId) => throw new NotImplementedException();
}
