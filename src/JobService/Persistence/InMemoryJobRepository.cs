using System.Collections.Concurrent;
using Contracts.Jobs;
using JobService.Domain;

namespace JobService.Persistence;

/// <summary>
/// Jobs in a <see cref="ConcurrentDictionary{TKey,TValue}"/> of immutable <see cref="Job"/> records. State updates are an optimistic
/// compare-and-swap loop: read, compute the new state, and replace only if nobody changed the job in between, otherwise retry.
/// </summary>
public sealed class InMemoryJobRepository : IJobRepository
{
    private readonly ConcurrentDictionary<string, Job> _jobs = new(StringComparer.Ordinal);

    public Task<bool> TryAddAsync(JobMetadata metadata, JobState initialState, CancellationToken cancellationToken = default) =>
        Task.FromResult(_jobs.TryAdd(metadata.Id, new Job(metadata, initialState)));

    public Task<Job?> GetAsync(string jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_jobs.GetValueOrDefault(jobId));

    public Task<PagedResult<Job>> ListAsync(JobStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var matching = _jobs.Values
            .Where(job => status is null || job.State.Status == status)
            .OrderByDescending(job => job.Metadata.CreatedAt)
            .ThenBy(job => job.Metadata.Id, StringComparer.Ordinal)
            .ToList();

        var items = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<Job>(items, page, pageSize, matching.Count));
    }

    public Task<IReadOnlyList<Job>> ListByStatusAsync(JobStatus status, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Job>>(_jobs.Values.Where(job => job.State.Status == status).ToList());

    public Task<StateUpdateResult> TryUpdateStateAsync(string jobId, Func<JobState, JobState?> update, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (!_jobs.TryGetValue(jobId, out var current))
                return Task.FromResult(new StateUpdateResult(StateUpdateStatus.NotFound, null));

            var next = update(current.State);
            if (next is null)
                return Task.FromResult(new StateUpdateResult(StateUpdateStatus.Declined, current.State));

            if (_jobs.TryUpdate(jobId, current with { State = next }, current))
                return Task.FromResult(new StateUpdateResult(StateUpdateStatus.Updated, next));
            // Another thread changed the job between the read and the swap: retry against its new state.
        }
    }
}
