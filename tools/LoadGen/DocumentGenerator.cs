using System.Text;
using Contracts.Documents;

namespace LoadGen;

/// <summary>
/// Deterministic synthetic documents. A fixed vocabulary is built from the seed, and words are drawn from it with a Zipf-like
/// distribution (the word at rank r has weight 1/r), so a few words are very common, like in natural text, and top-5 lists are meaningful.
/// </summary>
public sealed class DocumentGenerator
{
    private const int VocabularySize = 5_000;
    private static readonly string[] Syllables =
        ["ka", "lo", "mi", "ra", "te", "su", "no", "be", "di", "fa", "go", "hu", "ji", "ve", "zo", "pa", "ri", "sha", "tor", "len"];

    private readonly string[] _vocabulary;
    private readonly double[] _cumulativeWeights;

    public DocumentGenerator(int seed)
    {
        var random = new Random(seed);
        var words = new HashSet<string>(StringComparer.Ordinal);
        while (words.Count < VocabularySize)
        {
            var word = new StringBuilder();
            for (var i = random.Next(1, 4); i > 0; i--) // 1–3 syllables: ~10k characters per document at 200..3000 words
                word.Append(Syllables[random.Next(Syllables.Length)]);
            words.Add(word.ToString());
        }
        _vocabulary = words.ToArray();

        _cumulativeWeights = new double[VocabularySize];
        var total = 0.0;
        for (var rank = 1; rank <= VocabularySize; rank++)
            _cumulativeWeights[rank - 1] = total += 1.0 / rank;
    }

    /// <summary>The documents of one job. The same (seed, jobIndex) always gives the same documents.</summary>
    public IReadOnlyList<DocumentInput> CreateJob(int seed, int jobIndex, int documentCount, int minWords, int maxWords)
    {
        var random = new Random(HashCode.Combine(seed, jobIndex));
        var documents = new DocumentInput[documentCount];
        for (var i = 0; i < documentCount; i++)
            documents[i] = new DocumentInput($"doc-{i + 1}", CreateContent(random, random.Next(minWords, maxWords + 1)));
        return documents;
    }

    private string CreateContent(Random random, int wordCount)
    {
        var text = new StringBuilder(wordCount * 7);
        for (var i = 1; i <= wordCount; i++)
        {
            text.Append(NextWord(random));
            // Sentences of ~12 words and paragraphs of ~80, so the tokenizer sees punctuation and newlines too.
            text.Append(i % 80 == 0 ? ".\n" : i % 12 == 0 ? ". " : " ");
        }
        return text.ToString();
    }

    private string NextWord(Random random)
    {
        var target = random.NextDouble() * _cumulativeWeights[^1];
        var index = Array.BinarySearch(_cumulativeWeights, target);
        return _vocabulary[index >= 0 ? index : ~index];
    }
}
