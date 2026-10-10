using System.Collections.Concurrent;

namespace BlobStorage;

/// <summary>Process-local store for tests. Not shareable between services.</summary>
public sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public async Task PutAsync(string key, Stream content, CancellationToken cancellationToken = default)
    {
        BlobKey.EnsureValid(key);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _blobs[key] = buffer.ToArray();
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        BlobKey.EnsureValid(key);
        if (!_blobs.TryGetValue(key, out var bytes))
            throw new BlobNotFoundException(key);

        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        BlobKey.EnsureValid(key);
        return Task.FromResult(_blobs.ContainsKey(key));
    }

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        BlobKey.EnsureValid(key);
        return Task.FromResult(_blobs.TryRemove(key, out _));
    }
}
