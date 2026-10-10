namespace Contracts;

/// <summary>Machine-readable reason for a job-level failure. Shared by the REST API and WebSocket events.</summary>
public enum ErrorCode
{
    /// <summary>Job Service could not hand the job to the Processor after all retries.</summary>
    ProcessorUnavailable,

    /// <summary>Processor permanently rejected the job (e.g. 400).</summary>
    ProcessorRejected,

    /// <summary>No events for too long and the Processor does not know the job (crash / restart).</summary>
    ProcessorTimeout,

    /// <summary>Processor is shutting down and abandoned the job.</summary>
    ProcessorShuttingDown,

    /// <summary>Processor could not read the documents blob.</summary>
    InputUnavailable,

    /// <summary>Job Service could not read the results blob after all retries.</summary>
    ResultsUnavailable,

    /// <summary>Unexpected error in the processing pipeline.</summary>
    Unexpected
}
