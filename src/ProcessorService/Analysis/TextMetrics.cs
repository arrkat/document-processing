using System.Security.Cryptography;
using System.Text;
using Contracts.Documents;

namespace ProcessorService.Analysis;

/// <summary>
/// Pure text calculations used by <see cref="DocumentAnalyzer"/>. Each one is deterministic and has no dependencies,
/// so it is tested directly with known inputs and outputs (there is no reason to fake them).
/// </summary>
public static class TextMetrics
{
    /// <summary>
    /// Splits content into words:
    /// <list type="number">
    /// <item>split on whitespace only (spaces, tabs, newlines);</item>
    /// <item>remove every character that is not a letter or digit (punctuation never splits a word: "don't" → "dont");</item>
    /// <item>lower-case (invariant);</item>
    /// <item>keep the piece only if it contains at least one letter (pure numbers such as "3.14" are not words; "mp3" is).</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string content)
    {
        var words = new List<string>();
        var word = new StringBuilder();

        // A null separator array splits on every char.IsWhiteSpace character.
        foreach (var piece in content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            word.Clear();
            var hasLetter = false;

            foreach (var c in piece)
            {
                if (char.IsLetter(c))
                {
                    word.Append(char.ToLowerInvariant(c));
                    hasLetter = true;
                }
                else if (char.IsDigit(c))
                {
                    word.Append(c);
                }
            }

            if (hasLetter)
                words.Add(word.ToString());
        }

        return words;
    }

    /// <summary>Occurrences per distinct word.</summary>
    public static IReadOnlyDictionary<string, int> CountFrequencies(IReadOnlyList<string> words)
    {
        var frequencies = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var word in words)
            frequencies[word] = frequencies.GetValueOrDefault(word) + 1;

        return frequencies;
    }

    /// <summary>
    /// The <paramref name="count"/> most frequent words, by count descending, then word ascending (ordinal) so ties are deterministic.
    /// Returns fewer items when there are fewer distinct words.
    /// </summary>
    public static IReadOnlyList<WordFrequency> TopWords(IReadOnlyDictionary<string, int> frequencies, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return frequencies
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(count)
            .Select(pair => new WordFrequency(pair.Key, pair.Value))
            .ToList();
    }

    /// <summary>
    /// SHA-256 of the UTF-8 bytes of the original content exactly as submitted, as lowercase hex.
    /// Independent of <see cref="Tokenize"/>: punctuation, casing, whitespace and line endings all affect the hash.
    /// </summary>
    public static string Sha256Hex(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
