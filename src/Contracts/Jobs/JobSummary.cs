namespace Contracts.Jobs;

public sealed record JobSummary(
    string Id,
    JobStatus Status,
    int TotalDocuments,
    int ProcessedDocuments,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    ErrorCode? ErrorCode,
    string? ErrorMessage);
