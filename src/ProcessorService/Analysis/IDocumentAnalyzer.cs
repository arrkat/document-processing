using Contracts.Documents;

namespace ProcessorService.Analysis;

/// <summary>
/// Pure, CPU-bound analysis of one document. Same input always gives the same result.
/// An interface so pipeline tests can substitute a slow or throwing analyzer.
/// </summary>
public interface IDocumentAnalyzer
{
    DocumentResult Analyze(DocumentInput document);
}
