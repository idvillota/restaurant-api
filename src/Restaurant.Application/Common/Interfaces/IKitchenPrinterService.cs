using Restaurant.Application.Features.KitchenPrinters;
using Restaurant.Application.Features.Sales.SalesOrders;

namespace Restaurant.Application.Common.Interfaces;

public interface IKitchenPrinterService
{
    Task EnsureDefaultStationAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrinterStationDto>> ListStationsAsync(CancellationToken cancellationToken = default);

    Task<PrinterStationDto> CreateStationAsync(
        CreatePrinterStationDto dto,
        CancellationToken cancellationToken = default);

    Task<PrinterStationDto?> UpdateStationAsync(
        Guid id,
        UpdatePrinterStationDto dto,
        CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteStationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductTypePrinterRoutingDto> GetRoutingAsync(CancellationToken cancellationToken = default);

    Task<ProductTypePrinterRoutingDto> UpdateRoutingAsync(
        UpdateProductTypePrinterRoutingDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, (Guid StationId, string StationName, List<AddSalesOrderLineDto> Lines)>> GroupBatchByStationAsync(
        IReadOnlyList<AddSalesOrderLineDto> batchLines,
        CancellationToken cancellationToken = default);
}
