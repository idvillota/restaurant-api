using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Infrastructure.Planillas;

namespace Restaurant.Infrastructure.Services;

public sealed class PlanillaPdfService : IPlanillaPdfService
{
    static PlanillaPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IGeneratedFileStorage _fileStorage;

    public PlanillaPdfService(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        IGeneratedFileStorage fileStorage)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fileStorage = fileStorage;
    }

    public async Task<PlanillaGenerateResult> GenerateShiftPdfAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var shift = await _db.CashierShifts
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Cashier shift was not found.");

        if (shift.Status != CashierShiftStatus.Closed)
            throw new InvalidOperationException("Close the shift before generating the PDF.");

        var model = await BuildModelAsync(tenantId, shift.BusinessDate, shift.Id, cancellationToken);
        var path = await SavePdfAsync(
            tenantId,
            $"turno-{shift.BusinessDate:yyyy-MM-dd}-{shift.Id:N}.pdf",
            model,
            cancellationToken);

        shift.ClosurePdfRelativePath = path;
        await _db.SaveChangesAsync(cancellationToken);
        return ToResult(path, model);
    }

    public async Task<PlanillaGenerateResult> GenerateDailyPlanillaAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var closure = await _db.DailyClosures
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.BusinessDate == businessDate, cancellationToken)
            ?? throw new InvalidOperationException("Daily closure was not found.");

        if (closure.Status != DailyClosureStatus.Closed)
            throw new InvalidOperationException("Close the operational day before generating the Planilla.");

        var model = await BuildModelAsync(tenantId, businessDate, shiftId: null, cancellationToken);
        var path = await SavePdfAsync(
            tenantId,
            $"planilla-{businessDate:yyyy-MM-dd}.pdf",
            model,
            cancellationToken);

        closure.PlanillaPdfRelativePath = path;
        closure.PlanillaTotalSales = model.TotalSales;
        closure.PlanillaTotalEgresos = model.TotalEgresos;
        closure.PlanillaOverShort = model.OverShort;
        await _db.SaveChangesAsync(cancellationToken);
        return ToResult(path, model);
    }

    public async Task<IReadOnlyList<PlanillaListItemDto>> ListDailyPlanillasAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
            throw new InvalidOperationException("End date must be on or after start date.");

        var tenantId = ResolveTenantId();
        return await _db.DailyClosures
            .AsNoTracking()
            .Where(c =>
                c.TenantId == tenantId
                && c.Status == DailyClosureStatus.Closed
                && c.PlanillaPdfRelativePath != null
                && c.BusinessDate >= from
                && c.BusinessDate <= to)
            .OrderByDescending(c => c.BusinessDate)
            .Select(c => new PlanillaListItemDto
            {
                BusinessDate = c.BusinessDate,
                TotalSales = c.PlanillaTotalSales,
                TotalEgresos = c.PlanillaTotalEgresos,
                OverShort = c.PlanillaOverShort,
                HasPdf = true,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GeneratedFileContent?> OpenDailyPlanillaAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var closure = await _db.DailyClosures
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.BusinessDate == businessDate, cancellationToken);
        if (closure is null || closure.Status != DailyClosureStatus.Closed)
            return null;

        if (string.IsNullOrWhiteSpace(closure.PlanillaPdfRelativePath))
            await GenerateDailyPlanillaAsync(businessDate, cancellationToken);

        closure = await _db.DailyClosures
            .AsNoTracking()
            .FirstAsync(c => c.Id == closure.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(closure.PlanillaPdfRelativePath))
            return null;

        return await _fileStorage.OpenReadAsync(closure.PlanillaPdfRelativePath, cancellationToken);
    }

    public async Task<GeneratedFileContent?> OpenShiftPdfAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var shift = await _db.CashierShifts
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.TenantId == tenantId, cancellationToken);
        if (shift is null || shift.Status != CashierShiftStatus.Closed)
            return null;

        if (string.IsNullOrWhiteSpace(shift.ClosurePdfRelativePath))
            await GenerateShiftPdfAsync(shiftId, cancellationToken);

        shift = await _db.CashierShifts
            .AsNoTracking()
            .FirstAsync(s => s.Id == shiftId, cancellationToken);
        if (string.IsNullOrWhiteSpace(shift.ClosurePdfRelativePath))
            return null;

        return await _fileStorage.OpenReadAsync(shift.ClosurePdfRelativePath, cancellationToken);
    }

    private async Task<PlanillaDocumentModel> BuildModelAsync(
        Guid tenantId,
        DateOnly businessDate,
        Guid? shiftId,
        CancellationToken cancellationToken)
    {
        var tenantName = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var separateVitrina = await _db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.SeparateVitrinaCafeSales)
            .FirstOrDefaultAsync(cancellationToken);

        var dayNotes = await _db.DailyClosures.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.BusinessDate == businessDate)
            .Select(c => c.Notes)
            .FirstOrDefaultAsync(cancellationToken);

        var shiftQuery = _db.CashierShifts.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.BusinessDate == businessDate);
        if (shiftId is { } id)
            shiftQuery = shiftQuery.Where(s => s.Id == id);

        var shifts = await shiftQuery
            .OrderBy(s => s.OpenedAtUtc)
            .Select(s => new
            {
                s.Id,
                CashierEmail = s.CashierUser.Email,
                s.OpenedAtUtc,
                s.ClosedAtUtc,
                s.OpeningFloat,
                s.ExpectedCash,
                s.CountedCash,
                s.ClosingNotes,
            })
            .ToListAsync(cancellationToken);

        var shiftIds = shifts.Select(s => s.Id).ToList();
        var payments = shiftIds.Count == 0
            ? []
            : await _db.Payments.AsNoTracking()
                .Where(p =>
                    p.CashierShiftId != null
                    && p.BillId != null
                    && shiftIds.Contains(p.CashierShiftId.Value)
                    && p.Status == PaymentStatus.Completed)
                .Select(p => new
                {
                    ShiftId = p.CashierShiftId!.Value,
                    BillId = p.BillId!.Value,
                    p.Amount,
                    p.Method,
                })
                .ToListAsync(cancellationToken);

        var billIds = payments.Select(p => p.BillId).Distinct().ToList();
        var lines = billIds.Count == 0
            ? []
            : await _db.BillLines.AsNoTracking()
                .Where(l => billIds.Contains(l.BillId))
                .Select(l => new { l.BillId, l.ProductTypeName, l.Quantity, l.LineTotal })
                .ToListAsync(cancellationToken);

        var movements = shiftIds.Count == 0
            ? []
            : await _db.CashMovements.AsNoTracking()
                .Where(m =>
                    m.CashierShiftId != null
                    && shiftIds.Contains(m.CashierShiftId.Value)
                    && m.VoidedAtUtc == null
                    && (CashMovementKinds.OutflowTypes.Contains(m.MovementType)
                        || m.MovementType == CashMovementType.AdjustmentIn))
                .Select(m => new { ShiftId = m.CashierShiftId!.Value, m.MovementType, m.Amount, m.PurchaseId })
                .ToListAsync(cancellationToken);

        var paymentsByShift = payments.ToLookup(p => p.ShiftId);
        var linesByBill = lines.ToLookup(l => l.BillId);
        var outflowsByShift = movements
            .Where(m => CashMovementKinds.IsOutflow(m.MovementType))
            .ToLookup(o => o.ShiftId);
        var inflowsByShift = movements
            .Where(m => CashMovementKinds.IsInflow(m.MovementType))
            .ToLookup(i => i.ShiftId);

        var inputs = shifts.Select(shift =>
        {
            var shiftPayments = paymentsByShift[shift.Id].ToList();
            var shiftBillIds = shiftPayments.Select(p => p.BillId).Distinct().ToList();
            return new PlanillaShiftInput
            {
                CashierEmail = shift.CashierEmail,
                OpenedAtUtc = shift.OpenedAtUtc,
                ClosedAtUtc = shift.ClosedAtUtc,
                OpeningFloat = shift.OpeningFloat,
                ExpectedCash = shift.ExpectedCash,
                CountedCash = shift.CountedCash,
                Notes = shift.ClosingNotes,
                Payments = shiftPayments
                    .Select(p => new PlanillaPaymentInput
                    {
                        BillId = p.BillId,
                        Amount = p.Amount,
                        Method = p.Method,
                    })
                    .ToList(),
                Outflows = outflowsByShift[shift.Id]
                    .Select(o => new PlanillaOutflowInput
                    {
                        MovementType = o.MovementType,
                        Amount = o.Amount,
                        PurchaseId = o.PurchaseId,
                    })
                    .ToList(),
                Inflows = inflowsByShift[shift.Id]
                    .Select(i => new PlanillaInflowInput { Amount = i.Amount })
                    .ToList(),
                BillLines = shiftBillIds
                    .SelectMany(billId => linesByBill[billId])
                    .Select(l => new PlanillaBillLineInput
                    {
                        BillId = l.BillId,
                        ProductTypeName = l.ProductTypeName,
                        Quantity = l.Quantity,
                        LineTotal = l.LineTotal,
                    })
                    .ToList(),
            };
        }).ToList();

        return PlanillaDocumentFactory.Build(
            tenantName,
            businessDate,
            isDailyPlanilla: shiftId is null,
            separateVitrina,
            dayNotes,
            inputs);
    }

    private async Task<string> SavePdfAsync(
        Guid tenantId,
        string fileName,
        PlanillaDocumentModel model,
        CancellationToken cancellationToken)
    {
        var relativePath = $"Planillas/{tenantId:N}/{fileName}";
        return await _fileStorage.SaveAsync(
            relativePath,
            QuestPdfPlanillaDocument.BuildPdf(model),
            "application/pdf",
            cancellationToken);
    }

    private static PlanillaGenerateResult ToResult(string path, PlanillaDocumentModel model) =>
        new()
        {
            RelativePath = path,
            TotalSales = model.TotalSales,
            TotalEgresos = model.TotalEgresos,
            OverShort = model.OverShort,
        };

    private Guid ResolveTenantId()
    {
        if (_tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }
}
