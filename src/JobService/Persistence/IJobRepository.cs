using Contracts.Documents;
using Contracts.Jobs;
using JobService.Domain;

namespace JobService.Persistence;

/// <summary>
/// Job storage. In-memory today; this interface is what a persistent implementation replaces.
/// Metadata is insert-only; state changes only through <see cref="TryUpdateStateAsync"/>.
/// </summary>
public interface IJobRepository
{
    /// <returns><c>false</c> if a job with the same id already exists.</returns>
    Task<bool> TryAddAsync(JobMetadata metadata, JobState initialState, CancellationToken cancellationToken = default);

    Task<Job?> GetAsync(string jobId, CancellationToken cancellationToken = default);

    /// <param name="page">1-based page number. Ordered by creation time, newest first.</param>
    Task<PagedResult<Job>> ListAsync(JobStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Job>> ListByStatusAsync(JobStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces the job's state with the result of <paramref name="update"/> (compare-and-swap).
    /// The updater may run more than once under contention and must be side-effect free. Returning <c>null</c> means "no change".
    /// </summary>
    Task<StateUpdateResult> TryUpdateStateAsync(string jobId, Func<JobState, JobState?> update, CancellationToken cancellationToken = default);

    Task SaveResultsAsync(string jobId, IReadOnlyList<DocumentResult> results, CancellationToken cancellationToken = default);

    /// <returns><c>null</c> if no results were saved for the job.</returns>
    Task<IReadOnlyList<DocumentResult>?> GetResultsAsync(string jobId, CancellationToken cancellationToken = default);
}
