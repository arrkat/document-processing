using Contracts.Documents;

namespace ProcessorService.Analysis;

/// <summary>
/// Pure text calculations used by <see cref="DocumentAnalyzer"/>. Each one is deterministic and has no dependencies,
/// so it is tested directly with known inputs and outputs (there is no reason to fake them).
/// </summary>
public static class TextMetrics
{
    /// <summary>
    /// Splits content into lower-cased (invariant) words: runs of Unicode letters/digits, allowing apostrophes
    /// inside a word ("don't" is one word; leading/trailing apostrophes are not part of the word).
    /// Empty or whitespace-only content yields an empty list.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string content) => throw new NotImplementedException();

    /// <summary>Occurrences per distinct word.</summary>
    public static IReadOnlyDictionary<string, int> CountFrequencies(IReadOnlyList<string> words) =>
        throw new NotImplementedException();

    /// <summary>
    /// The <paramref name="count"/> most frequent words, by count descending, then word ascending (ordinal) so ties are deterministic.
    /// Returns fewer items when there are fewer distinct words.
    /// </summary>
    public static IReadOnlyList<WordFrequency> TopWords(IReadOnlyDictionary<string, int> frequencies, int count) =>
        throw new NotImplementedException();

    /// <summary>SHA-256 of the UTF-8 bytes of the original (not normalized) content, as lowercase hex.</summary>
    public static string Sha256Hex(string content) => throw new NotImplementedException();
}
