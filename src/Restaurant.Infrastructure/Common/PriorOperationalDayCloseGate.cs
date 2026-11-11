using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Common;

internal static class PriorOperationalDayCloseGate
{
    internal static async Task<bool> IsEnabledAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var enabled = await db.TenantSettings
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => (bool?)s.RequirePriorDayCloseBeforeNewShift)
            .FirstOrDefaultAsync(cancellationToken);

        return enabled == true;
    }

    /// <summary>
    /// Oldest operational date before <paramref name="currentBusinessDate"/> that has cashier
    /// activity (shift or cash movement) and is not Closed. Empty dates are ignored.
    /// </summary>
    internal static async Task<DateOnly?> FindBlockingDateAsync(
        ApplicationDbContext db,
        Guid tenantId,
        DateOnly currentBusinessDate,
        CancellationToken cancellationToken)
    {
        if (!await IsEnabledAsync(db, tenantId, cancellationToken))
            return null;

        var shiftDates = await db.CashierShifts
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.BusinessDate < currentBusinessDate)
            .Select(s => s.BusinessDate)
            .Distinct()
            .ToListAsync(cancellationToken);

        var movementDates = await db.CashMovements
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.BusinessDate < currentBusinessDate)
            .Select(m => m.BusinessDate)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activityDates = shiftDates
            .Concat(movementDates)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (activityDates.Count == 0)
            return null;

        var closedDates = (await db.DailyClosures
                .AsNoTracking()
                .Where(c =>
                    c.TenantId == tenantId
                    && c.BusinessDate < currentBusinessDate
                    && c.Status == DailyClosureStatus.Closed)
                .Select(c => c.BusinessDate)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var date in activityDates)
        {
            if (!closedDates.Contains(date))
                return date;
        }

        return null;
    }

    internal static async Task<IReadOnlyList<PendingPriorOpenShiftDto>> ListOpenShiftsAsync(
        ApplicationDbContext db,
        Guid tenantId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        return await db.CashierShifts
            .AsNoTracking()
            .Where(s =>
                s.TenantId == tenantId
                && s.BusinessDate == businessDate
                && s.Status == CashierShiftStatus.Open)
            .OrderBy(s => s.OpenedAtUtc)
            .Select(s => new PendingPriorOpenShiftDto
            {
                Id = s.Id,
                CashierUserId = s.CashierUserId,
                CashierEmail = s.CashierUser.Email,
                OpenedAtUtc = s.OpenedAtUtc,
                OpeningFloat = s.OpeningFloat,
            })
            .ToListAsync(cancellationToken);
    }

    internal static string BlockingMessage(DateOnly businessDate) =>
        $"The previous operational day {businessDate:yyyy-MM-dd} must be closed before opening a cashier shift on a new working day.";
}
