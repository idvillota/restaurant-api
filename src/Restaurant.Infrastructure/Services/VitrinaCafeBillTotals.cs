using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Sales;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

internal static class VitrinaCafeBillTotals
{
    public static Task<bool> IsSeparateEnabledAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken) =>
        db.TenantSettings.AsNoTracking()
            .Select(s => s.SeparateVitrinaCafeSales)
            .FirstOrDefaultAsync(cancellationToken);

    public static async Task<decimal> SumForBillsAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<Guid> billIds,
        CancellationToken cancellationToken)
    {
        var byBill = await SumByBillAsync(db, billIds, cancellationToken);
        return byBill.Values.Sum();
    }

    public static async Task<Dictionary<Guid, decimal>> SumByBillAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<Guid> billIds,
        CancellationToken cancellationToken)
    {
        if (billIds.Count == 0)
            return [];

        var lines = await db.BillLines.AsNoTracking()
            .Where(l => billIds.Contains(l.BillId))
            .Select(l => new { l.BillId, l.ProductTypeName, l.LineTotal })
            .ToListAsync(cancellationToken);

        return lines
            .Where(l => VitrinaCafeProductType.Matches(l.ProductTypeName))
            .GroupBy(l => l.BillId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.LineTotal));
    }
}
