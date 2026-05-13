using Restaurant.Application.Features.Catalog.ProductTypes;

namespace Restaurant.Application.Common.Interfaces;

public interface IProductTypeService
{
    Task<IReadOnlyList<ProductTypeDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ProductTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductTypeDto> CreateAsync(CreateProductTypeDto dto, CancellationToken cancellationToken = default);
    Task<ProductTypeDto?> UpdateAsync(Guid id, UpdateProductTypeDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
