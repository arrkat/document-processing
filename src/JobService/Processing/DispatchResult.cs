namespace JobService.Processing;

public enum DispatchResult
{
    /// <summary>202 (started) or 200 (already known — idempotent resubmit). Either way the Processor has the job.</summary>
    Accepted,

    /// <summary>503: all Processor job slots are busy. Retried without counting as an attempt.</summary>
    Busy,

    /// <summary>Connection failure, timeout or other 5xx. Retried with backoff, counted toward MaxAttempts.</summary>
    Unavailable,

    /// <summary>400: Processor considers the request invalid. Not retryable.</summary>
    Rejected
}
