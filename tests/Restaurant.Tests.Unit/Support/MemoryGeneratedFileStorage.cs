using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Tests.Unit.Support;

internal sealed class MemoryGeneratedFileStorage : IGeneratedFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<string> SaveAsync(
        string relativePath,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var path = relativePath.Replace('\\', '/');
        Files[path] = content;
        return Task.FromResult(path);
    }

    public Task<GeneratedFileContent?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var path = relativePath.Replace('\\', '/');
        if (!Files.TryGetValue(path, out var bytes))
            return Task.FromResult<GeneratedFileContent?>(null);

        return Task.FromResult<GeneratedFileContent?>(
            new GeneratedFileContent(new MemoryStream(bytes), "application/pdf"));
    }
}
