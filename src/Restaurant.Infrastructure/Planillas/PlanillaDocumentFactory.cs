using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Enums;
using Restaurant.Domain.Sales;

namespace Restaurant.Infrastructure.Planillas;

internal static class PlanillaDocumentFactory
{
    public static PlanillaDocumentModel Build(
        string tenantName,
        DateOnly businessDate,
        bool isDailyPlanilla,
        bool separateVitrina,
        string? dayNotes,
        IReadOnlyList<PlanillaShiftInput> shifts)
    {
        var dayPayments = shifts.SelectMany(s => s.Payments).ToList();
        var dayOutflows = shifts.SelectMany(s => s.Outflows).ToList();
        var dayLines = shifts.SelectMany(s => s.BillLines).ToList();

        var collected = dayPayments.Sum(p => p.Amount);
        var vitrinaLines = dayLines
            .Where(l => VitrinaCafeProductType.Matches(l.ProductTypeName))
            .Sum(l => l.LineTotal);
        var (totalSales, vitrinaSales) = separateVitrina
            ? VitrinaCafeProductType.SplitCollected(collected, vitrinaLines)
            : (collected, 0m);

        var overShorts = shifts
            .Where(s => s.CountedCash is not null && s.ExpectedCash is not null)
            .Select(s => s.CountedCash!.Value - s.ExpectedCash!.Value)
            .ToList();
        decimal? overShort = overShorts.Count == 0 ? null : overShorts.Sum();

        return new PlanillaDocumentModel
        {
            Title = isDailyPlanilla ? "Planilla" : "Cierre de turno",
            TenantName = tenantName,
            BusinessDate = businessDate,
            IsDailyPlanilla = isDailyPlanilla,
            SeparateVitrinaCafeSales = separateVitrina,
            TotalSales = totalSales,
            VitrinaCafeSales = vitrinaSales,
            CollectedPayments = collected,
            Categories = GroupCategories(dayLines),
            PaymentsByMethod = GroupPayments(dayPayments),
            EgresosByType = GroupEgresos(dayOutflows),
            TotalEgresos = dayOutflows.Sum(o => o.Amount),
            TotalPurchasesPaid = dayOutflows.Where(o => o.PurchaseId is not null).Sum(o => o.Amount),
            OverShort = overShort,
            DayNotes = dayNotes,
            Shifts = shifts.Select(s => MapShift(s, separateVitrina)).ToList(),
        };
    }

    private static PlanillaShiftSection MapShift(PlanillaShiftInput shift, bool separateVitrina)
    {
        var collected = shift.Payments.Sum(p => p.Amount);
        var vitrina = shift.BillLines
            .Where(l => VitrinaCafeProductType.Matches(l.ProductTypeName))
            .Sum(l => l.LineTotal);
        var (sales, vitrinaSales) = separateVitrina
            ? VitrinaCafeProductType.SplitCollected(collected, vitrina)
            : (collected, 0m);

        return new PlanillaShiftSection
        {
            CashierEmail = shift.CashierEmail,
            OpenedAtUtc = shift.OpenedAtUtc,
            ClosedAtUtc = shift.ClosedAtUtc,
            OpeningFloat = shift.OpeningFloat,
            ExpectedCash = shift.ExpectedCash,
            CountedCash = shift.CountedCash,
            OverShort = shift.CountedCash is not null && shift.ExpectedCash is not null
                ? shift.CountedCash - shift.ExpectedCash
                : null,
            Notes = shift.Notes,
            TotalSales = sales,
            VitrinaCafeSales = vitrinaSales,
            TotalEgresos = shift.Outflows.Sum(o => o.Amount),
            TotalPurchasesPaid = shift.Outflows.Where(o => o.PurchaseId is not null).Sum(o => o.Amount),
        };
    }

    private static IReadOnlyList<PlanillaCategoryRow> GroupCategories(
        IReadOnlyList<PlanillaBillLineInput> lines) =>
        lines
            .GroupBy(l => string.IsNullOrWhiteSpace(l.ProductTypeName) ? "Otros" : l.ProductTypeName.Trim())
            .Select(g => new PlanillaCategoryRow
            {
                ProductTypeName = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                LineTotal = g.Sum(x => x.LineTotal),
            })
            .Where(r => r.Quantity != 0 || r.LineTotal != 0)
            .OrderBy(r => r.ProductTypeName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static IReadOnlyList<PlanillaPaymentRow> GroupPayments(
        IReadOnlyList<PlanillaPaymentInput> payments) =>
        payments
            .GroupBy(p => p.Method)
            .OrderBy(g => g.Key)
            .Select(g => new PlanillaPaymentRow
            {
                MethodLabel = PaymentLabel(g.Key),
                Total = g.Sum(p => p.Amount),
                Count = g.Count(),
            })
            .ToList();

    private static IReadOnlyList<PlanillaEgresoRow> GroupEgresos(
        IReadOnlyList<PlanillaOutflowInput> outflows) =>
        outflows
            .GroupBy(o => o.MovementType)
            .OrderBy(g => g.Key)
            .Select(g => new PlanillaEgresoRow
            {
                TypeLabel = EgresoLabel(g.Key),
                Total = g.Sum(o => o.Amount),
                Count = g.Count(),
            })
            .ToList();

    public static string PaymentLabel(PaymentMethod method) =>
        method switch
        {
            PaymentMethod.Cash => "Efectivo",
            PaymentMethod.Card => "Tarjeta",
            PaymentMethod.Transfer => "Transferencia",
            _ => "Otro",
        };

    public static string EgresoLabel(CashMovementType type) =>
        type switch
        {
            CashMovementType.ProviderPayment => "Pago a proveedor",
            CashMovementType.PettyCash => "Caja menor",
            CashMovementType.Refund => "Devolución",
            CashMovementType.OtherOutflow => "Otro egreso",
            CashMovementType.AdjustmentOut => "Ajuste salida",
            _ => type.ToString(),
        };
}

internal sealed class PlanillaShiftInput
{
    public required string CashierEmail { get; init; }
    public required DateTime OpenedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public decimal OpeningFloat { get; init; }
    public decimal? ExpectedCash { get; init; }
    public decimal? CountedCash { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<PlanillaPaymentInput> Payments { get; init; } = [];
    public IReadOnlyList<PlanillaOutflowInput> Outflows { get; init; } = [];
    public IReadOnlyList<PlanillaBillLineInput> BillLines { get; init; } = [];
}

internal sealed class PlanillaPaymentInput
{
    public Guid BillId { get; init; }
    public decimal Amount { get; init; }
    public PaymentMethod Method { get; init; }
}

internal sealed class PlanillaOutflowInput
{
    public CashMovementType MovementType { get; init; }
    public decimal Amount { get; init; }
    public Guid? PurchaseId { get; init; }
}

internal sealed class PlanillaBillLineInput
{
    public Guid BillId { get; init; }
    public string ProductTypeName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal LineTotal { get; init; }
}
