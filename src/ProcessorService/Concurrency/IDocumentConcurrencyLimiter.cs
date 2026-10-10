namespace ProcessorService.Concurrency;

/// <summary>
/// Global cap on documents analysed at the same time, shared by all jobs (CPU-bound work → ~core count).
/// Per-job parallelism is limited separately; this limiter keeps the total bounded and lets jobs interleave fairly.
/// </summary>
public interface IDocumentConcurrencyLimiter
{
    int MaxConcurrency { get; }

    /// <summary>Waits asynchronously for a slot. Dispose the returned lease to release it.</summary>
    ValueTask<IDisposable> AcquireAsync(CancellationToken cancellationToken);
}
