using Restaurant.Application.Features.Catalog;

namespace Restaurant.Application.Common.Interfaces;

public interface IProductReadService
{
    Task<IReadOnlyList<ProductListItemDto>> ListAsync(CancellationToken cancellationToken = default);
}
