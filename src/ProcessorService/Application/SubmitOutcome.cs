namespace ProcessorService.Application;

public enum SubmitStatus
{
    Accepted,

    /// <summary>Job id already known — idempotent resubmit, nothing new was queued.</summary>
    AlreadyExists,
    Invalid,
    QueueFull
}

/// <param name="Errors">Set only for <see cref="SubmitStatus.Invalid"/>.</param>
public sealed record SubmitOutcome(SubmitStatus Status, IReadOnlyDictionary<string, string[]>? Errors = null);
