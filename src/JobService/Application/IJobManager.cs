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

    /// <summary>Moves the job to Cancelled (first terminal state wins) and asks the Processor to stop, best effort.</summary>
    Task<CancelJobResult> CancelAsync(string jobId, CancellationToken cancellationToken = default);
}
