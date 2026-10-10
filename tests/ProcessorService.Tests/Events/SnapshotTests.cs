using Contracts;
using Contracts.Events;
using Contracts.Processing;
using ProcessorService.Events;

namespace ProcessorService.Tests.Events;

public class SnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Each_job_is_restated_by_one_event_matching_its_status()
    {
        var jobs = new[]
        {
            new ProcessingJobStatus("running", ProcessingStatus.Processing, 4, 10, null, null, null),
            new ProcessingJobStatus("done", ProcessingStatus.Finished, 10, 10, "jobs/done/results.json", null, null),
            new ProcessingJobStatus("broken", ProcessingStatus.Failed, 2, 10, null, ErrorCode.InputUnavailable, "missing"),
            new ProcessingJobStatus("stopped", ProcessingStatus.Cancelled, 3, 10, null, null, null),
        };

        var snapshot = ProcessingEventsSocketClient.BuildSnapshot(jobs, _ => 7, Now);

        var progress = Assert.IsType<ProcessingProgressUpdatedEvent>(snapshot[0]);
        Assert.Equal((4, 10), (progress.ProcessedDocuments, progress.TotalDocuments));
        Assert.Equal("jobs/done/results.json", Assert.IsType<ProcessingFinishedEvent>(snapshot[1]).ResultsRef);
        Assert.Equal(ErrorCode.InputUnavailable, Assert.IsType<ProcessingFailedEvent>(snapshot[2]).ErrorCode);
        Assert.Equal(3, Assert.IsType<ProcessingCancelledEvent>(snapshot[3]).ProcessedDocuments);
    }

    [Fact]
    public void Every_snapshot_event_takes_the_jobs_next_sequence()
    {
        var sequences = new Dictionary<string, long> { ["a"] = 4, ["b"] = 9 };
        var jobs = new[]
        {
            new ProcessingJobStatus("a", ProcessingStatus.Processing, 1, 2, null, null, null),
            new ProcessingJobStatus("b", ProcessingStatus.Processing, 1, 2, null, null, null),
        };

        var snapshot = ProcessingEventsSocketClient.BuildSnapshot(jobs, id => ++sequences[id], Now);

        Assert.Equal([5L, 10L], snapshot.Select(e => e.Sequence));
    }
}
