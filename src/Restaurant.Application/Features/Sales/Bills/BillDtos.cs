using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Sales.Bills;

public sealed class TenantSettingsDto
{
    public decimal MaxDiscountPercent { get; set; }
    public int OperationalDayCutoffHour { get; set; }
}

public sealed class UpdateTenantSettingsDto
{
    [Range(0, 100)]
    public decimal MaxDiscountPercent { get; set; }

    [Range(0, 23)]
    public int OperationalDayCutoffHour { get; set; } = 4;
}

public sealed class PayableTableGroupDto
{
    public Guid TableId { get; set; }
    public string TableCode { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public IReadOnlyList<PayableOrderDto> Orders { get; set; } = [];
}

public sealed class PayableOrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public IReadOnlyList<PayableOrderLineDto> Lines { get; set; } = [];
}

public sealed class PayableOrderLineDto
{
    public Guid LineId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? TableCode { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
}

public sealed class CheckoutPaymentLineDto
{
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    [MaxLength(120)]
    public string? ExternalReference { get; set; }
}

public class CheckoutPreviewDto
{
    [MinLength(1)]
    public List<Guid> SalesOrderIds { get; set; } = [];

    [Range(0, 100)]
    public decimal? DiscountPercent { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? DiscountAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TipAmount { get; set; }
}

public sealed class CheckoutTotalsDto
{
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal MaxDiscountPercent { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalDue { get; set; }
    public IReadOnlyList<PayableOrderLineDto> Lines { get; set; } = [];
}

public sealed class FinalizeCheckoutDto : CheckoutPreviewDto
{
    public Guid? CustomerId { get; set; }

    [MinLength(1)]
    public List<CheckoutPaymentLineDto> Payments { get; set; } = [];
}

public sealed class BillDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public BillStatus Status { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime PaidAtUtc { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public IReadOnlyList<Guid> SalesOrderIds { get; set; } = [];
}
