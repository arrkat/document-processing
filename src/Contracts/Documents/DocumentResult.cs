namespace Contracts.Documents;

/// <summary>Analysis output for one document. For <see cref="DocumentStatus.Failed"/> the metrics are zero and <see cref="Error"/> is set.</summary>
public sealed record DocumentResult(
    string DocumentId,
    DocumentStatus Status,
    int WordCount,
    int UniqueWordCount,
    IReadOnlyList<WordFrequency> TopWords,
    string Sha256,
    int CharacterCount,
    int LineCount,
    double AverageWordLength,
    double ProcessingTimeMs,
    string? Error = null);
