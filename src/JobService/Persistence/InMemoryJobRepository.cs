using Contracts.Documents;
using Contracts.Jobs;
using JobService.Domain;

namespace JobService.Persistence;

public sealed class InMemoryJobRepository : IJobRepository
{
    public Task<bool> TryAddAsync(JobMetadata metadata, JobState initialState, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Job?> GetAsync(string jobId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<PagedResult<Job>> ListAsync(JobStatus? status, int page, int pageSize, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Job>> ListByStatusAsync(JobStatus status, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<StateUpdateResult> TryUpdateStateAsync(string jobId, Func<JobState, JobState?> update, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task SaveResultsAsync(string jobId, IReadOnlyList<DocumentResult> results, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<DocumentResult>?> GetResultsAsync(string jobId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
