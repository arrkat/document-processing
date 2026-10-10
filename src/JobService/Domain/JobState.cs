using Contracts;
using Contracts.Jobs;

namespace JobService.Domain;

/// <summary>
/// Immutable job state. Each lifecycle stage is its own type, and transition methods exist only where the transition is legal:
/// <code>
/// Pending ──Start──► Processing ──Complete──► Completed
///    ├──Fail──► Failed ◄──┤
///    └──Cancel─► Cancelled ◄┘
/// </code>
/// Terminal states have no transition methods. A new state replaces the old one atomically via
/// <see cref="Persistence.IJobRepository.TryUpdateStateAsync"/>.
/// </summary>
/// <param name="LastActivityAt">Last time anything happened to the job (used by the watchdog).</param>
/// <param name="LastEventSequence">Highest Processor event sequence applied; lower or equal sequences are duplicates/stale.</param>
public abstract record JobState(DateTimeOffset LastActivityAt, long LastEventSequence)
{
    public abstract JobStatus Status { get; }
}

public sealed record PendingState(DateTimeOffset LastActivityAt, long LastEventSequence)
    : JobState(LastActivityAt, LastEventSequence)
{
    public override JobStatus Status => JobStatus.Pending;

    public static PendingState Create(DateTimeOffset createdAt) => throw new NotImplementedException();

    public ProcessingState Start(DateTimeOffset at, long sequence) => throw new NotImplementedException();

    public FailedState Fail(ErrorCode errorCode, string? errorMessage, DateTimeOffset at) => throw new NotImplementedException();

    public CancelledState Cancel(DateTimeOffset at) => throw new NotImplementedException();
}

public sealed record ProcessingState(
    DateTimeOffset LastActivityAt,
    long LastEventSequence,
    DateTimeOffset StartedAt,
    int ProcessedDocuments)
    : JobState(LastActivityAt, LastEventSequence)
{
    public override JobStatus Status => JobStatus.Processing;

    public ProcessingState Progress(int processedDocuments, DateTimeOffset at, long sequence) => throw new NotImplementedException();

    /// <summary>Called by the results ingestor once the results are stored — not directly by the ProcessingFinished event.</summary>
    public CompletedState Complete(DateTimeOffset at) => throw new NotImplementedException();

    public FailedState Fail(ErrorCode errorCode, string? errorMessage, DateTimeOffset at) => throw new NotImplementedException();

    public CancelledState Cancel(DateTimeOffset at) => throw new NotImplementedException();
}

public sealed record CompletedState(
    DateTimeOffset LastActivityAt,
    long LastEventSequence,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
    : JobState(LastActivityAt, LastEventSequence)
{
    public override JobStatus Status => JobStatus.Completed;
}

/// <param name="StartedAt">Null when the job failed before processing started (e.g. Processor unreachable).</param>
public sealed record FailedState(
    DateTimeOffset LastActivityAt,
    long LastEventSequence,
    DateTimeOffset? StartedAt,
    DateTimeOffset FailedAt,
    int ProcessedDocuments,
    ErrorCode ErrorCode,
    string? ErrorMessage)
    : JobState(LastActivityAt, LastEventSequence)
{
    public override JobStatus Status => JobStatus.Failed;
}

/// <param name="StartedAt">Null when the job was cancelled before processing started.</param>
public sealed record CancelledState(
    DateTimeOffset LastActivityAt,
    long LastEventSequence,
    DateTimeOffset? StartedAt,
    DateTimeOffset CancelledAt,
    int ProcessedDocuments)
    : JobState(LastActivityAt, LastEventSequence)
{
    public override JobStatus Status => JobStatus.Cancelled;
}
