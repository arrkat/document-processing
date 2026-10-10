using System.Threading.Channels;
using BlobStorage;
using Contracts;
using Contracts.Blobs;
using Contracts.Documents;
using Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProcessorService.Analysis;
using ProcessorService.Jobs;
using ProcessorService.Processing;
using ProcessorService.Settings;

namespace ProcessorService.Tests.TestSupport;

/// <summary>A <see cref="ProcessingWorker"/> wired to real in-memory collaborators (blob store, registry, event channel).</summary>
public sealed class WorkerHarness : IAsyncDisposable
{
    private readonly Channel<ProcessingEvent> _events = Channel.CreateUnbounded<ProcessingEvent>();

    public WorkerHarness(IDocumentAnalyzer analyzer, Action<ProcessingOptions>? configure = null)
    {
        // Progress only by document count in tests (deterministic); the time-based trigger is effectively off.
        var options = new ProcessingOptions { ProgressInterval = TimeSpan.FromHours(1) };
        configure?.Invoke(options);

        Worker = new ProcessingWorker(Blobs, analyzer, Registry, _events.Writer, TimeProvider.System,
            Options.Create(options), NullLogger<ProcessingWorker>.Instance);
    }

    public InMemoryBlobStore Blobs { get; } = new();
    public JobRegistry Registry { get; } = new();
    public ProcessingWorker Worker { get; }

    /// <summary>Writes the documents blob and returns the matching job (not registered).</summary>
    public async Task<ProcessingJob> CreateJobAsync(string jobId, int documentCount)
    {
        var documents = Enumerable.Range(0, documentCount)
            .Select(i => new DocumentInput($"doc-{i}", $"text number {i}"))
            .ToList();
        await Blobs.PutJsonAsync(BlobKey.JobDocuments(jobId),
            new JobDocumentsBlob(JobDocumentsBlob.CurrentSchemaVersion, jobId, documents), ContractJson.Options);
        return new ProcessingJob(jobId, BlobKey.JobDocuments(jobId), documentCount);
    }

    /// <summary>Creates and registers a job, ready for <see cref="ProcessingWorker.ProcessAsync"/>.</summary>
    public async Task<ProcessingJob> CreateRegisteredJobAsync(string jobId, int documentCount)
    {
        var job = await CreateJobAsync(jobId, documentCount);
        Assert.True(Registry.TryRegister(job));
        return job;
    }

    /// <summary>All events published so far.</summary>
    public List<ProcessingEvent> DrainEvents()
    {
        var drained = new List<ProcessingEvent>();
        while (_events.Reader.TryRead(out var processingEvent))
            drained.Add(processingEvent);
        return drained;
    }

    public static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail($"Timed out waiting until {because}.");
            await Task.Delay(10);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Worker.StopAsync(CancellationToken.None);
        Worker.Dispose();
    }
}
