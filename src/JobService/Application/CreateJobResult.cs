using Contracts.Jobs;

namespace JobService.Application;

/// <summary>Exactly one of <see cref="Job"/> (created) / <see cref="Errors"/> (invalid input) is set.</summary>
public sealed record CreateJobResult(JobSummary? Job, IReadOnlyDictionary<string, string[]>? Errors)
{
    public static CreateJobResult Created(JobSummary job) => new(job, null);
    public static CreateJobResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new(null, errors);
}
