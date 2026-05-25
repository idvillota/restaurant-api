namespace Restaurant.Application.Features.Sales.SalesReceipts;

public sealed class SalesReceiptTenantInfo
{
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
}

public sealed class SalesReceiptLineModel
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductTypeName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal ImpoconsumoAmount { get; set; }
    public string? Notes { get; set; }
}

public sealed class SalesReceiptCategoryTotalModel
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public sealed class SalesReceiptModel
{
    public SalesReceiptTenantInfo Tenant { get; set; } = new();
    public string InvoiceDisplayNumber { get; set; } = string.Empty;
    public int DianConsecutiveNumber { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public string? TableCodes { get; set; }
    public string? OrderNumbers { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerTaxId { get; set; }
    public string? CashierName { get; set; }
    public IReadOnlyList<SalesReceiptLineModel> Lines { get; set; } = [];
    public IReadOnlyList<SalesReceiptCategoryTotalModel> CategoryTotals { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal ImpoconsumoPercent { get; set; }
    public decimal ImpoconsumoAmount { get; set; }
    public decimal TipAmount { get; set; }
    public decimal Total { get; set; }
    public string CurrencyCode { get; set; } = "COP";
}

public sealed class SalesReceiptFilesDto
{
    public string? PdfRelativePath { get; set; }
    public string? XmlRelativePath { get; set; }
}
