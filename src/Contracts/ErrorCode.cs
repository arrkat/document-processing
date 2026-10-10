namespace Contracts;

/// <summary>
/// Machine-readable reason for a job-level failure. Shared by the REST API and WebSocket events.
/// A job is failed only when waiting cannot help; transient problems are retried first.
/// </summary>
public enum ErrorCode
{
    /// <summary>The Processor could not be reached during the whole (bounded) dispatch retry window.</summary>
    ProcessorUnavailable,

    /// <summary>The Processor rejected the request as invalid (400); resending would give the same answer.</summary>
    ProcessorRejected,

    /// <summary>A Processing job had no activity for too long and the Processor no longer knows it (crash / restart).</summary>
    ProcessorTimeout,

    /// <summary>The Processor could not read the documents blob.</summary>
    InputUnavailable,

    /// <summary>The Job Service could not read the results blob after all retries.</summary>
    ResultsUnavailable,

    /// <summary>Unexpected error in the processing pipeline (a bug).</summary>
    Unexpected
}
