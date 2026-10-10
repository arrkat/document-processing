using Contracts.Jobs;

namespace JobService.Domain;

/// <summary>Read model combining the immutable metadata with the current state.</summary>
public sealed record Job(JobMetadata Metadata, JobState State)
{
    /// <summary>Flattens the polymorphic state into the public API shape.</summary>
    public JobSummary ToSummary() => State switch
    {
        PendingState s => Summary(s, processed: 0, startedAt: null, completedAt: null),
        ProcessingState s => Summary(s, s.ProcessedDocuments, s.StartedAt, completedAt: null),
        CompletedState s => Summary(s, Metadata.TotalDocuments, s.StartedAt, s.CompletedAt),
        FailedState s => Summary(s, s.ProcessedDocuments, s.StartedAt, completedAt: null, s.ErrorCode, s.ErrorMessage),
        CancelledState s => Summary(s, s.ProcessedDocuments, s.StartedAt, completedAt: null),
        _ => throw new InvalidOperationException($"Unknown job state {State.GetType().Name}.")
    };

    private JobSummary Summary(JobState state, int processed, DateTimeOffset? startedAt, DateTimeOffset? completedAt,
        Contracts.ErrorCode? errorCode = null, string? errorMessage = null) =>
        new(Metadata.Id, state.Status, Metadata.TotalDocuments, processed, Metadata.CreatedAt, startedAt, completedAt,
            LastUpdatedAt: state.LastActivityAt, errorCode, errorMessage);
}
