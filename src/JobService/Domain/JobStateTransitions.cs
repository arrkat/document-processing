using Contracts.Events;

namespace JobService.Domain;

public enum IgnoreReason
{
    /// <summary>Event sequence is lower than or equal to the last applied one.</summary>
    Duplicate,

    /// <summary>The event makes no sense in the current state (e.g. progress for a Completed job).</summary>
    InvalidTransition
}

/// <summary>Exactly one of <see cref="NewState"/> / <see cref="Ignored"/> is set.</summary>
public sealed record TransitionResult(JobState? NewState, IgnoreReason? Ignored)
{
    public static TransitionResult Changed(JobState newState) => new(newState, null);
    public static TransitionResult Ignore(IgnoreReason reason) => new(null, reason);
}

/// <summary>
/// Maps untrusted Processor events (possibly duplicated, late or out of order) onto the legal transitions of <see cref="JobState"/>.
/// <para>
/// PURE: no I/O, no clock, no logging. It runs inside the repository's atomic update callback, which may execute more than once,
/// so side effects (enqueue results ingestion, logging) belong to the caller, after the update.
/// </para>
/// <para>
/// ProcessingFinished does not complete the job: it returns the Processing state with the new sequence/activity; the caller then enqueues
/// results ingestion, and only the ingestor calls <see cref="ProcessingState.Complete"/>.
/// </para>
/// </summary>
public static class JobStateTransitions
{
    public static TransitionResult Apply(JobState current, ProcessingEvent incoming) => throw new NotImplementedException();
}
