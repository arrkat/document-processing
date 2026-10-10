using Contracts;
using Contracts.Processing;
using ProcessorService.Jobs;

namespace ProcessorService.Tests.Jobs;

public class JobRegistryTests
{
    private readonly JobRegistry _registry = new();
    private readonly ProcessingJob _job = new("job-1", "jobs/job-1/documents.json", 3);

    [Fact]
    public void Registration_is_atomic_under_concurrency()
    {
        var wins = 0;
        Parallel.For(0, 50, _ =>
        {
            if (_registry.TryRegister(_job))
                Interlocked.Increment(ref wins);
        });

        Assert.Equal(1, wins);
        Assert.Equal(ProcessingStatus.Processing, _registry.GetStatus("job-1")!.Status);
    }

    [Fact]
    public void First_terminal_state_wins()
    {
        _registry.TryRegister(_job);

        Assert.True(_registry.TryMarkFinished("job-1", "jobs/job-1/results.json"));
        Assert.False(_registry.TryMarkFailed("job-1", ErrorCode.Unexpected, "late"));
        Assert.False(_registry.TryMarkCancelled("job-1"));

        var status = _registry.GetStatus("job-1")!;
        Assert.Equal(ProcessingStatus.Finished, status.Status);
        Assert.Equal("jobs/job-1/results.json", status.ResultsRef);
        Assert.Equal(3, status.ProcessedDocuments);
        Assert.Null(status.ErrorCode);
    }

    [Fact]
    public void Cancel_fires_the_token_of_a_running_job_only()
    {
        _registry.TryRegister(_job);
        _registry.TryGetCancellationToken("job-1", out var token);

        Assert.Equal(CancelProcessingJobResult.NotFound, _registry.RequestCancel("unknown"));
        Assert.Equal(CancelProcessingJobResult.CancellationRequested, _registry.RequestCancel("job-1"));
        Assert.True(token.IsCancellationRequested);

        _registry.TryMarkCancelled("job-1");
        Assert.Equal(CancelProcessingJobResult.AlreadyFinished, _registry.RequestCancel("job-1"));
    }

    [Fact]
    public void Sequence_numbers_increase_per_job()
    {
        _registry.TryRegister(_job);
        _registry.TryRegister(_job with { JobId = "job-2", DocumentsRef = "jobs/job-2/documents.json" });

        Assert.Equal(1, _registry.NextSequence("job-1"));
        Assert.Equal(2, _registry.NextSequence("job-1"));
        Assert.Equal(1, _registry.NextSequence("job-2"));
        Assert.Equal(3, _registry.NextSequence("job-1"));
    }
}
