namespace JobService.Application;

public enum CancelJobOutcome
{
    Accepted,
    NotFound,

    /// <summary>The job is already Completed, Failed or Cancelled.</summary>
    AlreadyTerminal
}
