namespace BlobStorage;

public sealed class BlobNotFoundException(string key)
    : Exception($"Blob '{key}' was not found.")
{
    public string Key { get; } = key;
}
