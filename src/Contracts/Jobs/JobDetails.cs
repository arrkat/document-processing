using Contracts.Documents;

namespace Contracts.Jobs;

/// <summary>A job plus its results. <see cref="Results"/> is null until the job is Completed.</summary>
public sealed record JobDetails(JobSummary Job, IReadOnlyList<DocumentResult>? Results);
