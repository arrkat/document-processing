namespace JobService.Processing;

/// <summary>The Processor's answer to a cancel command.</summary>
public enum ProcessorCancelResult
{
    /// <summary>202: the Processor will stop the job and send ProcessingCancelled.</summary>
    CancellationRequested,

    /// <summary>409: the job already reached a final state on the Processor.</summary>
    AlreadyFinished,

    /// <summary>404: the Processor does not know the job (it restarted).</summary>
    NotFound,

    /// <summary>Connection failure, timeout or 5xx: the command was not delivered.</summary>
    Unavailable
}
