using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Tests.Unit.Support;

public sealed class FakeProductImageStorage : IProductImageStorage
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public async Task<string> SaveAsync(
        Guid tenantId,
        Guid productId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
            extension = ".jpg";

        var relativePath = $"{tenantId:N}/{productId:N}{extension.ToLowerInvariant()}";
        _files[relativePath] = ms.ToArray();
        return relativePath;
    }

    public Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(relativePath))
            _files.Remove(relativePath);

        return Task.CompletedTask;
    }

    public string? GetPublicUrl(string? relativePath) =>
        string.IsNullOrWhiteSpace(relativePath) ? null : $"/media/products/{relativePath.Replace('\\', '/')}";

    public bool FileExists(string? relativePath) =>
        !string.IsNullOrWhiteSpace(relativePath) && _files.ContainsKey(relativePath);
}
