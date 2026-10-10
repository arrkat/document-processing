namespace Contracts.Documents;

/// <summary>
/// Analysis output for one document. Every validated document can be analysed, so there is no per-document failure:
/// an empty document simply has zero counts, no top words and the hash of the empty string.
/// </summary>
public sealed record DocumentResult(
    string DocumentId,
    int WordCount,
    int UniqueWordCount,
    IReadOnlyList<WordFrequency> TopWords,
    string Sha256);
