using Restaurant.Application.Features.Sales.LunchTickets;

namespace Restaurant.Application.Common.Interfaces;

public interface ITiqueteraSalesService
{
    Task<SellTiqueteraPackResultDto> SellPackAsync(
        SellTiqueteraPackDto dto,
        CancellationToken cancellationToken = default);
}
