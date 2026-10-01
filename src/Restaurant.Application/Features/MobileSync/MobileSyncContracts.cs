namespace Restaurant.Application.Features.MobileSync;

public sealed class RegisterMobileDeviceRequest
{
    public string DeviceId { get; set; } = string.Empty;
}

public sealed class MobileSyncBatchRequest
{
    public string BatchId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public List<MobileSyncBatchOrder> Orders { get; set; } = [];
}

public sealed class MobileSyncBatchOrder
{
    public string LocalId { get; set; } = string.Empty;
    public Guid? RemoteId { get; set; }
    public string? TableId { get; set; }
    public string? TableCode { get; set; }
    public string? WaiterName { get; set; }
    public long OpenedAtUtc { get; set; }
    public List<MobileSyncBatchLine> Lines { get; set; } = [];
}

public sealed class MobileSyncBatchLine
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public sealed class MobileSyncBatchResult
{
    public string LocalId { get; set; } = string.Empty;
    public string RemoteId { get; set; } = string.Empty;
    public string Status { get; set; } = "failed";
    public string? Error { get; set; }
}

public sealed class MobileSyncBatchResponse
{
    public List<MobileSyncBatchResult> Results { get; set; } = [];
}

public sealed class PendingMobileSaleLine
{
    public Guid LineId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class PendingMobileSale
{
    public Guid RemoteOrderId { get; set; }
    public Guid? TableId { get; set; }
    public string TableCode { get; set; } = string.Empty;
    public string WaiterName { get; set; } = "HOST";
    public string Cursor { get; set; } = string.Empty;
    public List<PendingMobileSaleLine> Lines { get; set; } = [];
}

public sealed class PendingMobileSalesResponse
{
    public List<PendingMobileSale> Sales { get; set; } = [];
    public string Cursor { get; set; } = string.Empty;
}

public sealed class MobileSyncPaymentRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public List<Guid> RemoteIds { get; set; } = [];
    public string PaymentMethod { get; set; } = "Cash";
    public decimal TipAmount { get; set; }
}

public sealed class MobileSyncPaymentResponse
{
    public string Status { get; set; } = "failed";
    public string? Error { get; set; }
    public bool TablesAvailable { get; set; }
}

public interface IMobileSyncService
{
    Task<MobileSyncBatchResponse> UploadBatchAsync(
        MobileSyncBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<PendingMobileSalesResponse> GetPendingSalesAsync(
        string deviceId,
        string? since,
        CancellationToken cancellationToken = default);

    Task<MobileSyncPaymentResponse> PayAsync(
        MobileSyncPaymentRequest request,
        CancellationToken cancellationToken = default);
}
