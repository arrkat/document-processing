using JobService.Settings;
using Microsoft.Extensions.Options;

namespace JobService.Dispatch;

public sealed class ChannelJobDispatchQueue(IOptions<DispatchOptions> options) : IJobDispatchQueue
{
    public bool TryEnqueue(string jobId) => throw new NotImplementedException();

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
