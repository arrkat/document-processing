namespace Contracts.Processing;

/// <summary>
/// Job state inside the Processor: Processing → Finished | Failed | Cancelled.
/// "Finished" means the Processor's work is done and the results blob is fully written (atomically, before the event is sent).
/// "Completed" is the Job Service's word: it marks the job Completed when it receives that report.
/// There is no queued state: the Processor accepts a job only when a job slot is free and starts it immediately.
/// </summary>
public enum ProcessingStatus
{
    Processing,
    Finished,
    Failed,
    Cancelled
}
