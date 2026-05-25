using Restaurant.Application.Features.Sales.Bills;

namespace Restaurant.Infrastructure.SalesReceipts;

internal static class BillCheckoutCalculator
{
    public static (decimal ImpoconsumoAmount, decimal TotalDue) ComputeTotals(
        decimal subtotal,
        decimal discountAmount,
        decimal tipAmount,
        decimal impoconsumoPercent)
    {
        var taxableBase = Math.Max(0, subtotal - discountAmount);
        var impoconsumo = decimal.Round(
            taxableBase * (impoconsumoPercent / 100m),
            2,
            MidpointRounding.AwayFromZero);
        var totalDue = decimal.Round(taxableBase + impoconsumo + Math.Max(0, tipAmount), 2, MidpointRounding.AwayFromZero);
        return (impoconsumo, totalDue);
    }

    public static IReadOnlyList<CheckoutCategoryTotalDto> BuildCategoryTotals(
        IReadOnlyList<PayableOrderLineDto> lines)
    {
        return lines
            .GroupBy(l => string.IsNullOrWhiteSpace(l.ProductTypeName) ? "Otros" : l.ProductTypeName)
            .Select(g => new CheckoutCategoryTotalDto
            {
                CategoryName = g.Key,
                Total = g.Sum(x => x.LineTotal),
            })
            .OrderBy(c => c.CategoryName)
            .ToList();
    }

    public static decimal AllocateLineImpoconsumo(
        decimal lineTotal,
        decimal subtotal,
        decimal totalImpoconsumo)
    {
        if (subtotal <= 0 || totalImpoconsumo <= 0)
            return 0;

        return decimal.Round(lineTotal / subtotal * totalImpoconsumo, 2, MidpointRounding.AwayFromZero);
    }

    public static string FormatInvoiceDisplayNumber(string? prefix, int consecutive)
    {
        var p = prefix?.Trim();
        return string.IsNullOrEmpty(p) ? consecutive.ToString() : $"{p}{consecutive}";
    }
}
