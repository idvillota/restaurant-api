using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Sales.Bills;

public sealed class TenantSettingsDto
{
    public decimal MaxDiscountPercent { get; set; }
    public int OperationalDayCutoffHour { get; set; }
    public string TradeName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string TaxRegime { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string? LegalRepresentative { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? DianResolutionNumber { get; set; }
    public int DianResolutionFrom { get; set; }
    public int DianResolutionTo { get; set; }
    public int DianNextConsecutive { get; set; }
    public string? InvoiceNumberPrefix { get; set; }
    public decimal ImpoconsumoPercent { get; set; }
}

public sealed class UpdateTenantSettingsDto
{
    [Range(0, 100)]
    public decimal MaxDiscountPercent { get; set; }

    [Range(0, 23)]
    public int OperationalDayCutoffHour { get; set; } = 4;

    [MaxLength(200)]
    public string TradeName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string LegalName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string TaxRegime { get; set; } = "Régimen Simplificado";

    [MaxLength(40)]
    public string TaxId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? LegalRepresentative { get; set; }

    [MaxLength(300)]
    public string AddressLine { get; set; } = string.Empty;

    [MaxLength(120)]
    public string City { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Country { get; set; } = "Colombia";

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(80)]
    public string? DianResolutionNumber { get; set; }

    [Range(1, int.MaxValue)]
    public int DianResolutionFrom { get; set; }

    [Range(1, int.MaxValue)]
    public int DianResolutionTo { get; set; }

    [MaxLength(20)]
    public string? InvoiceNumberPrefix { get; set; }

    [Range(0, 100)]
    public decimal ImpoconsumoPercent { get; set; } = 8m;
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
    public string ProductTypeName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
}

public sealed class CheckoutCategoryTotalDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Total { get; set; }
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
    public decimal ImpoconsumoPercent { get; set; }
    public decimal ImpoconsumoAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalDue { get; set; }
    public IReadOnlyList<PayableOrderLineDto> Lines { get; set; } = [];
    public IReadOnlyList<CheckoutCategoryTotalDto> CategoryTotals { get; set; } = [];
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
    public decimal ImpoconsumoPercent { get; set; }
    public decimal ImpoconsumoAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime PaidAtUtc { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int DianConsecutiveNumber { get; set; }
    public string? ReceiptPdfRelativePath { get; set; }
    public string? ReceiptXmlRelativePath { get; set; }
    public IReadOnlyList<Guid> SalesOrderIds { get; set; } = [];
}
