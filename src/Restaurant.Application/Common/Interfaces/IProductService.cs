using Restaurant.Application.Features.Catalog;
using Restaurant.Application.Features.Catalog.Products;

namespace Restaurant.Application.Common.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductListItemDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ProductListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductListItemDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken = default);
    Task<ProductListItemDto?> UpdateAsync(Guid id, UpdateProductDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductRecipeDto?> GetRecipeAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductRecipeDto?> SetRecipeAsync(Guid productId, SetProductRecipeDto dto, CancellationToken cancellationToken = default);
}
