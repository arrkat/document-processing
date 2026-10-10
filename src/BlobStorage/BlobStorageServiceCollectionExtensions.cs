using Microsoft.Extensions.DependencyInjection;

namespace BlobStorage;

public static class BlobStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="FileSystemBlobStore"/>. A relative <paramref name="rootPath"/> is resolved against
    /// <paramref name="contentRootPath"/> so both services can share one folder via the same relative setting.
    /// </summary>
    public static IServiceCollection AddFileSystemBlobStore(this IServiceCollection services,
        string rootPath, string contentRootPath)
    {
        var fullPath = Path.IsPathRooted(rootPath) ? rootPath : Path.Combine(contentRootPath, rootPath);
        return services.AddSingleton<IBlobStore>(new FileSystemBlobStore(fullPath));
    }
}
