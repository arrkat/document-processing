using Contracts.Processing;
using ProcessorService.Jobs;
using ProcessorService.Processing;
using ProcessorService.Validation;

namespace ProcessorService.Application;

public sealed class ProcessingJobManager(
    IProcessJobRequestValidator validator,
    IJobRegistry registry,
    IJobRunner runner,
    ILogger<ProcessingJobManager> logger) : IProcessingJobManager
{
    public SubmitProcessingJobResult Submit(ProcessJobRequest? request)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            logger.LogWarning("Rejected processing request for job {JobId}: {Fields}", request?.JobId, string.Join(", ", errors.Keys));
            return new SubmitProcessingJobResult(SubmitProcessingJobStatus.Invalid, errors);
        }

        var result = runner.TryStart(new ProcessingJob(request!.JobId, request.DocumentsRef, request.DocumentCount));
        return result switch
        {
            StartJobResult.Started => new SubmitProcessingJobResult(SubmitProcessingJobStatus.Accepted),
            StartJobResult.AlreadyKnown => new SubmitProcessingJobResult(SubmitProcessingJobStatus.AlreadyExists),
            StartJobResult.Busy => new SubmitProcessingJobResult(SubmitProcessingJobStatus.Busy),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown start result.")
        };
    }

    public ProcessingJobStatus? GetStatus(string jobId) => registry.GetStatus(jobId);

    public CancelProcessingJobResult Cancel(string jobId)
    {
        var result = registry.RequestCancel(jobId);
        logger.LogInformation("Cancel requested for job {JobId}: {Result}", jobId, result);
        return result;
    }
}
