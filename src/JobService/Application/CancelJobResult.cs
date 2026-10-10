namespace JobService.Application;

public enum CancelJobResult
{
    /// <summary>202: the job was still waiting in the Job Service and is now Cancelled.</summary>
    Cancelled,

    /// <summary>202: the job is processing; the Processor accepted the cancel command. The job stays Processing until
    /// ProcessingCancelled arrives (or finishes first, if processing completed before the command took effect).</summary>
    CancellationRequested,

    /// <summary>404.</summary>
    NotFound,

    /// <summary>409: the job is already Completed, Failed or Cancelled.</summary>
    AlreadyTerminal,

    /// <summary>503: the job is processing but the cancel command could not be delivered to the Processor; retry.</summary>
    ProcessorUnavailable
}
