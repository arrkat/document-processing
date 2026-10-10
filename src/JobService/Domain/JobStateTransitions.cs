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
/// The Job Service records what the Processor reports; it decides nothing about processing itself.
/// <list type="bullet">
/// <item>Sequence ≤ the last applied one → <see cref="IgnoreReason.Duplicate"/>.</item>
/// <item>Any processing event on a Pending job first starts it (implicit start): the Processor clearly has the job, even if
/// ProcessingStarted or the dispatcher's 202 were lost.</item>
/// <item>Started → Processing · Progress → count updated · Finished → Completed · Failed → Failed · Cancelled → Cancelled.</item>
/// <item>Any event on a terminal job → <see cref="IgnoreReason.InvalidTransition"/>.</item>
/// </list>
/// PURE: no I/O, no clock, no logging. It runs inside the repository's atomic update callback, which may execute more than once,
/// so side effects (logging, waking the dispatcher) belong to the caller, after the update.
/// </summary>
public static class JobStateTransitions
{
    /// <param name="receivedAt">When the Job Service received the event. All timestamps record when the Job Service learned of a change,
    /// which is what <c>LastUpdatedAt</c> and the watchdog need, and it avoids depending on the two machines' clocks agreeing.</param>
    public static TransitionResult Apply(JobState current, ProcessingEvent incoming, DateTimeOffset receivedAt)
    {
        if (incoming.Sequence <= current.LastEventSequence)
            return TransitionResult.Ignore(IgnoreReason.Duplicate);

        var processing = current switch
        {
            PendingState pending => pending.Start(receivedAt, incoming.Sequence), // implicit start
            ProcessingState running => running,
            _ => null // Completed, Failed, Cancelled: nothing can change a terminal job
        };
        if (processing is null)
            return TransitionResult.Ignore(IgnoreReason.InvalidTransition);

        JobState next = incoming switch
        {
            ProcessingStartedEvent => processing,
            ProcessingProgressUpdatedEvent progress => processing.Progress(progress.ProcessedDocuments, receivedAt, incoming.Sequence),
            ProcessingFinishedEvent => processing.Complete(receivedAt),
            ProcessingFailedEvent failed => processing.Fail(failed.ErrorCode, failed.ErrorMessage, receivedAt),
            ProcessingCancelledEvent cancelled => processing.Cancel(receivedAt) with { ProcessedDocuments = cancelled.ProcessedDocuments },
            _ => throw new ArgumentOutOfRangeException(nameof(incoming), incoming.GetType().Name, "Unknown processing event.")
        };

        return TransitionResult.Changed(next with { LastActivityAt = receivedAt, LastEventSequence = incoming.Sequence });
    }
}
