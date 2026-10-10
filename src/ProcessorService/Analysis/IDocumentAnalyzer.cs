using Contracts.Documents;

namespace ProcessorService.Analysis;

/// <summary>
/// Pure, CPU-bound analysis of one document. Rules:
/// <list type="bullet">
/// <item>Words: case-insensitive (lower-invariant) runs of Unicode letters/digits, allowing inner apostrophes.</item>
/// <item>Top words: <see cref="Contracts.DocumentLimits.TopWordsCount"/> by count desc, then word asc (ordinal) for deterministic ties.</item>
/// <item>SHA-256 over the UTF-8 bytes of the original content, lowercase hex.</item>
/// <item>Empty / whitespace-only content is valid: zero counts, hash still computed.</item>
/// </list>
/// Exceptions are converted to a <see cref="DocumentStatus.Failed"/> result rather than thrown.
/// </summary>
public interface IDocumentAnalyzer
{
    DocumentResult Analyze(DocumentInput document);
}
