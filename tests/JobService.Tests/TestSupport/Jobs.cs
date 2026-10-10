using BlobStorage;
using Contracts;
using JobService.Domain;
using JobService.Persistence;

namespace JobService.Tests.TestSupport;

/// <summary>Builders for jobs in a given state.</summary>
public static class Jobs
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public static JobMetadata Metadata(string id, int documents = 3, DateTimeOffset? createdAt = null) =>
        new(id, documents, BlobKey.JobDocuments(id), createdAt ?? T0);

    public static PendingState PendingJob(long sequence = 0) => new(T0, sequence);

    public static ProcessingState ProcessingJob(long sequence = 0, int processed = 0) => new(T0, sequence, StartedAt: T0, processed);

    public static CompletedState CompletedJob(long sequence = 5) => new(T0, sequence, StartedAt: T0, CompletedAt: T0);

    public static FailedState FailedJob(long sequence = 5) => new(T0, sequence, StartedAt: T0, FailedAt: T0, 1, ErrorCode.Unexpected, "boom");

    public static CancelledState CancelledJob(long sequence = 5) => new(T0, sequence, StartedAt: T0, CancelledAt: T0, 1);

    public static async Task<string> AddAsync(IJobRepository repository, string id, JobState state, int documents = 3)
    {
        await repository.TryAddAsync(Metadata(id, documents), state);
        return id;
    }
}
