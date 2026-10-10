namespace ProcessorService.Jobs;

public enum CancelOutcome
{
    CancellationRequested,
    NotFound,

    /// <summary>The job already reached a terminal state.</summary>
    AlreadyFinished
}
