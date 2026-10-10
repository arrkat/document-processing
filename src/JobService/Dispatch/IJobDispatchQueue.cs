namespace JobService.Dispatch;

/// <summary>In-process outbox of job ids waiting to be sent to the Processor. Decouples POST /api/jobs from Processor availability.</summary>
public interface IJobDispatchQueue
{
    bool TryEnqueue(string jobId);

    IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken);
}
