using Restaurant.Application.Features.Sales.KitchenTickets;
using Restaurant.Application.Features.Sales.SalesOrders;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Common.Interfaces;

public interface IKitchenTicketService
{
    Task<KitchenTicketModel> BuildTicketModelAsync(
        SalesOrder order,
        IReadOnlyList<AddSalesOrderLineDto> batchLines,
        CancellationToken cancellationToken = default);

    Task<string?> GeneratePdfAsync(KitchenTicketModel model, CancellationToken cancellationToken = default);
}
