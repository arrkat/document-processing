namespace Contracts.Jobs;

public sealed record JobSummary(
    string Id,
    JobStatus Status,
    int TotalDocuments,
    int ProcessedDocuments,
    int SucceededDocuments,
    int FailedDocuments,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    ErrorCode? ErrorCode,
    string? ErrorMessage);
