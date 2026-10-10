namespace Contracts.Processing;

/// <summary>Job state inside the Processor. Queued -> Processing -> Completed | Failed | Cancelled; Queued -> Cancelled.</summary>
public enum ProcessingStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}
