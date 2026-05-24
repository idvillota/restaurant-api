using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class DailyClosureService : IDailyClosureService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ICashierShiftService _cashierShifts;

    public DailyClosureService(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        ICashierShiftService cashierShifts)
    {
        _db = db;
        _tenantContext = tenantContext;
        _cashierShifts = cashierShifts;
    }

    public async Task<DailyClosureReportDto> GetDailyReportAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        return await BuildDailyReportAsync(tenantId, businessDate, cancellationToken);
    }

    public async Task<DailyClosureReportDto> CloseDailyAsync(
        DateOnly businessDate,
        CloseDailyClosureDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var userId = ResolveUserId();

        var openShifts = await _db.CashierShifts.AnyAsync(
            s => s.TenantId == tenantId && s.BusinessDate == businessDate && s.Status == CashierShiftStatus.Open,
            cancellationToken);
        if (openShifts)
            throw new InvalidOperationException("Close all cashier shifts before closing the day.");

        var closure = await _db.DailyClosures
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.BusinessDate == businessDate, cancellationToken);

        if (closure?.Status == DailyClosureStatus.Closed)
            throw new InvalidOperationException("This operational day is already closed.");

        if (closure is null)
        {
            closure = new DailyClosure
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BusinessDate = businessDate,
                Status = DailyClosureStatus.Open,
            };
            await _db.DailyClosures.AddAsync(closure, cancellationToken);
        }

        closure.Status = DailyClosureStatus.Closed;
        closure.ClosedAtUtc = DateTime.UtcNow;
        closure.ClosedByUserId = userId;
        closure.Notes = dto.Notes?.Trim();
        _db.DailyClosures.Update(closure);
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildDailyReportAsync(tenantId, businessDate, cancellationToken);
    }

    public async Task<IReadOnlyList<DailyClosureSummaryDto>> ListClosuresAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var closures = await _db.DailyClosures
            .AsNoTracking()
            .Include(c => c.ClosedByUser)
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.BusinessDate)
            .Take(90)
            .ToListAsync(cancellationToken);

        return closures.Select(c => new DailyClosureSummaryDto
        {
            BusinessDate = c.BusinessDate,
            Status = c.Status,
            ClosedAtUtc = c.ClosedAtUtc,
            ClosedByEmail = c.ClosedByUser?.Email,
        }).ToList();
    }

    private async Task<DailyClosureReportDto> BuildDailyReportAsync(
        Guid tenantId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var closure = await _db.DailyClosures
            .AsNoTracking()
            .Include(c => c.ClosedByUser)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.BusinessDate == businessDate, cancellationToken);

        var closureSummary = new DailyClosureSummaryDto
        {
            BusinessDate = businessDate,
            Status = closure?.Status ?? DailyClosureStatus.Open,
            ClosedAtUtc = closure?.ClosedAtUtc,
            ClosedByEmail = closure?.ClosedByUser?.Email,
        };

        var shiftIds = await _db.CashierShifts
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.BusinessDate == businessDate)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.CashierShiftId != null && shiftIds.Contains(p.CashierShiftId.Value) && p.Status == PaymentStatus.Completed)
            .Join(
                _db.Bills.AsNoTracking(),
                p => p.BillId,
                b => b.Id,
                (p, b) => new ShiftPaymentLineDto
                {
                    PaymentId = p.Id,
                    BillId = b.Id,
                    BillNumber = b.Number,
                    Amount = p.Amount,
                    Method = p.Method,
                    ExternalReference = p.ExternalReference,
                    PaidAtUtc = p.PaidAtUtc,
                })
            .OrderBy(p => p.PaidAtUtc)
            .ToListAsync(cancellationToken);

        var outflows = await _db.CashMovements
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.BusinessDate == businessDate && CashMovementKinds.OutflowTypes.Contains(m.MovementType))
            .OrderBy(m => m.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        var shifts = await _db.CashierShifts
            .AsNoTracking()
            .Include(s => s.CashierUser)
            .Where(s => s.TenantId == tenantId && s.BusinessDate == businessDate)
            .OrderBy(s => s.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        var shiftRollups = new List<DailyShiftRollupDto>();
        foreach (var shift in shifts)
        {
            var shiftReport = await _cashierShifts.GetShiftReportAsync(shift.Id, cancellationToken);
            shiftRollups.Add(new DailyShiftRollupDto
            {
                ShiftId = shift.Id,
                CashierEmail = shift.CashierUser.Email,
                OpenedAtUtc = shift.OpenedAtUtc,
                ClosedAtUtc = shift.ClosedAtUtc,
                Status = shift.Status,
                TotalSales = shiftReport.TotalSales,
                CashSales = shiftReport.TotalsByMethod.Where(t => t.Method == PaymentMethod.Cash).Sum(t => t.Total),
                CardSales = shiftReport.TotalsByMethod.Where(t => t.Method == PaymentMethod.Card).Sum(t => t.Total),
                CountedCash = shift.CountedCash,
                CashOverShort = shift.CountedCash - shift.ExpectedCash,
            });
        }

        var totalCash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var totalCard = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount);
        var totalTransfer = payments.Where(p => p.Method == PaymentMethod.Transfer).Sum(p => p.Amount);
        var totalOther = payments.Where(p => p.Method == PaymentMethod.Other).Sum(p => p.Amount);
        var totalOutflows = outflows.Sum(m => m.Amount);

        return new DailyClosureReportDto
        {
            Closure = closureSummary,
            TotalSales = payments.Sum(p => p.Amount),
            BillCount = payments.Select(p => p.BillId).Distinct().Count(),
            TotalCashIn = totalCash,
            TotalCard = totalCard,
            TotalTransfer = totalTransfer,
            TotalOther = totalOther,
            TotalCashOutflows = totalOutflows,
            NetCash = totalCash - totalOutflows,
            Shifts = shiftRollups,
            CashOutflows = outflows.Select(m => new CashMovementDto
            {
                Id = m.Id,
                MovementType = m.MovementType,
                Amount = m.Amount,
                Method = m.Method,
                Description = m.Description,
                PurchaseId = m.PurchaseId,
                OccurredAtUtc = m.OccurredAtUtc,
            }).ToList(),
            Payments = payments,
        };
    }

    private Guid ResolveTenantId()
    {
        if (_tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }

    private Guid ResolveUserId()
    {
        if (_tenantContext.UserId is { } userId && userId != Guid.Empty)
            return userId;
        throw new InvalidOperationException("User context is not available.");
    }
}
