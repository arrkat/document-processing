namespace ProcessorService.Jobs;

public enum CancelProcessingJobResult
{
    CancellationRequested,
    NotFound,

    /// <summary>The job already reached a terminal state.</summary>
    AlreadyFinished
}
