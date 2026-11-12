using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common;
using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Domain.Sales;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class DailyClosureService : IDailyClosureService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IPlanillaPdfService _planillaPdf;

    public DailyClosureService(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        IPlanillaPdfService planillaPdf)
    {
        _db = db;
        _tenantContext = tenantContext;
        _planillaPdf = planillaPdf;
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

        var nextBusinessDate = businessDate.AddDays(1);
        var settings = await GetOrCreateTenantSettingsAsync(tenantId, cancellationToken);
        settings.ActiveOperationalBusinessDate = nextBusinessDate;
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _planillaPdf.GenerateDailyPlanillaAsync(businessDate, cancellationToken);
        }
        catch
        {
            // Day is already closed; PDF can be generated on download.
        }

        var report = await BuildDailyReportAsync(tenantId, businessDate, cancellationToken);
        report.NextOperationalBusinessDate = nextBusinessDate;
        return report;
    }

    private async Task<TenantSettings> GetOrCreateTenantSettingsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (settings is not null)
            return settings;

        settings = new TenantSettings
        {
            TenantId = tenantId,
            MaxDiscountPercent = 10m,
            OperationalDayCutoffHour = 4,
        };
        await _db.TenantSettings.AddAsync(settings, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<IReadOnlyList<DailyClosureSummaryDto>> ListClosuresAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        return await _db.DailyClosures
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.BusinessDate)
            .Take(90)
            .Select(c => new DailyClosureSummaryDto
            {
                BusinessDate = c.BusinessDate,
                Status = c.Status,
                ClosedAtUtc = c.ClosedAtUtc,
                ClosedByEmail = c.ClosedByUser != null ? c.ClosedByUser.Email : null,
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<DailyClosureReportDto> BuildDailyReportAsync(
        Guid tenantId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var closureSummary = await _db.DailyClosures
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.BusinessDate == businessDate)
            .Select(c => new DailyClosureSummaryDto
            {
                BusinessDate = businessDate,
                Status = c.Status,
                ClosedAtUtc = c.ClosedAtUtc,
                ClosedByEmail = c.ClosedByUser != null ? c.ClosedByUser.Email : null,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? new DailyClosureSummaryDto
            {
                BusinessDate = businessDate,
                Status = DailyClosureStatus.Open,
            };

        var shifts = await _db.CashierShifts
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.BusinessDate == businessDate)
            .OrderBy(s => s.OpenedAtUtc)
            .Select(s => new
            {
                s.Id,
                s.OpenedAtUtc,
                s.ClosedAtUtc,
                s.Status,
                s.CountedCash,
                s.ExpectedCash,
                CashierEmail = s.CashierUser.Email,
            })
            .ToListAsync(cancellationToken);

        var shiftIds = shifts.Select(s => s.Id).ToList();

        var paymentRows = shiftIds.Count == 0
            ? []
            : await _db.Payments
                .AsNoTracking()
                .Where(p =>
                    p.CashierShiftId != null
                    && shiftIds.Contains(p.CashierShiftId.Value)
                    && p.Status == PaymentStatus.Completed)
                .Join(
                    _db.Bills.AsNoTracking(),
                    p => p.BillId,
                    b => b.Id,
                    (p, b) => new
                    {
                        p.Id,
                        ShiftId = p.CashierShiftId!.Value,
                        BillId = b.Id,
                        BillNumber = b.Number,
                        TableCodes = b.TableCodesSnapshot,
                        p.Amount,
                        p.Method,
                        p.ExternalReference,
                        p.PaidAtUtc,
                    })
                .OrderBy(p => p.PaidAtUtc)
                .ToListAsync(cancellationToken);

        var payments = paymentRows
            .Select(p => new ShiftPaymentLineDto
            {
                PaymentId = p.Id,
                BillId = p.BillId,
                BillNumber = p.BillNumber,
                TableCodes = p.TableCodes,
                Amount = p.Amount,
                Method = p.Method,
                ExternalReference = p.ExternalReference,
                PaidAtUtc = p.PaidAtUtc,
            })
            .ToList();

        await ShiftPaymentTableCodes.FillMissingAsync(_db, payments, cancellationToken);

        var movements = await _db.CashMovements
            .AsNoTracking()
            .Include(m => m.Provider)
            .Where(m =>
                m.TenantId == tenantId
                && m.BusinessDate == businessDate
                && (CashMovementKinds.OutflowTypes.Contains(m.MovementType)
                    || m.MovementType == CashMovementType.AdjustmentIn))
            .OrderBy(m => m.OccurredAtUtc)
            .ToListAsync(cancellationToken);
        var outflows = movements.Where(m => CashMovementKinds.IsOutflow(m.MovementType)).ToList();
        var inflows = movements.Where(m => CashMovementKinds.IsInflow(m.MovementType)).ToList();

        var paymentsByShift = paymentRows.ToLookup(p => p.ShiftId);
        var separateVitrina = await VitrinaCafeBillTotals.IsSeparateEnabledAsync(_db, cancellationToken);
        var vitrinaByBill = separateVitrina
            ? await VitrinaCafeBillTotals.SumByBillAsync(
                _db,
                paymentRows.Select(p => p.BillId).Distinct().ToList(),
                cancellationToken)
            : [];

        var shiftRollups = shifts.Select(shift =>
        {
            var shiftPayments = paymentsByShift[shift.Id].ToList();
            var collected = shiftPayments.Sum(p => p.Amount);
            decimal generalSales;
            decimal vitrinaSales;
            if (separateVitrina)
            {
                var vitrina = shiftPayments
                    .Select(p => p.BillId)
                    .Distinct()
                    .Sum(billId => vitrinaByBill.GetValueOrDefault(billId));
                (generalSales, vitrinaSales) = VitrinaCafeProductType.SplitCollected(collected, vitrina);
            }
            else
            {
                generalSales = collected;
                vitrinaSales = 0m;
            }
            return new DailyShiftRollupDto
            {
                ShiftId = shift.Id,
                CashierEmail = shift.CashierEmail,
                OpenedAtUtc = shift.OpenedAtUtc,
                ClosedAtUtc = shift.ClosedAtUtc,
                Status = shift.Status,
                TotalSales = generalSales,
                VitrinaCafeSales = vitrinaSales,
                CashSales = shiftPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount),
                CardSales = shiftPayments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount),
                TransferSales = shiftPayments.Where(p => p.Method == PaymentMethod.Transfer).Sum(p => p.Amount),
                CountedCash = shift.CountedCash,
                CashOverShort = shift.CountedCash - shift.ExpectedCash,
            };
        }).ToList();

        var totalCash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var totalCard = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount);
        var totalTransfer = payments.Where(p => p.Method == PaymentMethod.Transfer).Sum(p => p.Amount);
        var totalOther = payments.Where(p => p.Method == PaymentMethod.Other).Sum(p => p.Amount);
        var totalOutflows = outflows
            .Where(m => m.IsCashDrawerPayment && m.VoidedAtUtc is null)
            .Sum(m => m.Amount);
        var totalInflows = inflows
            .Where(m => m.IsCashDrawerPayment && m.VoidedAtUtc is null)
            .Sum(m => m.Amount);
        var collected = payments.Sum(p => p.Amount);
        decimal totalSales;
        decimal vitrinaCafeSales;
        if (separateVitrina)
            (totalSales, vitrinaCafeSales) = VitrinaCafeProductType.SplitCollected(
                collected,
                vitrinaByBill.Values.Sum());
        else
        {
            totalSales = collected;
            vitrinaCafeSales = 0m;
        }

        return new DailyClosureReportDto
        {
            Closure = closureSummary,
            TotalSales = totalSales,
            VitrinaCafeSales = vitrinaCafeSales,
            SeparateVitrinaCafeSales = separateVitrina,
            BillCount = payments.Select(p => p.BillId).Distinct().Count(),
            TotalCashIn = totalCash,
            TotalCard = totalCard,
            TotalTransfer = totalTransfer,
            TotalOther = totalOther,
            TotalCashOutflows = totalOutflows,
            TotalCashInflows = totalInflows,
            NetCash = totalCash + totalInflows - totalOutflows,
            Shifts = shiftRollups,
            CashOutflows = outflows.Select(MapMovement).ToList(),
            CashInflows = inflows.Select(MapMovement).ToList(),
            Payments = payments,
        };
    }

    private static CashMovementDto MapMovement(CashMovement m) =>
        new()
        {
            Id = m.Id,
            MovementType = m.MovementType,
            Amount = m.Amount,
            Method = m.Method,
            Description = m.Description,
            PurchaseId = m.PurchaseId,
            ProviderId = m.ProviderId,
            ProviderName = m.Provider?.Name,
            IsCashDrawerPayment = m.IsCashDrawerPayment,
            OccurredAtUtc = m.OccurredAtUtc,
            IsVoided = m.VoidedAtUtc is not null,
            VoidedAtUtc = m.VoidedAtUtc,
            VoidReason = m.VoidReason,
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
