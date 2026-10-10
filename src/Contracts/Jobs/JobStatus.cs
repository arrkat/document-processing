namespace Contracts.Jobs;

/// <summary>
/// Job lifecycle as seen by clients of the Job Service.
/// Pending -> Processing -> Completed | Failed | Cancelled; Pending -> Failed | Cancelled.
/// The Job Service decides only what happens before the handover (waiting, cancel while waiting, unreachable, rejected);
/// after the Processor accepts the job, the status records what the Processor reports.
/// </summary>
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}
