namespace Restaurant.Application.Features.Reports;

public sealed class SalesReportRowDto
{
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public DateTime SoldAtUtc { get; set; }
}

public sealed class SalesReportDto
{
    public string TenantName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? ProductName { get; set; }
    public IReadOnlyList<SalesReportRowDto> Rows { get; set; } = [];
}

public sealed class IngredientsReportRowDto
{
    public string IngredientName { get; set; } = string.Empty;
    public decimal? UnitCost { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? StockQuantity { get; set; }
    public decimal? ReorderLevel { get; set; }
}

public sealed class IngredientsReportDto
{
    public string TenantName { get; set; } = string.Empty;
    public string? NameFilter { get; set; }
    public IReadOnlyList<IngredientsReportRowDto> Rows { get; set; } = [];
}

public sealed class PurchasesReportRowDto
{
    public DateTime PurchasedAtUtc { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class PurchasesReportDto
{
    public string TenantName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? IngredientName { get; set; }
    public string? ProviderName { get; set; }
    public IReadOnlyList<PurchasesReportRowDto> Rows { get; set; } = [];
}

public sealed class DailySummaryReportRowDto
{
    public DateOnly Date { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal NetResult { get; set; }
    public int SalesOrderCount { get; set; }
    public int PurchaseCount { get; set; }
    public decimal ItemsSold { get; set; }
}

public sealed class DailySummaryReportDto
{
    public string TenantName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal GrandTotalSales { get; set; }
    public decimal GrandTotalPurchases { get; set; }
    public decimal GrandNetResult { get; set; }
    public IReadOnlyList<DailySummaryReportRowDto> Rows { get; set; } = [];
}
