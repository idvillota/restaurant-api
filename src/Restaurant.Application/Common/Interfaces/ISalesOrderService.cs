using Restaurant.Application.Features.Sales.SalesOrders;

namespace Restaurant.Application.Common.Interfaces;

public interface ISalesOrderService
{
    Task<IReadOnlyList<TableServiceSummaryDto>> ListTableSummariesAsync(CancellationToken cancellationToken = default);

    Task<SalesOrderDto?> GetOpenByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default);

    Task<SalesOrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SalesOrderDto> StartOrderForTableAsync(Guid tableId, CancellationToken cancellationToken = default);

    Task<SalesOrderDto?> AddLineAsync(Guid orderId, AddSalesOrderLineDto dto, CancellationToken cancellationToken = default);

    Task<SalesOrderDto?> RemovePendingLineAsync(
        Guid orderId,
        Guid lineId,
        CancellationToken cancellationToken = default);

    Task<SalesOrderDto?> UpdatePendingLineQuantityAsync(
        Guid orderId,
        Guid lineId,
        decimal quantity,
        CancellationToken cancellationToken = default);

    Task<ConfirmSalesOrderResultDto?> ConfirmOrderAsync(
        Guid orderId,
        ConfirmSalesOrderDto dto,
        CancellationToken cancellationToken = default);

    Task<SalesOrderDto?> CompleteAsync(Guid orderId, CancellationToken cancellationToken = default);
}
