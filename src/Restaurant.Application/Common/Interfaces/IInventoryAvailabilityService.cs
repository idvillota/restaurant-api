using Restaurant.Application.Features.Inventory;
using Restaurant.Application.Features.Sales.SalesOrders;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Common.Interfaces;

public interface IInventoryAvailabilityService
{
    Task<StockAvailabilityResultDto> CheckLinesAsync(
        StockAvailabilityCheckDto dto,
        CancellationToken cancellationToken = default);

    Task<StockAvailabilityResultDto> CheckKitchenBatchAsync(
        Guid salesOrderId,
        IReadOnlyList<AddSalesOrderLineDto> newLines,
        CancellationToken cancellationToken = default);

    Task<StockAvailabilityResultDto> CheckOrdersForPaymentAsync(
        IReadOnlyList<Guid> salesOrderIds,
        CancellationToken cancellationToken = default);

    Task<StockAvailabilityResultDto> CheckOrdersForPaymentFromEntitiesAsync(
        IReadOnlyList<SalesOrder> orders,
        CancellationToken cancellationToken = default);

    void EnsureAvailable(StockAvailabilityResultDto result);
}
