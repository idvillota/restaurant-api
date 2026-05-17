namespace Restaurant.Application.Common.Interfaces;

public interface IProductImageStorage
{
    Task<string> SaveAsync(
        Guid tenantId,
        Guid productId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default);

    string? GetPublicUrl(string? relativePath);
}
