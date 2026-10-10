using System.Text.RegularExpressions;

namespace BlobStorage;

/// <summary>
/// Builds and validates blob keys. Keys are relative, '/'-separated paths made of a safe character set,
/// so they can never escape the store root (no "..", no absolute paths, no backslashes).
/// </summary>
public static partial class BlobKey
{
    public static string JobDocuments(string jobId) => $"jobs/{RequireSegment(jobId)}/documents.json";

    public static string JobResults(string jobId) => $"jobs/{RequireSegment(jobId)}/results.json";

    public static bool IsValid(string? key) =>
        !string.IsNullOrEmpty(key)
        && key.Length <= 512
        && KeyPattern().IsMatch(key)
        && key.Split('/').All(segment => segment is not ("." or ".."));

    public static void EnsureValid(string? key)
    {
        if (!IsValid(key))
            throw new ArgumentException($"Invalid blob key '{key}'.", nameof(key));
    }

    private static string RequireSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) || !SegmentPattern().IsMatch(segment))
            throw new ArgumentException($"Invalid key segment '{segment}'.", nameof(segment));
        return segment;
    }

    // One or more segments of [A-Za-z0-9._-], separated by single '/'.
    [GeneratedRegex(@"^[A-Za-z0-9._-]+(/[A-Za-z0-9._-]+)*$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_-]+$")]
    private static partial Regex SegmentPattern();
}
