namespace JobService.Results;

public sealed class ChannelResultsIngestionQueue : IResultsIngestionQueue
{
    public bool TryEnqueue(string jobId) => throw new NotImplementedException();

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
