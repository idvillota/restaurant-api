using Restaurant.Application.Features.Sales.LunchTickets;

namespace Restaurant.Application.Common.Interfaces;

public interface ITiqueteraConfigService
{
    Task<TiqueteraConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);

    Task<TiqueteraPackDto> CreatePackAsync(CreateTiqueteraPackDto dto, CancellationToken cancellationToken = default);

    Task<TiqueteraPackDto?> UpdatePackAsync(
        Guid productId,
        UpdateTiqueteraPackDto dto,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivatePackAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<TiqueteraConfigDto> SetRedeemableProductsAsync(
        SetTiqueteraRedeemableProductsDto dto,
        CancellationToken cancellationToken = default);
}
