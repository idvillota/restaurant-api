using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class CashierShiftService : ICashierShiftService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;

    public CashierShiftService(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BusinessDayContextDto> GetBusinessDayContextAsync(CancellationToken cancellationToken = default)
    {
        var (tenantId, _, businessDate, cutoffHour) = await ResolveBusinessContextAsync(cancellationToken);
        var closure = await GetOrCreateDailyClosureAsync(tenantId, businessDate, cancellationToken);
        var openShiftsCount = await _db.CashierShifts.CountAsync(
            s => s.TenantId == tenantId && s.BusinessDate == businessDate && s.Status == CashierShiftStatus.Open,
            cancellationToken);

        var myOpen = await GetMyOpenShiftAsync(cancellationToken);

        return new BusinessDayContextDto
        {
            BusinessDate = businessDate,
            OperationalDayCutoffHour = cutoffHour,
            DailyClosureStatus = closure.Status,
            OpenShiftsCount = openShiftsCount,
            MyOpenShift = myOpen,
        };
    }

    public async Task<CashierShiftSummaryDto?> GetMyOpenShiftAsync(CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _, _) = await ResolveBusinessContextAsync(cancellationToken);
        var shift = await _db.CashierShifts
            .AsNoTracking()
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && s.CashierUserId == userId && s.Status == CashierShiftStatus.Open,
                cancellationToken);

        return shift is null ? null : MapSummary(shift);
    }

    public async Task<CashierShiftSummaryDto> OpenShiftAsync(
        OpenCashierShiftDto dto,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, businessDate, _) = await ResolveBusinessContextAsync(cancellationToken);
        await EnsureDayNotClosedAsync(tenantId, businessDate, cancellationToken);

        var existing = await _db.CashierShifts.AnyAsync(
            s => s.TenantId == tenantId && s.CashierUserId == userId && s.Status == CashierShiftStatus.Open,
            cancellationToken);
        if (existing)
            throw new InvalidOperationException("You already have an open cashier shift.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var shift = new CashierShift
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashierUserId = userId,
            Status = CashierShiftStatus.Open,
            BusinessDate = businessDate,
            OpenedAtUtc = DateTime.UtcNow,
            OpeningFloat = decimal.Round(Math.Max(0, dto.OpeningFloat), 2, MidpointRounding.AwayFromZero),
        };

        await _db.CashierShifts.AddAsync(shift, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return MapSummary(shift, user.Email);
    }

    public async Task<CashierShiftReportDto> CloseShiftAsync(
        Guid shiftId,
        CloseCashierShiftDto dto,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _, _) = await ResolveBusinessContextAsync(cancellationToken);
        var shift = await _db.CashierShifts
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Cashier shift was not found.");

        if (shift.Status != CashierShiftStatus.Open)
            throw new InvalidOperationException("This shift is already closed.");

        if (shift.CashierUserId != userId)
            throw new InvalidOperationException("Only the cashier who opened this shift can close it.");

        var report = await BuildShiftReportAsync(shift, cancellationToken);
        var expectedCash = report.Shift.OpeningFloat + report.TotalsByMethod
            .Where(t => t.Method == PaymentMethod.Cash)
            .Sum(t => t.Total) - report.CashOutflowTotal;

        shift.ExpectedCash = decimal.Round(expectedCash, 2, MidpointRounding.AwayFromZero);
        shift.CountedCash = decimal.Round(Math.Max(0, dto.CountedCash), 2, MidpointRounding.AwayFromZero);
        shift.ClosingNotes = dto.ClosingNotes?.Trim();
        shift.ClosedAtUtc = DateTime.UtcNow;
        shift.Status = CashierShiftStatus.Closed;
        _db.CashierShifts.Update(shift);
        await _db.SaveChangesAsync(cancellationToken);

        report.Shift = MapSummary(shift);
        report.Shift.CashOverShort = shift.CountedCash - shift.ExpectedCash;
        return report;
    }

    public async Task<CashierShiftReportDto> GetShiftReportAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var shift = await _db.CashierShifts
            .AsNoTracking()
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Cashier shift was not found.");

        return await BuildShiftReportAsync(shift, cancellationToken);
    }

    public async Task<IReadOnlyList<CashierShiftSummaryDto>> ListShiftsAsync(
        DateOnly? businessDate,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var query = _db.CashierShifts
            .AsNoTracking()
            .Include(s => s.CashierUser)
            .Where(s => s.TenantId == tenantId);

        if (businessDate is { } date)
            query = query.Where(s => s.BusinessDate == date);

        var shifts = await query
            .OrderByDescending(s => s.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return shifts.Select(s => MapSummary(s)).ToList();
    }

    public async Task<CashMovementDto> RecordCashMovementAsync(
        CreateCashMovementDto dto,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, businessDate, _) = await ResolveBusinessContextAsync(cancellationToken);
        await EnsureDayNotClosedAsync(tenantId, businessDate, cancellationToken);

        if (dto.Amount <= 0)
            throw new InvalidOperationException("Amount must be greater than zero.");

        CashierShift? shift = null;
        if (dto.CashierShiftId is { } shiftId)
        {
            shift = await _db.CashierShifts.FirstOrDefaultAsync(
                s => s.Id == shiftId && s.TenantId == tenantId,
                cancellationToken)
                ?? throw new InvalidOperationException("Cashier shift was not found.");

            if (shift.Status != CashierShiftStatus.Open)
                throw new InvalidOperationException("Cash movements can only be linked to an open shift.");
        }
        else
        {
            shift = await _db.CashierShifts.FirstOrDefaultAsync(
                s => s.TenantId == tenantId && s.CashierUserId == userId && s.Status == CashierShiftStatus.Open,
                cancellationToken);
        }

        if (dto.PurchaseId is { } purchaseId)
        {
            var purchaseExists = await _db.Purchases.AnyAsync(p => p.Id == purchaseId, cancellationToken);
            if (!purchaseExists)
                throw new InvalidOperationException("Purchase was not found.");
        }

        var movement = new CashMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashierShiftId = shift?.Id,
            BusinessDate = shift?.BusinessDate ?? businessDate,
            MovementType = dto.MovementType,
            Amount = decimal.Round(dto.Amount, 2, MidpointRounding.AwayFromZero),
            Method = dto.Method,
            Description = dto.Description.Trim(),
            PurchaseId = dto.PurchaseId,
            CreatedByUserId = userId,
            OccurredAtUtc = DateTime.UtcNow,
        };

        await _db.CashMovements.AddAsync(movement, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return MapMovement(movement);
    }

    public async Task<(Guid ShiftId, Guid UserId)> RequireOpenShiftAsync(CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, businessDate, _) = await ResolveBusinessContextAsync(cancellationToken);
        await EnsureDayNotClosedAsync(tenantId, businessDate, cancellationToken);

        var shift = await _db.CashierShifts.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.CashierUserId == userId && s.Status == CashierShiftStatus.Open,
            cancellationToken);

        if (shift is null)
            throw new InvalidOperationException("Open a cashier shift before processing payments.");

        return (shift.Id, userId);
    }

    private async Task<CashierShiftReportDto> BuildShiftReportAsync(
        CashierShift shift,
        CancellationToken cancellationToken)
    {
        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.CashierShiftId == shift.Id && p.Status == PaymentStatus.Completed)
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
            .Where(m => m.CashierShiftId == shift.Id && CashMovementKinds.OutflowTypes.Contains(m.MovementType))
            .OrderBy(m => m.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        var totalsByMethod = payments
            .GroupBy(p => p.Method)
            .Select(g => new ShiftTotalsByMethodDto
            {
                Method = g.Key,
                Total = g.Sum(p => p.Amount),
                Count = g.Count(),
            })
            .OrderBy(t => t.Method)
            .ToList();

        var billCount = payments.Select(p => p.BillId).Distinct().Count();
        var totalSales = payments.Sum(p => p.Amount);
        var cashOutflowTotal = outflows.Sum(m => m.Amount);

        var summary = MapSummary(shift);
        if (summary.ExpectedCash is not null && summary.CountedCash is not null)
            summary.CashOverShort = summary.CountedCash - summary.ExpectedCash;

        return new CashierShiftReportDto
        {
            Shift = summary,
            TotalSales = totalSales,
            BillCount = billCount,
            TotalsByMethod = totalsByMethod,
            Payments = payments,
            CashOutflows = outflows.Select(MapMovement).ToList(),
            CashOutflowTotal = cashOutflowTotal,
        };
    }

    private async Task EnsureDayNotClosedAsync(
        Guid tenantId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var closure = await _db.DailyClosures.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.BusinessDate == businessDate, cancellationToken);

        if (closure?.Status == DailyClosureStatus.Closed)
            throw new InvalidOperationException("The operational day is already closed.");
    }

    private async Task<DailyClosure> GetOrCreateDailyClosureAsync(
        Guid tenantId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var closure = await _db.DailyClosures
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.BusinessDate == businessDate, cancellationToken);

        if (closure is not null)
            return closure;

        closure = new DailyClosure
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessDate = businessDate,
            Status = DailyClosureStatus.Open,
        };
        await _db.DailyClosures.AddAsync(closure, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return closure;
    }

    private async Task<(Guid TenantId, Guid UserId, DateOnly BusinessDate, int CutoffHour)> ResolveBusinessContextAsync(
        CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var userId = ResolveUserId();
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        var settings = await _db.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        var cutoffHour = settings?.OperationalDayCutoffHour ?? 4;
        var businessDate = BusinessDayCalculator.ResolveBusinessDate(
            DateTime.UtcNow,
            tenant.TimeZoneId,
            cutoffHour);

        return (tenantId, userId, businessDate, cutoffHour);
    }

    private static CashierShiftSummaryDto MapSummary(CashierShift shift, string? cashierEmail = null) =>
        new()
        {
            Id = shift.Id,
            CashierUserId = shift.CashierUserId,
            CashierEmail = cashierEmail ?? shift.CashierUser?.Email ?? string.Empty,
            Status = shift.Status,
            BusinessDate = shift.BusinessDate,
            OpenedAtUtc = shift.OpenedAtUtc,
            ClosedAtUtc = shift.ClosedAtUtc,
            OpeningFloat = shift.OpeningFloat,
            ExpectedCash = shift.ExpectedCash,
            CountedCash = shift.CountedCash,
            CashOverShort = shift.ExpectedCash is not null && shift.CountedCash is not null
                ? shift.CountedCash - shift.ExpectedCash
                : null,
        };

    private static CashMovementDto MapMovement(CashMovement movement) =>
        new()
        {
            Id = movement.Id,
            MovementType = movement.MovementType,
            Amount = movement.Amount,
            Method = movement.Method,
            Description = movement.Description,
            PurchaseId = movement.PurchaseId,
            OccurredAtUtc = movement.OccurredAtUtc,
        };

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
