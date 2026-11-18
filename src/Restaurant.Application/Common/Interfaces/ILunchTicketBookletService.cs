using Restaurant.Application.Features.Sales.LunchTickets;

namespace Restaurant.Application.Common.Interfaces;

public interface ILunchTicketBookletService
{
    Task<LunchTicketBalanceDto> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<int> GetRemainingTicketsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task CreateFromPackSaleAsync(
        Guid customerId,
        Guid billId,
        Guid packProductId,
        int ticketsPerPack,
        decimal quantity,
        decimal packUnitPrice,
        CancellationToken cancellationToken = default);

    Task RedeemAsync(Guid customerId, int tickets, CancellationToken cancellationToken = default);
}
