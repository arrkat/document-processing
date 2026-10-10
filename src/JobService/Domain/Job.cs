using Contracts.Jobs;

namespace JobService.Domain;

/// <summary>Read model combining the immutable metadata with the current state.</summary>
public sealed record Job(JobMetadata Metadata, JobState State)
{
    /// <summary>Flattens the polymorphic state into the public API shape.</summary>
    public JobSummary ToSummary() => throw new NotImplementedException();
}
