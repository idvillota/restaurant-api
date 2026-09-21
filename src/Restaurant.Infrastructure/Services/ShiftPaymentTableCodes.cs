using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Cashier;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

internal static class ShiftPaymentTableCodes
{
    /// <summary>
    /// Fills missing <see cref="ShiftPaymentLineDto.TableCodes"/> from linked orders
    /// when <c>Bill.TableCodesSnapshot</c> was never stored.
    /// </summary>
    public static async Task FillMissingAsync(
        ApplicationDbContext db,
        IList<ShiftPaymentLineDto> payments,
        CancellationToken cancellationToken)
    {
        var missingBillIds = payments
            .Where(p => string.IsNullOrWhiteSpace(p.TableCodes))
            .Select(p => p.BillId)
            .Distinct()
            .ToList();

        if (missingBillIds.Count == 0)
            return;

        var rows = await (
            from bo in db.BillSalesOrders.AsNoTracking()
            join o in db.SalesOrders.AsNoTracking() on bo.SalesOrderId equals o.Id
            join t in db.DiningTables.AsNoTracking() on o.DiningTableId equals t.Id into tables
            from t in tables.DefaultIfEmpty()
            where missingBillIds.Contains(bo.BillId)
            select new { bo.BillId, Code = t != null ? t.Code : null }
        ).ToListAsync(cancellationToken);

        var codesByBill = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Code))
            .GroupBy(r => r.BillId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(x => x.Code!).Distinct(StringComparer.OrdinalIgnoreCase)));

        foreach (var payment in payments)
        {
            if (!string.IsNullOrWhiteSpace(payment.TableCodes))
                continue;
            if (codesByBill.TryGetValue(payment.BillId, out var codes))
                payment.TableCodes = codes;
        }
    }
}
