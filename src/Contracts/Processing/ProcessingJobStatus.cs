namespace Contracts.Processing;

/// <summary>Response of GET /api/processing-jobs/{jobId}; used by the Job Service for reconciliation.</summary>
public sealed record ProcessingJobStatus(
    string JobId,
    ProcessingStatus Status,
    int ProcessedDocuments,
    int TotalDocuments,
    string? ResultsRef,
    ErrorCode? ErrorCode,
    string? ErrorMessage);
