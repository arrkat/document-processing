using Contracts.Documents;

namespace ProcessorService.Analysis;

public sealed class DocumentAnalyzer(TimeProvider timeProvider) : IDocumentAnalyzer
{
    public DocumentResult Analyze(DocumentInput document) => throw new NotImplementedException();
}
