using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;

namespace Restaurant.Infrastructure.Storage;

/// <summary>Stores generated documents on the local filesystem under a base folder.</summary>
public sealed class LocalGeneratedFileStorage : IGeneratedFileStorage
{
    private readonly string _absoluteRoot;

    public LocalGeneratedFileStorage(
        IOptions<GeneratedFileStorageOptions> options,
        IHostEnvironment environment)
    {
        var rootPath = options.Value.Local.RootPath.Trim().TrimEnd('/', '\\');
        if (rootPath.Length == 0)
            rootPath = "files";

        _absoluteRoot = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.Combine(environment.ContentRootPath, rootPath);
        Directory.CreateDirectory(_absoluteRoot);
    }

    public async Task<string> SaveAsync(
        string relativePath,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var absolutePath = GetSafeAbsolutePath(normalized);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await File.WriteAllBytesAsync(absolutePath, content, cancellationToken);
        return normalized;
    }

    public Task<GeneratedFileContent?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Task.FromResult<GeneratedFileContent?>(null);

        var absolutePath = GetSafeAbsolutePath(NormalizeRelativePath(relativePath));
        if (!File.Exists(absolutePath))
            return Task.FromResult<GeneratedFileContent?>(null);

        Stream stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var contentType = GetContentType(absolutePath);
        return Task.FromResult<GeneratedFileContent?>(new GeneratedFileContent(stream, contentType));
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');

    private string GetSafeAbsolutePath(string normalizedRelativePath)
    {
        var combined = Path.GetFullPath(
            Path.Combine(_absoluteRoot, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootFull = Path.GetFullPath(_absoluteRoot);
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file path.");

        return combined;
    }

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".xml" => "application/xml",
            _ => "application/octet-stream",
        };
}
