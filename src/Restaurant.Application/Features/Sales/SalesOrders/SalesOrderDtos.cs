using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Sales.SalesOrders;

public sealed class SalesOrderDto
{
    public Guid Id { get; set; }
    public Guid? DiningTableId { get; set; }
    public string? DiningTableCode { get; set; }
    public string Number { get; set; } = string.Empty;
    public SalesOrderStatus Status { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public List<SalesOrderLineDto> Lines { get; set; } = [];
}

public sealed class SalesOrderLineDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public EProductType CompositionType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    /// <summary>Recipe-based unit cost captured when the line is paid. Null for legacy sales.</summary>
    public decimal? UnitCostPrice { get; set; }
    public string? Notes { get; set; }

    /// <summary>Null until the line is sent to the kitchen on a ticket.</summary>
    public DateTime? SentToKitchenAtUtc { get; set; }

    /// <summary>Cumulative cancelled quantity (logical). Active qty is <see cref="Quantity"/>.</summary>
    public decimal CancelledQuantity { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? CancelReason { get; set; }

    public List<SalesOrderLineExcludedIngredientDto> ExcludedIngredients { get; set; } = [];
}

public sealed class SalesOrderLineExcludedIngredientDto
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
}

public sealed class UpdatePendingLineQuantityDto
{
    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; } = 1;
}

public sealed class AddSalesOrderLineDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; } = 1;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public List<Guid> ExcludedIngredientIds { get; set; } = [];
}

/// <summary>Legacy optional payload; confirmation uses pending (unsent) lines on the order.</summary>
public sealed class ConfirmSalesOrderDto
{
    public List<AddSalesOrderLineDto> Lines { get; set; } = [];
}

public sealed class KitchenTicketFileDto
{
    public string PrinterStationCode { get; set; } = string.Empty;
    public string PrinterStationName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}

public sealed class ConfirmSalesOrderResultDto
{
    public SalesOrderDto Order { get; set; } = null!;

    /// <summary>Storage-relative paths for the comanda PDFs (orders/{tenant}/...).</summary>
    public List<KitchenTicketFileDto> KitchenTickets { get; set; } = [];
}

public sealed class TableServiceSummaryDto
{
    public Guid TableId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public ETableStatus Status { get; set; }
    public int Capacity { get; set; }
    public double? LayoutX { get; set; }
    public double? LayoutY { get; set; }
    public Guid? OpenOrderId { get; set; }
    public string? OpenOrderNumber { get; set; }
    public decimal? OpenOrderTotal { get; set; }

    public int OpenOrderPendingKitchenLineCount { get; set; }
}

public sealed class RelocateOrderDto
{
    [Required]
    public Guid TargetTableId { get; set; }

    /// <summary>
    /// When the target table is busy: null = ask client to confirm merge;
    /// false = cancel (no changes); true = merge into the target order.
    /// Ignored when the target is free.
    /// </summary>
    public bool? MergeIfTargetBusy { get; set; }
}

public static class RelocateOrderActions
{
    public const string Transferred = "transferred";
    public const string Merged = "merged";
    public const string Cancelled = "cancelled";
    public const string MergeRequired = "merge_required";
}

public sealed class RelocateOrderResultDto
{
    public string Action { get; set; } = string.Empty;
    public SalesOrderDto? Order { get; set; }
    public Guid? SourceTableId { get; set; }
    public Guid? TargetTableId { get; set; }
    public string? TargetTableCode { get; set; }
    public Guid? TargetOrderId { get; set; }
    public string? Message { get; set; }
}

public sealed class CancelSalesOrderLineItemDto
{
    [Required]
    public Guid LineId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; } = 1;
}

public sealed class CancelSalesOrderLinesDto
{
    [Required]
    [MinLength(1)]
    public List<CancelSalesOrderLineItemDto> Lines { get; set; } = [];

    /// <summary>Reason code from <see cref="SalesOrderCancelReasons"/>.</summary>
    [Required]
    [MaxLength(64)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Free text when reason is <c>other</c>.</summary>
    [MaxLength(300)]
    public string? ReasonDetail { get; set; }

    /// <summary>
    /// When cancelling leaves no active lines: true voids the order and frees the table;
    /// false leaves an empty active order (UI should ask).
    /// </summary>
    public bool VoidOrderIfEmpty { get; set; }
}

public sealed class VoidSalesOrderDto
{
    [Required]
    [MaxLength(64)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? ReasonDetail { get; set; }
}

/// <summary>Same shape as confirm: updated order + anulación kitchen PDFs.</summary>
public sealed class CancelSalesOrderResultDto
{
    public SalesOrderDto Order { get; set; } = null!;
    public List<KitchenTicketFileDto> KitchenTickets { get; set; } = [];
    public bool OrderVoided { get; set; }
    public string? Message { get; set; }
}
