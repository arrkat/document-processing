using Contracts.Documents;
using Contracts.Jobs;

namespace JobService.Application;

/// <summary>Use cases behind the /api/jobs endpoints.</summary>
public interface IJobManager
{
    /// <summary>Validates, writes the documents blob, stores the job as Pending and enqueues it for dispatch.</summary>
    Task<CreateJobResult> CreateAsync(CreateJobRequest? request, CancellationToken cancellationToken = default);

    Task<PagedResult<JobSummary>> ListAsync(JobStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>State from the repository; for a Completed job the results are read from the results blob.</summary>
    /// <returns><c>null</c> if the job does not exist.</returns>
    Task<JobDetails?> GetAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>Reads the results blob. Results exist only for Completed jobs.</summary>
    /// <returns><c>null</c> if the job does not exist or is not Completed.</returns>
    Task<IReadOnlyList<DocumentResult>?> GetResultsAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pending → Cancelled locally (the Processor never had it). Processing → a cancel command to the Processor; the state does not
    /// change until the Processor reports ProcessingCancelled, so the Job Service never shows Cancelled while work continues.
    /// The Processor says it no longer knows the job → Failed(ProcessorTimeout) and AlreadyTerminal.
    /// </summary>
    Task<CancelJobResult> CancelAsync(string jobId, CancellationToken cancellationToken = default);
}
