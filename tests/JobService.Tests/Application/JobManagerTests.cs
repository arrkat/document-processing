using System.Threading.Channels;
using BlobStorage;
using Contracts;
using Contracts.Blobs;
using Contracts.Documents;
using Contracts.Jobs;
using JobService.Application;
using JobService.Dispatch;
using JobService.Domain;
using JobService.Persistence;
using JobService.Processing;
using JobService.Tests.TestSupport;
using JobService.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using static JobService.Tests.TestSupport.Jobs;

namespace JobService.Tests.Application;

public class JobManagerTests
{
    private readonly InMemoryJobRepository _repository = new();
    private readonly InMemoryBlobStore _blobs = new();
    private readonly Channel<DispatchRequest> _dispatch = Channel.CreateUnbounded<DispatchRequest>();
    private readonly FakeProcessorClient _processor = new();

    private JobManager CreateManager(IBlobStore? blobs = null) =>
        new(new CreateJobRequestValidator(), _repository, blobs ?? _blobs, _dispatch.Writer, _processor, TimeProvider.System,
            NullLogger<JobManager>.Instance);

    private static CreateJobRequest ValidRequest => new([new DocumentInput("a", "hello world"), new DocumentInput("b", "foo")]);

    // ---------- Create ----------

    [Fact]
    public async Task Invalid_request_stores_nothing()
    {
        var result = await CreateManager().CreateAsync(new CreateJobRequest([]));

        Assert.NotNull(result.Errors);
        Assert.Equal(0, (await _repository.ListAsync(null, 1, 50)).TotalCount);
        Assert.False(_dispatch.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Valid_request_writes_documents_stores_Pending_and_queues_the_job()
    {
        var result = await CreateManager().CreateAsync(ValidRequest);

        var summary = Assert.IsType<JobSummary>(result.Job);
        Assert.Equal(JobStatus.Pending, summary.Status);
        Assert.Equal(2, summary.TotalDocuments);

        var blob = await _blobs.GetJsonAsync<JobDocumentsBlob>(BlobKey.JobDocuments(summary.Id), ContractJson.Options);
        Assert.Equal(["a", "b"], blob.Documents.Select(d => d.Id));
        Assert.IsType<PendingState>((await _repository.GetAsync(summary.Id))!.State);
        Assert.True(_dispatch.Reader.TryRead(out var queued));
        Assert.Equal(summary.Id, queued.JobId);
    }

    [Fact]
    public async Task Failed_blob_write_stores_no_job()
    {
        await Assert.ThrowsAsync<IOException>(() => CreateManager(new FailingBlobStore()).CreateAsync(ValidRequest));

        Assert.Equal(0, (await _repository.ListAsync(null, 1, 50)).TotalCount);
        Assert.False(_dispatch.Reader.TryRead(out _));
    }

    // ---------- Get ----------

    [Fact]
    public async Task Results_come_from_the_blob_only_for_Completed_jobs()
    {
        await AddAsync(_repository, "done", CompletedJob());
        await AddAsync(_repository, "running", ProcessingJob());
        await _blobs.PutJsonAsync(BlobKey.JobResults("done"), new JobResultsBlob(1, "done",
            [new DocumentResult("a", 2, 2, [new WordFrequency("hello", 1)], "hash")]), ContractJson.Options);

        var manager = CreateManager();
        var done = await manager.GetAsync("done");
        var running = await manager.GetAsync("running");

        Assert.Equal(["a"], done!.Results!.Select(r => r.DocumentId));
        Assert.Null(running!.Results);
        Assert.Null(await manager.GetResultsAsync("running"));
        Assert.Null(await manager.GetAsync("missing"));
    }

    // ---------- Cancel ----------

    [Fact]
    public async Task Cancel_unknown_or_finished_job()
    {
        await AddAsync(_repository, "done", CompletedJob());
        var manager = CreateManager();

        Assert.Equal(CancelJobResult.NotFound, await manager.CancelAsync("missing"));
        Assert.Equal(CancelJobResult.AlreadyTerminal, await manager.CancelAsync("done"));
        Assert.Empty(_processor.CancelCommands);
    }

    [Fact]
    public async Task Cancel_Pending_job_is_local()
    {
        await AddAsync(_repository, "waiting", PendingJob());

        Assert.Equal(CancelJobResult.Cancelled, await CreateManager().CancelAsync("waiting"));

        Assert.IsType<CancelledState>((await _repository.GetAsync("waiting"))!.State);
        Assert.Empty(_processor.CancelCommands);
    }

    [Theory]
    [InlineData(ProcessorCancelResult.CancellationRequested, CancelJobResult.CancellationRequested, JobStatus.Processing)]
    [InlineData(ProcessorCancelResult.AlreadyFinished, CancelJobResult.AlreadyTerminal, JobStatus.Processing)]
    [InlineData(ProcessorCancelResult.NotFound, CancelJobResult.AlreadyTerminal, JobStatus.Failed)]
    [InlineData(ProcessorCancelResult.Unavailable, CancelJobResult.ProcessorUnavailable, JobStatus.Processing)]
    public async Task Cancel_Processing_job_is_a_command_and_the_state_follows_the_Processor(
        ProcessorCancelResult processorAnswer, CancelJobResult expected, JobStatus statusAfter)
    {
        await AddAsync(_repository, "running", ProcessingJob());
        _processor.CancelAnswer = processorAnswer;

        Assert.Equal(expected, await CreateManager().CancelAsync("running"));

        Assert.Equal(["running"], _processor.CancelCommands);
        var state = (await _repository.GetAsync("running"))!.State;
        Assert.Equal(statusAfter, state.Status);
        if (state is FailedState failed)
            Assert.Equal(ErrorCode.ProcessorTimeout, failed.ErrorCode);
    }
}
