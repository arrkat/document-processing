using System.Threading.Channels;
using BlobStorage;
using Contracts.Events;
using Microsoft.Extensions.Options;
using ProcessorService.Analysis;
using ProcessorService.Jobs;
using ProcessorService.Settings;

namespace ProcessorService.Processing;

/// <summary>
/// Runs accepted jobs. Two limits keep CPU work bounded and fair:
/// <list type="bullet">
/// <item>job slots: at most <see cref="ProcessingOptions.MaxConcurrentJobs"/> jobs at once (checked without blocking in <see cref="TryStart"/>);</item>
/// <item>a global <see cref="SemaphoreSlim"/> of <see cref="ProcessingOptions.MaxConcurrentDocuments"/> (default: core count) that every
/// document analysis must enter, shared by all jobs; within a job at most <see cref="ProcessingOptions.PerJobParallelism"/> documents are in flight,
/// so one large job cannot take every slot.</item>
/// </list>
/// Events are written to the outbound <see cref="ChannelWriter{T}"/> and sent by the WebSocket client.
/// Registered as a hosted service so in-flight jobs are cancelled on shutdown.
/// </summary>
public sealed class ProcessingWorker(
    IBlobStore blobStore,
    IDocumentAnalyzer analyzer,
    IJobRegistry registry,
    ChannelWriter<ProcessingEvent> events,
    TimeProvider timeProvider,
    IOptions<ProcessingOptions> options,
    ILogger<ProcessingWorker> logger) : BackgroundService, IJobRunner
{
    public StartOutcome TryStart(ProcessingJob job) => throw new NotImplementedException();

    /// <summary>
    /// Runs one registered job end to end (public so tests can await it directly, without job slots):
    /// ProcessingStarted → read documents blob (missing → ProcessingFailed(InputUnavailable)) → analyse documents under both limits
    /// → throttled ProcessingProgress → write results blob → mark finished → ProcessingFinished(resultsRef).
    /// User cancel → ProcessingCancelled. Unexpected exception → ProcessingFailed(Unexpected).
    /// Host shutdown sends no terminal event: the Job Service watchdog detects the abandoned job.
    /// </summary>
    public Task ProcessAsync(ProcessingJob job, CancellationToken stoppingToken) => throw new NotImplementedException();

    // TODO: on stoppingToken, cancel and await the jobs started by TryStart.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
