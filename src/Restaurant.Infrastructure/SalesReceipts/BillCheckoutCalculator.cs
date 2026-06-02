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
        // IMPORTANT: Menu prices are tax-included. Subtotal and discounts are applied over the tax-included amount.
        // We "extract" impoconsumo from the discounted gross, instead of adding it on top.
        var discountedGross = Math.Max(0, subtotal - discountAmount);
        if (discountedGross <= 0 || impoconsumoPercent <= 0)
        {
            var due = decimal.Round(discountedGross + Math.Max(0, tipAmount), 2, MidpointRounding.AwayFromZero);
            return (0m, due);
        }

        var divisor = 1m + (impoconsumoPercent / 100m);
        var baseAmount = decimal.Round(discountedGross / divisor, 2, MidpointRounding.AwayFromZero);
        var impoconsumo = decimal.Round(discountedGross - baseAmount, 2, MidpointRounding.AwayFromZero);
        var totalDue = decimal.Round(discountedGross + Math.Max(0, tipAmount), 2, MidpointRounding.AwayFromZero);
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
