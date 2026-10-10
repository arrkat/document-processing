using Contracts.Documents;

namespace ProcessorService.Analysis;

/// <summary>
/// Pure, CPU-bound analysis of one document. Same input always gives the same result.
/// An interface so pipeline tests can substitute a slow or throwing analyzer.
/// </summary>
public interface IDocumentAnalyzer
{
    /// <summary>Never throws: an exception during analysis becomes a <see cref="DocumentStatus.Failed"/> result with zero metrics.</summary>
    DocumentResult Analyze(DocumentInput document);
}
