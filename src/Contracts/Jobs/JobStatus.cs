namespace Contracts.Jobs;

/// <summary>
/// Job lifecycle as seen by clients of the Job Service.
/// Pending -> Processing -> Completed | Failed | Cancelled; Pending -> Failed | Cancelled.
/// Completed is set only after the results have been ingested.
/// </summary>
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}
