using Contracts.Processing;
using Microsoft.Extensions.Logging.Abstractions;
using ProcessorService.Application;
using ProcessorService.Jobs;
using ProcessorService.Processing;
using ProcessorService.Validation;

namespace ProcessorService.Tests.Application;

public class ProcessingJobManagerTests
{
    private readonly RecordingRunner _runner = new();
    private readonly ProcessingJobManager _manager;

    public ProcessingJobManagerTests() =>
        _manager = new ProcessingJobManager(new ProcessJobRequestValidator(), new JobRegistry(), _runner, NullLogger<ProcessingJobManager>.Instance);

    [Fact]
    public void Invalid_request_is_never_started()
    {
        var result = _manager.Submit(new ProcessJobRequest("job-1", "jobs/other/documents.json", 3));

        Assert.Equal(SubmitProcessingJobStatus.Invalid, result.Status);
        Assert.NotNull(result.Errors);
        Assert.Empty(_runner.Started);
    }

    [Fact]
    public void Valid_request_is_handed_to_the_runner()
    {
        var result = _manager.Submit(new ProcessJobRequest("job-1", "jobs/job-1/documents.json", 3));

        Assert.Equal(SubmitProcessingJobStatus.Accepted, result.Status);
        Assert.Equal([new ProcessingJob("job-1", "jobs/job-1/documents.json", 3)], _runner.Started);
    }

    private sealed class RecordingRunner : IJobRunner
    {
        public List<ProcessingJob> Started { get; } = [];

        public StartJobResult TryStart(ProcessingJob job)
        {
            Started.Add(job);
            return StartJobResult.Started;
        }
    }
}
