namespace Restaurant.Application.Common.Interfaces;

public interface IProductImageStorage
{
    Task<string> SaveAsync(
        Guid tenantId,
        Guid productId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the current tenant’s logo under <c>logos/{tenantId}/logo.ext</c>
    /// (same media root/container as product images).
    /// </summary>
    Task<string> SaveTenantLogoAsync(
        Guid tenantId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads media bytes for a stored relative path (product image or tenant logo).
    /// Returns null when the path is empty or the file is missing.
    /// </summary>
    Task<byte[]?> TryReadAsync(string? relativePath, CancellationToken cancellationToken = default);

    string? GetPublicUrl(string? relativePath);
}
