using Restaurant.Application.Features.ProductFavorites;

namespace Restaurant.Application.Common.Interfaces;

public interface IProductFavoriteService
{
    Task<ProductFavoriteListDto> ListAsync(CancellationToken cancellationToken = default);

    Task<ProductFavoriteListDto> AddAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductFavoriteListDto> RemoveAsync(Guid productId, CancellationToken cancellationToken = default);
}
