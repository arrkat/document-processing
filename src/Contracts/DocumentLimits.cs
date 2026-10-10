namespace Contracts;

/// <summary>Input limits enforced by both services (Job Service validates, Processor re-validates).</summary>
public static class DocumentLimits
{
    public const int MaxDocumentsPerJob = 1000;
    public const int MaxDocumentIdLength = 128;
    public const int MaxContentLength = 100_000;
    public const int TopWordsCount = 5;
}
