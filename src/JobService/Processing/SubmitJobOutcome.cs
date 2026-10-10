namespace JobService.Processing;

public enum SubmitJobOutcome
{
    /// <summary>202 (new) or 200 (already known — idempotent resubmit).</summary>
    Accepted,

    /// <summary>503 / 429: Processor queue is full. Retryable.</summary>
    Busy,

    /// <summary>Connection failure, timeout or other 5xx. Retryable.</summary>
    Unavailable,

    /// <summary>400: Processor considers the request invalid. Not retryable.</summary>
    Rejected
}
