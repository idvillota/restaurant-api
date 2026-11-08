namespace Restaurant.Application.Features.Cashier;

public sealed class PlanillaListItemDto
{
    public DateOnly BusinessDate { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal? OverShort { get; set; }
    public bool HasPdf { get; set; }
}

public sealed class PlanillaGenerateResult
{
    public string RelativePath { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal? OverShort { get; set; }
}

public sealed class PlanillaDocumentModel
{
    public string Title { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public bool IsDailyPlanilla { get; set; }
    public bool SeparateVitrinaCafeSales { get; set; }
    public decimal TotalSales { get; set; }
    public decimal VitrinaCafeSales { get; set; }
    public decimal CollectedPayments { get; set; }
    public IReadOnlyList<PlanillaCategoryRow> Categories { get; set; } = [];
    public IReadOnlyList<PlanillaPaymentRow> PaymentsByMethod { get; set; } = [];
    public IReadOnlyList<PlanillaEgresoRow> EgresosByType { get; set; } = [];
    public decimal TotalEgresos { get; set; }
    public decimal TotalPurchasesPaid { get; set; }
    public decimal? OverShort { get; set; }
    public string? DayNotes { get; set; }
    public IReadOnlyList<PlanillaShiftSection> Shifts { get; set; } = [];
}

public sealed class PlanillaCategoryRow
{
    public string ProductTypeName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class PlanillaPaymentRow
{
    public string MethodLabel { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public sealed class PlanillaEgresoRow
{
    public string TypeLabel { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public sealed class PlanillaShiftSection
{
    public string CashierEmail { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? OverShort { get; set; }
    public string? Notes { get; set; }
    public decimal TotalSales { get; set; }
    public decimal VitrinaCafeSales { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal TotalPurchasesPaid { get; set; }
}
