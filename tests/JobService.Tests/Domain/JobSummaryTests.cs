using Contracts;
using Contracts.Jobs;
using JobService.Domain;
using static JobService.Tests.TestSupport.Jobs;

namespace JobService.Tests.Domain;

public class JobSummaryTests
{
    public static TheoryData<JobState, JobStatus, int, bool, bool, ErrorCode?> Cases => new()
    {
        // state, status, processed, has StartedAt, has CompletedAt, error code
        { PendingJob(), JobStatus.Pending, 0, false, false, null },
        { ProcessingJob(processed: 2), JobStatus.Processing, 2, true, false, null },
        { CompletedJob(), JobStatus.Completed, 3, true, true, null },
        { FailedJob(), JobStatus.Failed, 1, true, false, ErrorCode.Unexpected },
        { CancelledJob(), JobStatus.Cancelled, 1, true, false, null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Flattens_each_state(JobState state, JobStatus status, int processed, bool hasStartedAt, bool hasCompletedAt, ErrorCode? errorCode)
    {
        var summary = new Job(Metadata("job-1", documents: 3), state with { LastActivityAt = T0.AddMinutes(5) }).ToSummary();

        Assert.Equal("job-1", summary.Id);
        Assert.Equal(status, summary.Status);
        Assert.Equal(3, summary.TotalDocuments);
        Assert.Equal(processed, summary.ProcessedDocuments);
        Assert.Equal(hasStartedAt, summary.StartedAt is not null);
        Assert.Equal(hasCompletedAt, summary.CompletedAt is not null);
        Assert.Equal(errorCode, summary.ErrorCode);
        Assert.Equal(T0.AddMinutes(5), summary.LastUpdatedAt);
    }
}
