using BlobStorage;
using Contracts;
using Contracts.Blobs;
using Contracts.Jobs;
using Contracts.Processing;
using JobService.Domain;
using JobService.Monitoring;
using JobService.Persistence;
using JobService.Settings;
using JobService.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static JobService.Tests.TestSupport.Jobs;

namespace JobService.Tests.Monitoring;

public class JobWatchdogTests
{
    private readonly InMemoryJobRepository _repository = new();
    private readonly InMemoryBlobStore _blobs = new();
    private readonly FakeProcessorClient _processor = new();
    private readonly ListLogger<JobWatchdog> _logger = new();
    private int _statusQueries;

    private JobWatchdog CreateWatchdog(TimeSpan staleAfter) =>
        new(_repository, _blobs, _processor, TimeProvider.System, Options.Create(new WatchdogOptions { StaleAfter = staleAfter }), _logger);

    // Every Processing job built by Jobs.Processing() was last active at T0, long ago, so StaleAfter = 1 minute makes it stale.
    private static readonly TimeSpan Stale = TimeSpan.FromMinutes(1);

    private async Task<JobState> ScanAndGetStateAsync(TimeSpan staleAfter)
    {
        await CreateWatchdog(staleAfter).ScanAsync(CancellationToken.None);
        return (await _repository.GetAsync("a"))!.State;
    }

    private void ProcessorReports(ProcessingStatus status, ErrorCode? errorCode = null) =>
        _processor.Status = id =>
        {
            Interlocked.Increment(ref _statusQueries);
            return new ProcessingJobStatus(id, status, 1, 3, null, errorCode, errorCode is null ? null : "reason");
        };

    [Fact]
    public async Task Results_blob_completes_the_job_without_asking_the_Processor()
    {
        await AddAsync(_repository, "a", ProcessingJob());
        await _blobs.PutJsonAsync(BlobKey.JobResults("a"), new JobResultsBlob(1, "a", []), ContractJson.Options);
        ProcessorReports(ProcessingStatus.Processing);

        Assert.IsType<CompletedState>(await ScanAndGetStateAsync(Stale));
        Assert.Equal(0, _statusQueries);
    }

    [Theory]
    [InlineData(ProcessingStatus.Finished, JobStatus.Completed)]
    [InlineData(ProcessingStatus.Failed, JobStatus.Failed)]
    [InlineData(ProcessingStatus.Cancelled, JobStatus.Cancelled)]
    [InlineData(ProcessingStatus.Processing, JobStatus.Processing)]
    public async Task Processor_status_is_applied(ProcessingStatus reported, JobStatus expected)
    {
        await AddAsync(_repository, "a", ProcessingJob());
        ProcessorReports(reported, reported == ProcessingStatus.Failed ? ErrorCode.InputUnavailable : null);

        var state = await ScanAndGetStateAsync(Stale);

        Assert.Equal(expected, state.Status);
        if (state is FailedState failed)
            Assert.Equal(ErrorCode.InputUnavailable, failed.ErrorCode);
    }

    [Fact]
    public async Task Processor_that_lost_the_job_fails_it_as_ProcessorTimeout()
    {
        await AddAsync(_repository, "a", ProcessingJob());
        _processor.Status = _ => null;

        Assert.Equal(ErrorCode.ProcessorTimeout, Assert.IsType<FailedState>(await ScanAndGetStateAsync(Stale)).ErrorCode);
    }

    [Fact]
    public async Task Unreachable_Processor_leaves_the_job_Processing_and_logs_a_warning()
    {
        await AddAsync(_repository, "a", ProcessingJob());
        _processor.Status = _ => throw new HttpRequestException("connection refused");

        Assert.IsType<ProcessingState>(await ScanAndGetStateAsync(Stale));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("unreachable"));
    }

    [Fact]
    public async Task Job_with_recent_activity_is_left_alone()
    {
        await AddAsync(_repository, "a", ProcessingJob() with { LastActivityAt = DateTimeOffset.UtcNow });
        ProcessorReports(ProcessingStatus.Finished);

        Assert.IsType<ProcessingState>(await ScanAndGetStateAsync(Stale));
        Assert.Equal(0, _statusQueries);
    }
}
