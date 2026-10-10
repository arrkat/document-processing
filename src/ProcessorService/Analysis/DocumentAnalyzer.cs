using Contracts;
using Contracts.Documents;

namespace ProcessorService.Analysis;

/// <summary>
/// Orchestrates <see cref="TextMetrics"/>: tokenize once, count frequencies once, derive the rest from them,
/// and assemble the <see cref="DocumentResult"/> (top words limited to <see cref="DocumentLimits.TopWordsCount"/>).
/// </summary>
public sealed class DocumentAnalyzer : IDocumentAnalyzer
{
    public DocumentResult Analyze(DocumentInput document)
    {
        var words = TextMetrics.Tokenize(document.Content);
        var frequencies = TextMetrics.CountFrequencies(words);

        return new DocumentResult(
            DocumentId: document.Id,
            WordCount: words.Count,
            UniqueWordCount: frequencies.Count,
            TopWords: TextMetrics.TopWords(frequencies, DocumentLimits.TopWordsCount),
            Sha256: TextMetrics.Sha256Hex(document.Content));
    }
}
