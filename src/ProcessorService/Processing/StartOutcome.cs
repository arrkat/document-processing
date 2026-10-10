namespace ProcessorService.Processing;

public enum StartOutcome
{
    Started,

    /// <summary>The jobId was already registered; nothing new was started.</summary>
    AlreadyKnown,

    /// <summary>All job slots are taken; nothing was registered.</summary>
    Busy
}
