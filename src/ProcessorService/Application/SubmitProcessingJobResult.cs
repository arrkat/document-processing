namespace ProcessorService.Application;

public enum SubmitProcessingJobStatus
{
    /// <summary>202: the job started.</summary>
    Accepted,

    /// <summary>200: the jobId is already known (idempotent resubmit); nothing new started.</summary>
    AlreadyExists,

    /// <summary>400.</summary>
    Invalid,

    /// <summary>503 + Retry-After: all job slots are busy; nothing was registered.</summary>
    Busy
}

/// <param name="Errors">Set only for <see cref="SubmitProcessingJobStatus.Invalid"/>.</param>
public sealed record SubmitProcessingJobResult(SubmitProcessingJobStatus Status, IReadOnlyDictionary<string, string[]>? Errors = null);
