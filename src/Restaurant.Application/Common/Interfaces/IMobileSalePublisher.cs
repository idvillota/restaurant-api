namespace Restaurant.Application.Common.Interfaces;

public sealed record MobileSaleLineNotice(
    Guid LineId,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    string? Notes);

public sealed record MobileSaleNotice(
    Guid RemoteOrderId,
    Guid? TableId,
    string TableCode,
    string WaiterName,
    string Cursor,
    IReadOnlyList<MobileSaleLineNotice> Lines);

public interface IMobileSalePublisher
{
    Task PublishAsync(
        Guid tenantId,
        MobileSaleNotice notice,
        string? exceptDeviceId,
        CancellationToken cancellationToken = default);

    Task PublishTablesAvailableAsync(
        Guid tenantId,
        IReadOnlyList<Guid> tableIds,
        CancellationToken cancellationToken = default);
}
