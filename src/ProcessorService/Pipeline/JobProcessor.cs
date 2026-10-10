using BlobStorage;
using Microsoft.Extensions.Options;
using ProcessorService.Analysis;
using ProcessorService.Concurrency;
using ProcessorService.Events;
using ProcessorService.Jobs;
using ProcessorService.Settings;

namespace ProcessorService.Pipeline;

public sealed class JobProcessor(
    IBlobStore blobStore,
    IDocumentAnalyzer analyzer,
    IDocumentConcurrencyLimiter limiter,
    IJobRegistry registry,
    IJobEventFactory eventFactory,
    IJobEventPublisher publisher,
    IOptions<ProcessingOptions> options,
    ILogger<JobProcessor> logger) : IJobProcessor
{
    public Task ProcessAsync(ProcessingJob job, CancellationToken stoppingToken) =>
        throw new NotImplementedException();
}
