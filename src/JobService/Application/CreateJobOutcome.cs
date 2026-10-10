using Contracts.Jobs;

namespace JobService.Application;

/// <summary>Exactly one of <see cref="Job"/> (created) / <see cref="Errors"/> (invalid input) is set.</summary>
public sealed record CreateJobOutcome(JobSummary? Job, IReadOnlyDictionary<string, string[]>? Errors)
{
    public static CreateJobOutcome Created(JobSummary job) => new(job, null);
    public static CreateJobOutcome Invalid(IReadOnlyDictionary<string, string[]> errors) => new(null, errors);
}
