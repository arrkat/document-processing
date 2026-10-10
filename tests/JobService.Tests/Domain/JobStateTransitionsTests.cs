using Contracts;
using Contracts.Events;
using Contracts.Jobs;
using JobService.Domain;
using static JobService.Tests.TestSupport.Jobs;

namespace JobService.Tests.Domain;

public class JobStateTransitionsTests
{
    private static readonly DateTimeOffset ReceivedAt = T0.AddMinutes(1);

    private static ProcessingEvent Started(long seq) => new ProcessingStartedEvent(Guid.NewGuid(), "job", seq, T0, 3);
    private static ProcessingEvent Progress(long seq, int processed) => new ProcessingProgressUpdatedEvent(Guid.NewGuid(), "job", seq, T0, processed, 3);
    private static ProcessingEvent Finished(long seq) => new ProcessingFinishedEvent(Guid.NewGuid(), "job", seq, T0, "jobs/job/results.json");
    private static ProcessingEvent FailedEvent(long seq) => new ProcessingFailedEvent(Guid.NewGuid(), "job", seq, T0, ErrorCode.InputUnavailable, "missing");
    private static ProcessingEvent CancelledEvent(long seq, int processed) => new ProcessingCancelledEvent(Guid.NewGuid(), "job", seq, T0, processed);

    public static TheoryData<string, JobState, ProcessingEvent, JobStatus?, IgnoreReason?> Cases => new()
    {
        { "Pending + Started", PendingJob(), Started(1), JobStatus.Processing, null },
        { "Pending + Progress (implicit start)", PendingJob(), Progress(1, 2), JobStatus.Processing, null },
        { "Pending + Finished (implicit start)", PendingJob(), Finished(1), JobStatus.Completed, null },
        { "Pending + Failed (implicit start)", PendingJob(), FailedEvent(1), JobStatus.Failed, null },
        { "Processing + Started (after the 202)", ProcessingJob(), Started(1), JobStatus.Processing, null },
        { "Processing + Progress", ProcessingJob(1), Progress(2, 2), JobStatus.Processing, null },
        { "Processing + Finished", ProcessingJob(2), Finished(3), JobStatus.Completed, null },
        { "Processing + Failed", ProcessingJob(2), FailedEvent(3), JobStatus.Failed, null },
        { "Processing + Cancelled", ProcessingJob(2), CancelledEvent(3, 1), JobStatus.Cancelled, null },
        { "same sequence", ProcessingJob(5), Progress(5, 2), null, IgnoreReason.Duplicate },
        { "older sequence", ProcessingJob(5), Progress(4, 2), null, IgnoreReason.Duplicate },
        { "Completed + Progress", CompletedJob(5), Progress(6, 2), null, IgnoreReason.InvalidTransition },
        { "Failed + Finished", FailedJob(5), Finished(6), null, IgnoreReason.InvalidTransition },
        { "Cancelled + Started", CancelledJob(5), Started(6), null, IgnoreReason.InvalidTransition },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Maps_events_onto_legal_transitions(string scenario, JobState current, ProcessingEvent incoming,
        JobStatus? expectedStatus, IgnoreReason? expectedIgnore)
    {
        var result = JobStateTransitions.Apply(current, incoming, ReceivedAt);

        Assert.True(expectedStatus == result.NewState?.Status, scenario);
        Assert.Equal(expectedIgnore, result.Ignored);
        if (result.NewState is { } next)
        {
            Assert.Equal(incoming.Sequence, next.LastEventSequence);
            Assert.Equal(ReceivedAt, next.LastActivityAt);
        }
    }

    [Fact]
    public void Carries_the_event_details_into_the_new_state()
    {
        var progressed = Assert.IsType<ProcessingState>(JobStateTransitions.Apply(ProcessingJob(1), Progress(2, 7), ReceivedAt).NewState);
        Assert.Equal(7, progressed.ProcessedDocuments);

        var failed = Assert.IsType<FailedState>(JobStateTransitions.Apply(PendingJob(), FailedEvent(1), ReceivedAt).NewState);
        Assert.Equal(ErrorCode.InputUnavailable, failed.ErrorCode);
        Assert.NotNull(failed.StartedAt); // the Processor had the job, so it did start

        var cancelled = Assert.IsType<CancelledState>(JobStateTransitions.Apply(ProcessingJob(2), CancelledEvent(3, 2), ReceivedAt).NewState);
        Assert.Equal(2, cancelled.ProcessedDocuments);
    }
}
