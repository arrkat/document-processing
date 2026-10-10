namespace BlobStorage;

/// <summary>
/// Minimal key/value blob storage used for the claim-check pattern:
/// services exchange blob keys (references) over REST/WebSocket and the payloads live here.
/// Implementations must make <see cref="PutAsync"/> atomic: a reader sees either the old blob or the new one, never a partial write.
/// </summary>
public interface IBlobStore
{
    Task PutAsync(string key, Stream content, CancellationToken cancellationToken = default);

    /// <exception cref="BlobNotFoundException">No blob exists for <paramref name="key"/>.</exception>
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <returns><c>true</c> if a blob was deleted, <c>false</c> if it did not exist.</returns>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
