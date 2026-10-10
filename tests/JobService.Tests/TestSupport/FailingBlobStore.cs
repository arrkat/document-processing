using BlobStorage;

namespace JobService.Tests.TestSupport;

/// <summary>A blob store whose writes always fail, e.g. a full disk.</summary>
public sealed class FailingBlobStore : IBlobStore
{
    public Task PutAsync(string key, Stream content, CancellationToken cancellationToken = default) =>
        throw new IOException("disk full");

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) => throw new BlobNotFoundException(key);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
