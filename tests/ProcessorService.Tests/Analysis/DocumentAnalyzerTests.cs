using Contracts.Documents;
using ProcessorService.Analysis;

namespace ProcessorService.Tests.Analysis;

public class DocumentAnalyzerTests
{
    private readonly DocumentAnalyzer _analyzer = new();

    [Fact]
    public void Analyzes_a_typical_document()
    {
        // the ×5, cat ×3, and ×2, then hat/mat/on/ran/sat ×1 (ties → alphabetical: hat, mat)
        var document = new DocumentInput("doc-1", "The cat and the hat. The cat sat on the mat, and the cat ran!");

        var result = _analyzer.Analyze(document);

        Assert.Equal("doc-1", result.DocumentId);
        Assert.Equal(15, result.WordCount);
        Assert.Equal(8, result.UniqueWordCount);
        Assert.Equal(
            [
                new WordFrequency("the", 5),
                new WordFrequency("cat", 3),
                new WordFrequency("and", 2),
                new WordFrequency("hat", 1),
                new WordFrequency("mat", 1)
            ],
            result.TopWords);
        Assert.Equal("9786ee8c9247c34a8bab95047dffd945319d66367ca3a9895d5433f4d00bc004", result.Sha256);
    }

    [Fact]
    public void Empty_document_has_zero_counts_and_the_empty_string_hash()
    {
        var result = _analyzer.Analyze(new DocumentInput("empty", ""));

        Assert.Equal("empty", result.DocumentId);
        Assert.Equal(0, result.WordCount);
        Assert.Equal(0, result.UniqueWordCount);
        Assert.Empty(result.TopWords);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", result.Sha256);
    }
}
