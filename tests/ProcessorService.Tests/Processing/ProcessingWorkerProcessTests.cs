using BlobStorage;
using Contracts;
using Contracts.Blobs;
using Contracts.Events;
using Contracts.Processing;
using ProcessorService.Tests.TestSupport;

namespace ProcessorService.Tests.Processing;

/// <summary>Running one job, owned by <see cref="ProcessorService.Processing.ProcessingWorker.ProcessAsync"/>.</summary>
public class ProcessingWorkerProcessTests
{
    [Fact]
    public async Task Happy_path_writes_results_in_input_order_and_reports_started_then_finished()
    {
        await using var harness = new WorkerHarness(new StubAnalyzer());
        var job = await harness.CreateRegisteredJobAsync("job-1", 5);

        await harness.Worker.ProcessAsync(job, CancellationToken.None);

        var results = await harness.Blobs.GetJsonAsync<JobResultsBlob>(BlobKey.JobResults("job-1"), ContractJson.Options);
        Assert.Equal(["doc-0", "doc-1", "doc-2", "doc-3", "doc-4"], results.Results.Select(r => r.DocumentId));

        var events = harness.DrainEvents();
        Assert.IsType<ProcessingStartedEvent>(events.First());
        var finished = Assert.IsType<ProcessingFinishedEvent>(events.Last());
        Assert.Equal(BlobKey.JobResults("job-1"), finished.ResultsRef);
        Assert.Equal(events.Select(e => e.Sequence).Order(), events.Select(e => e.Sequence));
        Assert.Equal(ProcessingStatus.Finished, harness.Registry.GetStatus("job-1")!.Status);
    }

    [Fact]
    public async Task Progress_is_published_every_N_documents()
    {
        await using var harness = new WorkerHarness(new StubAnalyzer(), o =>
        {
            o.ProgressEveryDocuments = 3;
            o.PerJobParallelism = 1;
        });
        var job = await harness.CreateRegisteredJobAsync("job-1", 10);

        await harness.Worker.ProcessAsync(job, CancellationToken.None);

        var progress = harness.DrainEvents().OfType<ProcessingProgressEvent>().Select(p => p.ProcessedDocuments);
        Assert.Equal([3, 6, 9], progress);
    }

    [Fact]
    public async Task Global_document_limit_is_never_exceeded_across_jobs()
    {
        var analyzer = new ConcurrencyProbeAnalyzer(TimeSpan.FromMilliseconds(15));
        await using var harness = new WorkerHarness(analyzer, o =>
        {
            o.MaxConcurrentDocuments = 2;
            o.PerJobParallelism = 4;
        });
        var jobs = await Task.WhenAll(Enumerable.Range(0, 3).Select(i => harness.CreateRegisteredJobAsync($"job-{i}", 20)));

        await Task.WhenAll(jobs.Select(job => harness.Worker.ProcessAsync(job, CancellationToken.None)));

        Assert.Equal(2, analyzer.Peak);
    }

    [Fact]
    public async Task Per_job_parallelism_is_never_exceeded()
    {
        var analyzer = new ConcurrencyProbeAnalyzer(TimeSpan.FromMilliseconds(15));
        await using var harness = new WorkerHarness(analyzer, o =>
        {
            o.MaxConcurrentDocuments = 8;
            o.PerJobParallelism = 2;
        });
        var job = await harness.CreateRegisteredJobAsync("job-1", 20);

        await harness.Worker.ProcessAsync(job, CancellationToken.None);

        Assert.Equal(2, analyzer.Peak);
    }

    [Fact]
    public async Task Cancelled_job_reports_cancelled_and_writes_no_results()
    {
        using var analyzer = new GatedAnalyzer();
        await using var harness = new WorkerHarness(analyzer, o => o.PerJobParallelism = 1);
        var job = await harness.CreateRegisteredJobAsync("job-1", 5);

        var run = harness.Worker.ProcessAsync(job, CancellationToken.None);
        await analyzer.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        harness.Registry.RequestCancel("job-1");
        analyzer.Release();
        await run;

        Assert.IsType<ProcessingCancelledEvent>(harness.DrainEvents().Last());
        Assert.Equal(ProcessingStatus.Cancelled, harness.Registry.GetStatus("job-1")!.Status);
        Assert.False(await harness.Blobs.ExistsAsync(BlobKey.JobResults("job-1")));
    }

    [Theory]
    [InlineData(false, 3)] // documents blob missing
    [InlineData(true, 4)]  // blob has 3 documents but the request said 4
    public async Task Bad_input_fails_as_InputUnavailable(bool writeBlob, int declaredCount)
    {
        await using var harness = new WorkerHarness(new StubAnalyzer());
        if (writeBlob)
            await harness.CreateJobAsync("job-1", 3);
        var job = new ProcessorService.Jobs.ProcessingJob("job-1", BlobKey.JobDocuments("job-1"), declaredCount);
        harness.Registry.TryRegister(job);

        await harness.Worker.ProcessAsync(job, CancellationToken.None);

        var failed = Assert.IsType<ProcessingFailedEvent>(harness.DrainEvents().Last());
        Assert.Equal(ErrorCode.InputUnavailable, failed.ErrorCode);
        Assert.Equal(ProcessingStatus.Failed, harness.Registry.GetStatus("job-1")!.Status);
    }

    [Fact]
    public async Task Analyzer_exception_fails_as_Unexpected()
    {
        await using var harness = new WorkerHarness(new ThrowingAnalyzer());
        var job = await harness.CreateRegisteredJobAsync("job-1", 3);

        await harness.Worker.ProcessAsync(job, CancellationToken.None);

        var failed = Assert.IsType<ProcessingFailedEvent>(harness.DrainEvents().Last());
        Assert.Equal(ErrorCode.Unexpected, failed.ErrorCode);
    }

    [Fact]
    public async Task Host_shutdown_sends_no_terminal_event()
    {
        using var analyzer = new GatedAnalyzer();
        await using var harness = new WorkerHarness(analyzer, o => o.PerJobParallelism = 1);
        var job = await harness.CreateRegisteredJobAsync("job-1", 5);
        using var stopping = new CancellationTokenSource();

        var run = harness.Worker.ProcessAsync(job, stopping.Token);
        await analyzer.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        stopping.Cancel();
        analyzer.Release();
        await run;

        Assert.All(harness.DrainEvents(), e => Assert.False(e.IsTerminal));
        Assert.Equal(ProcessingStatus.Processing, harness.Registry.GetStatus("job-1")!.Status);
    }
}
