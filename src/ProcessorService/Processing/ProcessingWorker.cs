using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using BlobStorage;
using Contracts;
using Contracts.Blobs;
using Contracts.Documents;
using Contracts.Events;
using Microsoft.Extensions.Options;
using ProcessorService.Analysis;
using ProcessorService.Jobs;
using ProcessorService.Settings;

namespace ProcessorService.Processing;

/// <summary>
/// Runs accepted jobs. Two limits keep CPU work bounded and fair:
/// <list type="bullet">
/// <item>job slots: at most <see cref="ProcessingOptions.MaxConcurrentJobs"/> jobs at once (decided in <see cref="TryStart"/>);</item>
/// <item>a global <see cref="SemaphoreSlim"/> of <see cref="ProcessingOptions.MaxConcurrentDocuments"/> (default: core count) that every
/// document analysis must enter, shared by all jobs; within a job at most <see cref="ProcessingOptions.PerJobParallelism"/> documents are in flight,
/// so one large job cannot take every slot.</item>
/// </list>
/// The registry is always updated before the matching event is published, so a reconnect snapshot supersedes any discarded event.
/// Registered as a hosted service so in-flight jobs are cancelled on shutdown.
/// </summary>
public sealed class ProcessingWorker(
    IBlobStore blobStore,
    IDocumentAnalyzer analyzer,
    IJobRegistry registry,
    ChannelWriter<ProcessingEvent> events,
    TimeProvider timeProvider,
    IOptions<ProcessingOptions> options,
    ILogger<ProcessingWorker> logger) : IHostedService, IJobRunner
{
    private readonly ProcessingOptions _options = options.Value;
    // Deliberately not disposed: neither owns anything that needs it (no timer, no wait handle), and the host may call StopAsync
    // more than once or after disposal (e.g. app.Run and a test host both shutting down), so StopAsync must always be safe.
    private readonly SemaphoreSlim _documentSlots = new(ResolveMaxConcurrentDocuments(options.Value));
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<string, Task> _running = new(StringComparer.Ordinal);
    private readonly object _admission = new();
    private int _runningJobs;

    /// <summary>Jobs currently holding a job slot.</summary>
    public int RunningJobs => Volatile.Read(ref _runningJobs);

    public StartJobResult TryStart(ProcessingJob job)
    {
        // Lock order is always _admission → a job's registry gate, and the registry never takes _admission,
        // so calling the registry while holding this lock cannot deadlock.
        lock (_admission)
        {
            if (registry.GetStatus(job.JobId) is not null)
                return StartJobResult.AlreadyKnown;
            if (_runningJobs >= _options.MaxConcurrentJobs)
                return StartJobResult.Busy;
            if (!registry.TryRegister(job))
                return StartJobResult.AlreadyKnown;
            _runningJobs++;
        }

        // Track the task before it starts, so its own cleanup can never run before it is tracked.
        var run = new Task<Task>(() => RunAndReleaseSlotAsync(job));
        _running[job.JobId] = run.Unwrap();
        run.Start(TaskScheduler.Default);

        logger.LogInformation("Job {JobId} started with {DocumentCount} documents", job.JobId, job.DocumentCount);
        return StartJobResult.Started;
    }

    /// <summary>
    /// Runs one registered job end to end (public so tests can await it directly, without job slots):
    /// ProcessingStarted → read documents blob (missing, unreadable or wrong count → ProcessingFailed(InputUnavailable))
    /// → analyse documents under both limits → throttled ProcessingProgressUpdated → write results blob → mark finished → ProcessingFinished(resultsRef).
    /// User cancel → ProcessingCancelled. Unexpected exception → ProcessingFailed(Unexpected).
    /// Host shutdown sends no terminal event: the Job Service watchdog detects the abandoned job.
    /// </summary>
    public async Task ProcessAsync(ProcessingJob job, CancellationToken stoppingToken)
    {
        if (!registry.TryGetCancellationToken(job.JobId, out var cancelToken))
            throw new InvalidOperationException($"Job '{job.JobId}' is not registered.");

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["JobId"] = job.JobId });
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, stoppingToken);
        var processed = 0;

        try
        {
            await PublishAsync(new ProcessingStartedEvent(Guid.NewGuid(), job.JobId, registry.NextSequence(job.JobId),
                timeProvider.GetUtcNow(), job.DocumentCount));

            var documents = await ReadDocumentsAsync(job, linked.Token);
            if (documents is null)
                return;

            var results = new DocumentResult[documents.Count];

            // Progress: at most every ProgressEveryDocuments documents or ProgressInterval, whichever comes first.
            // The lock keeps sequence numbers and processed counts increasing together. Progress is droppable
            // (TryWrite skips it when the buffer is full), and the final count is implied by ProcessingFinished.
            var progressGate = new object();
            var lastPublished = 0;
            var lastPublishedAt = timeProvider.GetTimestamp();
            void PublishProgressIfDue()
            {
                lock (progressGate)
                {
                    var current = Volatile.Read(ref processed);
                    if (current <= lastPublished || current >= documents.Count)
                        return;
                    if (current - lastPublished < _options.ProgressEveryDocuments
                        && timeProvider.GetElapsedTime(lastPublishedAt) < _options.ProgressInterval)
                        return;

                    lastPublished = current;
                    lastPublishedAt = timeProvider.GetTimestamp();
                    events.TryWrite(new ProcessingProgressUpdatedEvent(Guid.NewGuid(), job.JobId, registry.NextSequence(job.JobId),
                        timeProvider.GetUtcNow(), current, documents.Count));
                }
            }

            await Parallel.ForEachAsync(
                Enumerable.Range(0, documents.Count),
                new ParallelOptions { MaxDegreeOfParallelism = _options.PerJobParallelism, CancellationToken = linked.Token },
                async (index, ct) =>
                {
                    await _documentSlots.WaitAsync(ct);
                    try
                    {
                        results[index] = analyzer.Analyze(documents[index]);
                    }
                    finally
                    {
                        _documentSlots.Release();
                    }

                    var done = Interlocked.Increment(ref processed);
                    registry.ReportProgress(job.JobId, done);
                    PublishProgressIfDue();
                });

            var resultsRef = BlobKey.JobResults(job.JobId);
            await blobStore.PutJsonAsync(resultsRef,
                new JobResultsBlob(JobResultsBlob.CurrentSchemaVersion, job.JobId, results), ContractJson.Options, linked.Token);

            if (registry.TryMarkFinished(job.JobId, resultsRef))
            {
                logger.LogInformation("Job {JobId} finished", job.JobId);
                await PublishAsync(new ProcessingFinishedEvent(Guid.NewGuid(), job.JobId, registry.NextSequence(job.JobId),
                    timeProvider.GetUtcNow(), resultsRef));
            }
        }
        catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
        {
            if (registry.TryMarkCancelled(job.JobId))
            {
                logger.LogInformation("Job {JobId} cancelled", job.JobId);
                await PublishAsync(new ProcessingCancelledEvent(Guid.NewGuid(), job.JobId, registry.NextSequence(job.JobId),
                    timeProvider.GetUtcNow(), Volatile.Read(ref processed)));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Job {JobId} abandoned because the Processor is shutting down", job.JobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed unexpectedly", job.JobId);
            await FailAsync(job.JobId, ErrorCode.Unexpected, ex.Message);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        try
        {
            await Task.WhenAll(_running.Values).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Shutdown timed out with {Count} jobs still stopping", _running.Count);
        }
    }

    private async Task RunAndReleaseSlotAsync(ProcessingJob job)
    {
        try
        {
            await ProcessAsync(job, _stopping.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} crashed outside the processing pipeline", job.JobId);
        }
        finally
        {
            lock (_admission)
                _runningJobs--;
            _running.TryRemove(job.JobId, out _);
        }
    }

    private async Task<IReadOnlyList<DocumentInput>?> ReadDocumentsAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        JobDocumentsBlob blob;
        try
        {
            blob = await blobStore.GetJsonAsync<JobDocumentsBlob>(job.DocumentsRef, ContractJson.Options, cancellationToken);
        }
        catch (Exception ex) when (ex is BlobNotFoundException or JsonException)
        {
            await FailAsync(job.JobId, ErrorCode.InputUnavailable, $"Documents '{job.DocumentsRef}' could not be read: {ex.Message}");
            return null;
        }

        if (blob.Documents.Count != job.DocumentCount)
        {
            await FailAsync(job.JobId, ErrorCode.InputUnavailable,
                $"Expected {job.DocumentCount} documents in '{job.DocumentsRef}' but found {blob.Documents.Count}.");
            return null;
        }

        return blob.Documents;
    }

    private async Task FailAsync(string jobId, ErrorCode errorCode, string message)
    {
        if (!registry.TryMarkFailed(jobId, errorCode, message))
            return;

        logger.LogWarning("Job {JobId} failed: {ErrorCode} {Message}", jobId, errorCode, message);
        await PublishAsync(new ProcessingFailedEvent(Guid.NewGuid(), jobId, registry.NextSequence(jobId),
            timeProvider.GetUtcNow(), errorCode, message));
    }

    // Started and terminal events must not be lost while connected, so they wait for buffer space.
    private ValueTask PublishAsync(ProcessingEvent processingEvent) => events.WriteAsync(processingEvent);

    private static int ResolveMaxConcurrentDocuments(ProcessingOptions options) =>
        options.MaxConcurrentDocuments > 0 ? options.MaxConcurrentDocuments : Environment.ProcessorCount;

}
