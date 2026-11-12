using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Domain.Sales;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class CashierShiftService : ICashierShiftService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IOperationalBusinessDayService _operationalDay;
    private readonly IPlanillaPdfService _planillaPdf;

    public CashierShiftService(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        IOperationalBusinessDayService operationalDay,
        IPlanillaPdfService planillaPdf)
    {
        _db = db;
        _tenantContext = tenantContext;
        _operationalDay = operationalDay;
        _planillaPdf = planillaPdf;
    }

    public async Task<BusinessDayContextDto> GetBusinessDayContextAsync(CancellationToken cancellationToken = default)
    {
        var (tenantId, _, day) = await ResolveContextAsync(cancellationToken);
        var openShiftsCount = await _db.CashierShifts.CountAsync(
            s => s.TenantId == tenantId && s.BusinessDate == day.BusinessDate && s.Status == CashierShiftStatus.Open,
            cancellationToken);

        var myOpen = await GetMyOpenShiftAsync(cancellationToken);
        var requirePriorClose = await PriorOperationalDayCloseGate.IsEnabledAsync(
            _db, tenantId, cancellationToken);
        var pendingPrior = requirePriorClose
            ? await PriorOperationalDayCloseGate.FindBlockingDateAsync(
                _db, tenantId, day.BusinessDate, cancellationToken)
            : null;
        var pendingOpenShifts = pendingPrior is { } priorDate
            ? await PriorOperationalDayCloseGate.ListOpenShiftsAsync(
                _db, tenantId, priorDate, cancellationToken)
            : [];

        return new BusinessDayContextDto
        {
            BusinessDate = day.BusinessDate,
            ClockBusinessDate = day.ClockBusinessDate,
            IsAdvancedBeyondClock = day.IsAdvancedBeyondClock,
            OperationalDayCutoffHour = day.OperationalDayCutoffHour,
            DailyClosureStatus = day.ClosureStatus,
            OpenShiftsCount = openShiftsCount,
            MyOpenShift = myOpen,
            RequirePriorDayCloseBeforeNewShift = requirePriorClose,
            PendingPriorBusinessDate = pendingPrior,
            PendingPriorOpenShifts = pendingOpenShifts,
        };
    }

    public async Task<CashierShiftSummaryDto?> GetMyOpenShiftAsync(CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, day) = await ResolveContextAsync(cancellationToken);
        var shift = await _db.CashierShifts
            .AsNoTracking()
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(
                s =>
                    s.TenantId == tenantId &&
                    s.CashierUserId == userId &&
                    s.BusinessDate == day.BusinessDate &&
                    s.Status == CashierShiftStatus.Open,
                cancellationToken);

        return shift is null ? null : MapSummary(shift);
    }

    public async Task<CashierShiftSummaryDto> OpenShiftAsync(
        OpenCashierShiftDto dto,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, day) = await ResolveContextAsync(cancellationToken);
        EnsureDayOpen(day);

        var businessDate = day.BusinessDate;
        var blockingDate = await PriorOperationalDayCloseGate.FindBlockingDateAsync(
            _db, tenantId, businessDate, cancellationToken);
        if (blockingDate is { } prior)
            throw new InvalidOperationException(PriorOperationalDayCloseGate.BlockingMessage(prior));

        var existing = await _db.CashierShifts.AnyAsync(
            s =>
                s.TenantId == tenantId &&
                s.CashierUserId == userId &&
                s.BusinessDate == businessDate &&
                s.Status == CashierShiftStatus.Open,
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
        var (tenantId, userId, _) = await ResolveContextAsync(cancellationToken);
        var shift = await _db.CashierShifts
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Cashier shift was not found.");

        if (shift.Status != CashierShiftStatus.Open)
            throw new InvalidOperationException("This shift is already closed.");

        if (shift.CashierUserId != userId)
            throw new InvalidOperationException("Only the cashier who opened this shift can close it.");

        return await CompleteCloseShiftAsync(shift, dto.CountedCash, dto.ClosingNotes, cancellationToken);
    }

    public async Task<CashierShiftReportDto> ForceCloseShiftAsync(
        Guid shiftId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, _, _) = await ResolveContextAsync(cancellationToken);
        var shift = await _db.CashierShifts
            .Include(s => s.CashierUser)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Cashier shift was not found.");

        if (shift.Status != CashierShiftStatus.Open)
            throw new InvalidOperationException("This shift is already closed.");

        var report = await BuildShiftReportAsync(shift, cancellationToken);
        var expectedCash = ExpectedDrawerCash(report);
        var forceNote = string.IsNullOrWhiteSpace(notes)
            ? "Cierre forzado para permitir el cierre del día operativo."
            : notes.Trim();
        if (forceNote.Length > 500)
            forceNote = forceNote[..500];

        return await CompleteCloseShiftAsync(shift, expectedCash, forceNote, cancellationToken);
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
            .Take(businessDate is null ? 180 : 500)
            .ToListAsync(cancellationToken);

        return shifts.Select(s => MapSummary(s)).ToList();
    }

    public async Task<CashMovementDto> RecordCashMovementAsync(
        CreateCashMovementDto dto,
        bool canRecordCashAdjustment = false,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, day) = await ResolveContextAsync(cancellationToken);
        EnsureDayOpen(day);
        var businessDate = day.BusinessDate;

        if (dto.Amount <= 0)
            throw new InvalidOperationException("Amount must be greater than zero.");

        var isAdjustment = dto.MovementType is CashMovementType.AdjustmentIn or CashMovementType.AdjustmentOut;
        if (isAdjustment && !canRecordCashAdjustment)
            throw new InvalidOperationException(
                "Solo un usuario con cierre diario puede registrar un ajuste de caja.");

        if (dto.MovementType == CashMovementType.AdjustmentIn)
        {
            if (dto.PurchaseId is not null)
                throw new InvalidOperationException("Un ajuste de entrada no puede vincularse a una compra.");
            dto.ProviderId = null;
            dto.IsCashDrawerPayment = true;
        }

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
                s =>
                    s.TenantId == tenantId &&
                    s.CashierUserId == userId &&
                    s.BusinessDate == businessDate &&
                    s.Status == CashierShiftStatus.Open,
                cancellationToken);
        }

        if (dto.MovementType == CashMovementType.AdjustmentIn && shift is null)
            throw new InvalidOperationException(
                "Debe tener un turno abierto para registrar un ajuste de entrada.");

        if (dto.PurchaseId is { } purchaseId)
        {
            var purchaseExists = await _db.Purchases.AnyAsync(p => p.Id == purchaseId, cancellationToken);
            if (!purchaseExists)
                throw new InvalidOperationException("Purchase was not found.");
        }

        Guid? providerId = null;
        string? providerName = null;
        if (dto.MovementType == CashMovementType.ProviderPayment)
        {
            if (dto.ProviderId is not { } selectedProviderId)
                throw new InvalidOperationException("A provider is required for provider payments.");

            var provider = await _db.Providers.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == selectedProviderId && p.IsActive, cancellationToken);
            if (provider is null)
                throw new InvalidOperationException("Provider was not found or is inactive.");

            providerId = provider.Id;
            providerName = provider.Name;
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
            ProviderId = providerId,
            IsCashDrawerPayment = dto.IsCashDrawerPayment,
            CreatedByUserId = userId,
            OccurredAtUtc = DateTime.UtcNow,
        };

        await _db.CashMovements.AddAsync(movement, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return MapMovement(movement, providerName);
    }

    public async Task<CashMovementDto> VoidCashMovementAsync(
        Guid movementId,
        VoidCashMovementDto dto,
        bool canVoidClosedShift,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _) = await ResolveContextAsync(cancellationToken);
        var movement = await _db.CashMovements
            .Include(m => m.Provider)
            .FirstOrDefaultAsync(m => m.Id == movementId && m.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Cash movement was not found.");

        if (movement.VoidedAtUtc is not null)
            throw new InvalidOperationException("Este egreso ya está anulado.");

        var dayClosed = await _db.DailyClosures.AnyAsync(
            c =>
                c.TenantId == tenantId
                && c.BusinessDate == movement.BusinessDate
                && c.Status == DailyClosureStatus.Closed,
            cancellationToken);
        if (dayClosed)
            throw new InvalidOperationException(
                "No se puede anular un egreso de un día operativo cerrado.");

        CashierShift? shift = null;
        if (movement.CashierShiftId is { } shiftId)
        {
            shift = await _db.CashierShifts.FirstOrDefaultAsync(
                s => s.Id == shiftId && s.TenantId == tenantId,
                cancellationToken);
        }

        var shiftOpen = shift is { Status: CashierShiftStatus.Open };
        if (shiftOpen)
        {
            if (movement.CreatedByUserId != userId && !canVoidClosedShift)
                throw new InvalidOperationException(
                    "Solo quien registró el egreso puede anularlo mientras el turno está abierto.");
        }
        else if (!canVoidClosedShift)
        {
            throw new InvalidOperationException(
                "Solo un usuario con cierre diario puede anular un egreso de un turno cerrado.");
        }

        var reason = dto.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            reason = "Egreso duplicado o registrado por error.";
        if (reason.Length > 500)
            reason = reason[..500];

        movement.VoidedAtUtc = DateTime.UtcNow;
        movement.VoidedByUserId = userId;
        movement.VoidReason = reason;
        _db.CashMovements.Update(movement);
        await _db.SaveChangesAsync(cancellationToken);

        if (shift is { Status: CashierShiftStatus.Closed })
            await RecalculateClosedShiftExpectedCashAsync(shift, cancellationToken);

        return MapMovement(movement);
    }

    private async Task RecalculateClosedShiftExpectedCashAsync(
        CashierShift shift,
        CancellationToken cancellationToken)
    {
        var report = await BuildShiftReportAsync(shift, cancellationToken);
        shift.ExpectedCash = decimal.Round(ExpectedDrawerCash(report), 2, MidpointRounding.AwayFromZero);
        _db.CashierShifts.Update(shift);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _planillaPdf.GenerateShiftPdfAsync(shift.Id, cancellationToken);
        }
        catch
        {
            // Expected cash is already updated; PDF can be generated on download.
        }
    }

    public async Task<IReadOnlyList<CashierProviderOptionDto>> ListActiveProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        _ = ResolveTenantId();
        return await _db.Providers.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new CashierProviderOptionDto { Id = p.Id, Name = p.Name })
            .ToListAsync(cancellationToken);
    }

    public async Task<(Guid ShiftId, Guid UserId)> RequireOpenShiftAsync(CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, day) = await ResolveContextAsync(cancellationToken);
        EnsureDayOpen(day);

        var shift = await _db.CashierShifts.FirstOrDefaultAsync(
            s =>
                s.TenantId == tenantId &&
                s.CashierUserId == userId &&
                s.BusinessDate == day.BusinessDate &&
                s.Status == CashierShiftStatus.Open,
            cancellationToken);

        if (shift is null)
            throw new InvalidOperationException("Open a cashier shift before processing payments.");

        return (shift.Id, userId);
    }

    private async Task<CashierShiftReportDto> CompleteCloseShiftAsync(
        CashierShift shift,
        decimal countedCash,
        string? closingNotes,
        CancellationToken cancellationToken)
    {
        var report = await BuildShiftReportAsync(shift, cancellationToken);
        var expectedCash = ExpectedDrawerCash(report);

        shift.ExpectedCash = decimal.Round(expectedCash, 2, MidpointRounding.AwayFromZero);
        shift.CountedCash = decimal.Round(Math.Max(0, countedCash), 2, MidpointRounding.AwayFromZero);
        shift.ClosingNotes = closingNotes?.Trim();
        shift.ClosedAtUtc = DateTime.UtcNow;
        shift.Status = CashierShiftStatus.Closed;
        _db.CashierShifts.Update(shift);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _planillaPdf.GenerateShiftPdfAsync(shift.Id, cancellationToken);
            await _db.Entry(shift).ReloadAsync(cancellationToken);
        }
        catch
        {
            // Shift is already closed; PDF can be generated on download.
        }

        report.Shift = MapSummary(shift);
        report.Shift.CashOverShort = shift.CountedCash - shift.ExpectedCash;
        return report;
    }

    private static decimal ExpectedDrawerCash(CashierShiftReportDto report) =>
        report.Shift.OpeningFloat
        + report.TotalsByMethod.Where(t => t.Method == PaymentMethod.Cash).Sum(t => t.Total)
        - report.CashOutflowTotal
        + report.CashInflowTotal;

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
                    TableCodes = b.TableCodesSnapshot,
                    Amount = p.Amount,
                    Method = p.Method,
                    ExternalReference = p.ExternalReference,
                    PaidAtUtc = p.PaidAtUtc,
                })
            .OrderBy(p => p.PaidAtUtc)
            .ToListAsync(cancellationToken);

        await ShiftPaymentTableCodes.FillMissingAsync(_db, payments, cancellationToken);

        var movements = await _db.CashMovements
            .AsNoTracking()
            .Include(m => m.Provider)
            .Where(m =>
                m.CashierShiftId == shift.Id
                && (CashMovementKinds.OutflowTypes.Contains(m.MovementType)
                    || m.MovementType == CashMovementType.AdjustmentIn))
            .OrderBy(m => m.OccurredAtUtc)
            .ToListAsync(cancellationToken);
        var outflows = movements.Where(m => CashMovementKinds.IsOutflow(m.MovementType)).ToList();
        var inflows = movements.Where(m => CashMovementKinds.IsInflow(m.MovementType)).ToList();

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
        var collected = payments.Sum(p => p.Amount);
        var separateVitrina = await VitrinaCafeBillTotals.IsSeparateEnabledAsync(_db, cancellationToken);
        decimal totalSales;
        decimal vitrinaCafeSales;
        if (separateVitrina)
        {
            var vitrinaLineTotal = await VitrinaCafeBillTotals.SumForBillsAsync(
                _db,
                payments.Select(p => p.BillId).Distinct().ToList(),
                cancellationToken);
            (totalSales, vitrinaCafeSales) = VitrinaCafeProductType.SplitCollected(collected, vitrinaLineTotal);
        }
        else
        {
            totalSales = collected;
            vitrinaCafeSales = 0m;
        }
        var cashOutflowTotal = outflows
            .Where(m => m.IsCashDrawerPayment && m.VoidedAtUtc is null)
            .Sum(m => m.Amount);
        var cashInflowTotal = inflows
            .Where(m => m.IsCashDrawerPayment && m.VoidedAtUtc is null)
            .Sum(m => m.Amount);

        var summary = MapSummary(shift);
        if (summary.ExpectedCash is not null && summary.CountedCash is not null)
            summary.CashOverShort = summary.CountedCash - summary.ExpectedCash;

        return new CashierShiftReportDto
        {
            Shift = summary,
            TotalSales = totalSales,
            VitrinaCafeSales = vitrinaCafeSales,
            SeparateVitrinaCafeSales = separateVitrina,
            BillCount = billCount,
            TotalsByMethod = totalsByMethod,
            Payments = payments,
            CashOutflows = outflows.Select(m => MapMovement(m)).ToList(),
            CashOutflowTotal = cashOutflowTotal,
            CashInflows = inflows.Select(m => MapMovement(m)).ToList(),
            CashInflowTotal = cashInflowTotal,
        };
    }

    private static void EnsureDayOpen(EffectiveOperationalDay day)
    {
        if (day.ClosureStatus == DailyClosureStatus.Closed)
            throw new InvalidOperationException(
                "The operational day is closed. After daily closure, the system should advance to the next open day; refresh the page or contact a manager.");
    }

    private async Task<(Guid TenantId, Guid UserId, EffectiveOperationalDay Day)> ResolveContextAsync(
        CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var userId = ResolveUserId();
        var day = await _operationalDay.ResolveAsync(cancellationToken);
        return (tenantId, userId, day);
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
            HasClosurePdf = !string.IsNullOrWhiteSpace(shift.ClosurePdfRelativePath),
        };

    private static CashMovementDto MapMovement(CashMovement movement, string? providerName = null) =>
        new()
        {
            Id = movement.Id,
            MovementType = movement.MovementType,
            Amount = movement.Amount,
            Method = movement.Method,
            Description = movement.Description,
            PurchaseId = movement.PurchaseId,
            ProviderId = movement.ProviderId,
            ProviderName = providerName ?? movement.Provider?.Name,
            IsCashDrawerPayment = movement.IsCashDrawerPayment,
            OccurredAtUtc = movement.OccurredAtUtc,
            IsVoided = movement.VoidedAtUtc is not null,
            VoidedAtUtc = movement.VoidedAtUtc,
            VoidReason = movement.VoidReason,
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
