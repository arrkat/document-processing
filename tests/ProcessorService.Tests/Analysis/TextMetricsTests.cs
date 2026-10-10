using Contracts.Documents;
using ProcessorService.Analysis;

namespace ProcessorService.Tests.Analysis;

public class TextMetricsTests
{
    // ---------- Tokenize ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t \r\n")]
    public void Tokenize_empty_or_whitespace_has_no_words(string content)
    {
        Assert.Empty(TextMetrics.Tokenize(content));
    }

    [Fact]
    public void Tokenize_splits_on_any_whitespace()
    {
        Assert.Equal(["one", "two", "three", "four"], TextMetrics.Tokenize("one two\tthree\nfour"));
    }

    [Fact]
    public void Tokenize_strips_punctuation_without_splitting_words()
    {
        Assert.Equal(["hello", "world", "dont", "email"], TextMetrics.Tokenize("Hello, world! don't e-mail"));
    }

    [Fact]
    public void Tokenize_is_case_insensitive()
    {
        Assert.Equal(["apple", "apple", "apple"], TextMetrics.Tokenize("Apple APPLE apple"));
    }

    [Fact]
    public void Tokenize_drops_pure_numbers_but_keeps_words_with_digits()
    {
        Assert.Equal(["route", "mp3", "covid19"], TextMetrics.Tokenize("3.14 route 66 mp3 covid-19"));
    }

    [Fact]
    public void Tokenize_drops_pieces_without_letters()
    {
        Assert.Empty(TextMetrics.Tokenize("— ... :: _"));
    }

    [Fact]
    public void Tokenize_supports_hebrew_and_strips_vowel_marks()
    {
        Assert.Equal(["שלום", "עולם", "שלום"], TextMetrics.Tokenize("שלום עולם שָׁלוֹם"));
    }

    // ---------- CountFrequencies ----------

    [Fact]
    public void CountFrequencies_counts_each_distinct_word()
    {
        var frequencies = TextMetrics.CountFrequencies(["a", "b", "a"]);

        Assert.Equal(new Dictionary<string, int> { ["a"] = 2, ["b"] = 1 }, frequencies);
    }

    [Fact]
    public void CountFrequencies_of_no_words_is_empty()
    {
        Assert.Empty(TextMetrics.CountFrequencies([]));
    }

    // ---------- TopWords ----------

    [Fact]
    public void TopWords_orders_by_count_descending()
    {
        var top = TextMetrics.TopWords(new Dictionary<string, int> { ["a"] = 1, ["b"] = 3, ["c"] = 2 }, 2);

        Assert.Equal([new WordFrequency("b", 3), new WordFrequency("c", 2)], top);
    }

    [Fact]
    public void TopWords_breaks_ties_alphabetically()
    {
        var top = TextMetrics.TopWords(new Dictionary<string, int> { ["pear"] = 2, ["apple"] = 2, ["fig"] = 2 }, 2);

        Assert.Equal([new WordFrequency("apple", 2), new WordFrequency("fig", 2)], top);
    }

    [Fact]
    public void TopWords_returns_all_when_fewer_distinct_words_than_requested()
    {
        var top = TextMetrics.TopWords(new Dictionary<string, int> { ["a"] = 1, ["b"] = 1 }, 5);

        Assert.Equal(2, top.Count);
    }

    // ---------- Sha256Hex ----------

    // Expected values verified with: printf '%s' "<text>" | shasum -a 256
    [Theory]
    [InlineData("hello world", "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9")]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("a_b::c", "31d7b9a193ccadbdde0c2d9070c0f565c9683f660a17164f5a81ab8401a1bbbf")]
    public void Sha256Hex_matches_known_values(string content, string expected)
    {
        Assert.Equal(expected, TextMetrics.Sha256Hex(content));
    }

    // These pairs tokenize identically, but the hash must reflect the exact original text.
    [Theory]
    [InlineData("a_b::c", "abc")]
    [InlineData("Hello world", "hello world")]
    [InlineData("a\r\nb", "a\nb")]
    public void Sha256Hex_is_computed_on_the_original_text(string original, string tokenizedEquivalent)
    {
        Assert.NotEqual(TextMetrics.Sha256Hex(tokenizedEquivalent), TextMetrics.Sha256Hex(original));
    }
}
