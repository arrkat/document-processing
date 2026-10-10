namespace ProcessorService.Settings;

public sealed class ProcessingOptions
{
    public const string SectionName = "Processing";

    /// <summary>Jobs waiting in the intake queue before POST returns 503.</summary>
    public int QueueCapacity { get; set; } = 100;

    /// <summary>Jobs processed at the same time.</summary>
    public int WorkerCount { get; set; } = 10;

    /// <summary>Max documents of a single job in flight at once.</summary>
    public int PerJobParallelism { get; set; } = 4;

    /// <summary>Global cap on documents analysed at once across all jobs; 0 = <see cref="Environment.ProcessorCount"/>.</summary>
    public int MaxConcurrentDocuments { get; set; }

    /// <summary>Publish progress at most once per this interval…</summary>
    public TimeSpan ProgressInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>…or every this many documents, whichever comes first.</summary>
    public int ProgressEveryDocuments { get; set; } = 100;
}
