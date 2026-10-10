using ProcessorService.Jobs;

namespace ProcessorService.Queue;

/// <summary>Bounded intake queue. <see cref="TryEnqueue"/> never blocks: a full queue is reported to the caller (→ 503).</summary>
public interface IProcessingQueue
{
    bool TryEnqueue(ProcessingJob job);

    IAsyncEnumerable<ProcessingJob> ReadAllAsync(CancellationToken cancellationToken);
}
