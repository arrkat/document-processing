namespace Contracts.Jobs;

/// <param name="LastUpdatedAt">Last time anything happened to the job (creation, handover, any Processor event). A Processing job with an old
/// value means the Job Service has not heard from the Processor for that long.</param>
public sealed record JobSummary(
    string Id,
    JobStatus Status,
    int TotalDocuments,
    int ProcessedDocuments,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset LastUpdatedAt,
    ErrorCode? ErrorCode,
    string? ErrorMessage);
