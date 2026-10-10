using Contracts.Documents;
using Contracts.Jobs;

namespace JobService.Application;

/// <summary>Use cases behind the /api/jobs endpoints.</summary>
public interface IJobManager
{
    /// <summary>Validates, writes the documents blob, stores the job as Pending and enqueues it for dispatch.</summary>
    Task<CreateJobOutcome> CreateAsync(CreateJobRequest? request, CancellationToken cancellationToken = default);

    Task<PagedResult<JobSummary>> ListAsync(JobStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <returns><c>null</c> if the job does not exist.</returns>
    Task<JobDetails?> GetAsync(string jobId, CancellationToken cancellationToken = default);

    /// <returns><c>null</c> if the job does not exist; an empty list if it has no results yet.</returns>
    Task<IReadOnlyList<DocumentResult>?> GetResultsAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>Moves the job to Cancelled (first terminal state wins) and asks the Processor to stop, best effort.</summary>
    Task<CancelJobOutcome> CancelAsync(string jobId, CancellationToken cancellationToken = default);
}
