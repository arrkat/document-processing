using Microsoft.Extensions.Options;
using ProcessorService.Jobs;
using ProcessorService.Settings;

namespace ProcessorService.Queue;

public sealed class ChannelProcessingQueue(IOptions<ProcessingOptions> options) : IProcessingQueue
{
    public bool TryEnqueue(ProcessingJob job) => throw new NotImplementedException();

    public IAsyncEnumerable<ProcessingJob> ReadAllAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
