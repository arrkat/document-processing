using System.Text.Json;

namespace BlobStorage;

public static class BlobStoreJsonExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web);

    public static async Task PutJsonAsync<T>(this IBlobStore store, string key, T value,
        JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await JsonSerializer.SerializeAsync(buffer, value, options ?? DefaultOptions, cancellationToken);
        buffer.Position = 0;
        await store.PutAsync(key, buffer, cancellationToken);
    }

    /// <exception cref="BlobNotFoundException">No blob exists for <paramref name="key"/>.</exception>
    /// <exception cref="JsonException">The blob is not valid JSON for <typeparamref name="T"/>.</exception>
    public static async Task<T> GetJsonAsync<T>(this IBlobStore store, string key,
        JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        await using var stream = await store.OpenReadAsync(key, cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, options ?? DefaultOptions, cancellationToken)
               ?? throw new JsonException($"Blob '{key}' contained JSON null.");
    }
}
