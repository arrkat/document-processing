using System.Text;
using BlobStorage;

namespace BlobStorage.Tests;

/// <summary>Behavior every <see cref="IBlobStore"/> implementation must satisfy.</summary>
public abstract class BlobStoreContractTests
{
    protected abstract IBlobStore Store { get; }

    [Fact]
    public async Task Put_then_read_returns_same_bytes()
    {
        await Store.PutAsync("jobs/a/documents.json", Utf8("hello"));

        Assert.Equal("hello", await ReadString("jobs/a/documents.json"));
    }

    [Fact]
    public async Task Put_overwrites_existing_blob()
    {
        await Store.PutAsync("jobs/a/results.json", Utf8("first"));
        await Store.PutAsync("jobs/a/results.json", Utf8("second"));

        Assert.Equal("second", await ReadString("jobs/a/results.json"));
    }

    [Fact]
    public async Task Read_missing_blob_throws_BlobNotFoundException()
    {
        var ex = await Assert.ThrowsAsync<BlobNotFoundException>(() => Store.OpenReadAsync("jobs/missing/results.json"));

        Assert.Equal("jobs/missing/results.json", ex.Key);
    }

    [Fact]
    public async Task Exists_and_delete_reflect_state()
    {
        Assert.False(await Store.ExistsAsync("jobs/b/results.json"));

        await Store.PutAsync("jobs/b/results.json", Utf8("x"));
        Assert.True(await Store.ExistsAsync("jobs/b/results.json"));

        Assert.True(await Store.DeleteAsync("jobs/b/results.json"));
        Assert.False(await Store.ExistsAsync("jobs/b/results.json"));
        Assert.False(await Store.DeleteAsync("jobs/b/results.json"));
    }

    [Fact]
    public async Task Json_round_trip()
    {
        var value = new Sample("doc-1", 42);

        await Store.PutJsonAsync("jobs/c/documents.json", value);

        Assert.Equal(value, await Store.GetJsonAsync<Sample>("jobs/c/documents.json"));
    }

    // documentsRef arrives over HTTP, so these guard against path traversal.
    [Theory]
    [InlineData("jobs/../../escape.json")]
    [InlineData("/etc/passwd")]
    [InlineData("jobs\\a\\x.json")]
    public async Task Rejects_unsafe_keys(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Store.PutAsync(key, Utf8("x")));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.OpenReadAsync(key));
    }

    private async Task<string> ReadString(string key)
    {
        await using var stream = await Store.OpenReadAsync(key);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    private sealed record Sample(string Id, int Count);
}

public sealed class InMemoryBlobStoreTests : BlobStoreContractTests
{
    protected override IBlobStore Store { get; } = new InMemoryBlobStore();
}

public sealed class FileSystemBlobStoreTests : BlobStoreContractTests, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ta9-blobs-" + Guid.NewGuid().ToString("N"));

    public FileSystemBlobStoreTests() => Store = new FileSystemBlobStore(_root);

    protected override IBlobStore Store { get; }

    [Fact]
    public async Task Put_leaves_no_temp_files_behind()
    {
        await Store.PutAsync("jobs/d/results.json", new MemoryStream("x"u8.ToArray()));

        var files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories);
        Assert.Equal([Path.Combine(_root, "jobs", "d", "results.json")], files);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
