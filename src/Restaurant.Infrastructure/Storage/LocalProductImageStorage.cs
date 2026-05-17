using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;

namespace Restaurant.Infrastructure.Storage;

public sealed class LocalProductImageStorage : IProductImageStorage
{
    private readonly ProductImageOptions _options;
    private readonly string _absoluteRoot;

    public LocalProductImageStorage(IOptions<ProductImageOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _absoluteRoot = Path.IsPathRooted(_options.RootPath)
            ? _options.RootPath
            : Path.Combine(environment.ContentRootPath, _options.RootPath);
        Directory.CreateDirectory(_absoluteRoot);
    }

    public async Task<string> SaveAsync(
        Guid tenantId,
        Guid productId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var extension = NormalizeExtension(Path.GetExtension(fileName));
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"File type '{extension}' is not allowed.");

        if (content.CanSeek && content.Length > _options.MaxBytes)
            throw new InvalidOperationException($"Image must be {_options.MaxBytes / (1024 * 1024)} MB or smaller.");

        var tenantFolder = Path.Combine(_absoluteRoot, tenantId.ToString("N"));
        Directory.CreateDirectory(tenantFolder);

        var relativePath = Path.Combine(tenantId.ToString("N"), $"{productId:N}{extension}");
        var absolutePath = Path.Combine(_absoluteRoot, relativePath);

        await using var fileStream = new FileStream(
            absolutePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);
        fileStream.Close();

        var writtenBytes = new FileInfo(absolutePath).Length;
        if (writtenBytes > _options.MaxBytes)
        {
            File.Delete(absolutePath);
            throw new InvalidOperationException($"Image must be {_options.MaxBytes / (1024 * 1024)} MB or smaller.");
        }

        return relativePath.Replace('\\', '/');
    }

    public Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Task.CompletedTask;

        var absolutePath = GetSafeAbsolutePath(relativePath);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        return Task.CompletedTask;
    }

    public string? GetPublicUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var basePath = _options.PublicBasePath.TrimEnd('/');
        return $"{basePath}/{normalized}";
    }

    private string GetSafeAbsolutePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var combined = Path.GetFullPath(Path.Combine(_absoluteRoot, normalized));
        var rootFull = Path.GetFullPath(_absoluteRoot);
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid image path.");

        return combined;
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ".jpg";

        extension = extension.Trim().ToLowerInvariant();
        return extension.StartsWith('.') ? extension : $".{extension}";
    }
}
