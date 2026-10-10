namespace Contracts.Processing;

/// <summary>
/// Job state inside the Processor: Processing → Finished | Failed | Cancelled.
/// "Finished" means the Processor's work is done and the results blob is written. It is deliberately not "Completed":
/// the Job Service marks a job Completed only after it has verified the results.
/// There is no queued state: the Processor accepts a job only when a job slot is free and starts it immediately.
/// </summary>
public enum ProcessingStatus
{
    Processing,
    Finished,
    Failed,
    Cancelled
}
