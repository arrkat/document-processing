namespace JobService.Application;

public enum CancelJobResult
{
    Accepted,
    NotFound,

    /// <summary>The job is already Completed, Failed or Cancelled.</summary>
    AlreadyTerminal
}
