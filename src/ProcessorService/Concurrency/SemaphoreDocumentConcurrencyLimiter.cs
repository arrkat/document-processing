using Microsoft.Extensions.Options;
using ProcessorService.Settings;

namespace ProcessorService.Concurrency;

/// <summary><see cref="SemaphoreSlim"/>-based limiter; <see cref="ProcessingOptions.MaxConcurrentDocuments"/> = 0 means <see cref="Environment.ProcessorCount"/>.</summary>
public sealed class SemaphoreDocumentConcurrencyLimiter(IOptions<ProcessingOptions> options) : IDocumentConcurrencyLimiter
{
    public int MaxConcurrency => throw new NotImplementedException();

    public ValueTask<IDisposable> AcquireAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
