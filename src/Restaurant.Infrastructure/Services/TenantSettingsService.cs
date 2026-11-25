using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.Bills;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class TenantSettingsService : ITenantSettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IProductImageStorage _mediaStorage;
    private readonly IProductService _products;

    public TenantSettingsService(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        IProductImageStorage mediaStorage,
        IProductService products)
    {
        _db = db;
        _tenantContext = tenantContext;
        _mediaStorage = mediaStorage;
        _products = products;
    }

    public async Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        var synced = await SyncDianNextConsecutiveAsync(settings, cancellationToken);
        if (synced)
            await _db.SaveChangesAsync(cancellationToken);
        return Map(settings);
    }

    public async Task<TenantSettingsDto> UpdateAsync(
        UpdateTenantSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.DianResolutionTo < dto.DianResolutionFrom)
            throw new InvalidOperationException("El rango DIAN «hasta» debe ser mayor o igual que «desde».");

        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        settings.MaxDiscountPercent = dto.MaxDiscountPercent;
        settings.OperationalDayCutoffHour = dto.OperationalDayCutoffHour;
        settings.TradeName = dto.TradeName.Trim();
        settings.LegalName = dto.LegalName.Trim();
        settings.TaxRegime = dto.TaxRegime.Trim();
        settings.TaxId = dto.TaxId.Trim();
        settings.LegalRepresentative = dto.LegalRepresentative?.Trim();
        settings.AddressLine = dto.AddressLine.Trim();
        settings.City = dto.City.Trim();
        settings.Country = dto.Country.Trim();
        settings.PostalCode = dto.PostalCode?.Trim();
        settings.Phone = dto.Phone?.Trim();
        settings.PrinterMac = string.IsNullOrWhiteSpace(dto.PrinterMac) ? null : dto.PrinterMac.Trim();
        settings.DianResolutionNumber = dto.DianResolutionNumber?.Trim();
        settings.DianResolutionFrom = dto.DianResolutionFrom;
        settings.DianResolutionTo = dto.DianResolutionTo;
        settings.InvoiceNumberPrefix = dto.InvoiceNumberPrefix?.Trim();
        settings.ImpoconsumoPercent = dto.ImpoconsumoPercent;
        settings.ShowOperationalSalesPanel = dto.ShowOperationalSalesPanel;
        StockControlHelper.ApplyToSettings(settings, dto.StockControlMode, dto.InventoryEnabled);
        settings.AutoGeneratePurchaseBillNumbers = dto.AutoGeneratePurchaseBillNumbers;
        settings.KitchenTicketPrintMode = NormalizePrintMode(dto.KitchenTicketPrintMode);
        settings.ThermalPaperWidthMm = ThermalPaperWidth.Normalize(dto.ThermalPaperWidthMm);
        settings.SeparateVitrinaCafeSales = dto.SeparateVitrinaCafeSales;
        settings.RequirePriorDayCloseBeforeNewShift = dto.RequirePriorDayCloseBeforeNewShift;
        if (settings.TiqueterasEnabled && !dto.TiqueterasEnabled)
        {
            var remaining = await _db.LunchTicketBooklets
                .AsNoTracking()
                .SumAsync(b => (int?)b.RemainingTickets, cancellationToken) ?? 0;
            if (remaining > 0)
                throw new InvalidOperationException(
                    $"No se pueden desactivar las tiqueteras mientras haya cupos pendientes ({remaining}).");
        }

        settings.TiqueterasEnabled = dto.TiqueterasEnabled;
        settings.BarcodesEnabled = dto.BarcodesEnabled;
        ApplyBreBKey(settings, dto);

        await SyncDianNextConsecutiveAsync(settings, cancellationToken);

        _db.TenantSettings.Update(settings);
        await _db.SaveChangesAsync(cancellationToken);

        // When barcodes are on, fill only products without a code (never overwrite stickers).
        var assigned = dto.BarcodesEnabled
            ? await _products.BackfillMissingBarcodesAsync(cancellationToken)
            : 0;

        var mapped = Map(settings);
        mapped.ProductBarcodesAssigned = assigned;
        return mapped;
    }

    public async Task<TenantSettingsDto> SetLogoAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        var tenantId = ResolveTenantId();
        var previousPath = settings.LogoPath;

        var relativePath = await _mediaStorage.SaveTenantLogoAsync(
            tenantId,
            content,
            fileName,
            cancellationToken);

        settings.LogoPath = relativePath;
        _db.TenantSettings.Update(settings);
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previousPath, relativePath, StringComparison.OrdinalIgnoreCase))
            await _mediaStorage.DeleteIfExistsAsync(previousPath, cancellationToken);

        return Map(settings);
    }

    public async Task<TenantSettingsDto> RemoveLogoAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        var previousPath = settings.LogoPath;
        if (string.IsNullOrWhiteSpace(previousPath))
            return Map(settings);

        settings.LogoPath = null;
        _db.TenantSettings.Update(settings);
        await _db.SaveChangesAsync(cancellationToken);
        await _mediaStorage.DeleteIfExistsAsync(previousPath, cancellationToken);
        return Map(settings);
    }

    private async Task<TenantSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (settings is not null)
            return settings;

        settings = new TenantSettings
        {
            TenantId = tenantId,
            MaxDiscountPercent = 10m,
            OperationalDayCutoffHour = 4,
            ImpoconsumoPercent = 8m,
            TaxRegime = "Régimen Simplificado",
            Country = "Colombia",
            ShowOperationalSalesPanel = true,
            InventoryEnabled = true,
            StockControlMode = StockControlMode.Ingredients,
            AutoGeneratePurchaseBillNumbers = false,
            KitchenTicketPrintMode = KitchenTicketPrintMode.Always,
            ThermalPaperWidthMm = ThermalPaperWidthMm.Mm58,
            SeparateVitrinaCafeSales = false,
            BarcodesEnabled = true,
        };
        await _db.TenantSettings.AddAsync(settings, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private Guid ResolveTenantId()
    {
        if (_tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }

    private async Task<bool> SyncDianNextConsecutiveAsync(
        TenantSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.DianResolutionFrom <= 0)
            return false;

        var maxUsed = await _db.Bills
            .Where(b => b.DianConsecutiveNumber > 0)
            .MaxAsync(b => (int?)b.DianConsecutiveNumber, cancellationToken) ?? 0;

        var synced = Math.Max(settings.DianResolutionFrom, maxUsed + 1);
        if (settings.DianNextConsecutive == synced)
            return false;

        settings.DianNextConsecutive = synced;
        return true;
    }

    private TenantSettingsDto Map(TenantSettings settings) =>
        new()
        {
            MaxDiscountPercent = settings.MaxDiscountPercent,
            OperationalDayCutoffHour = settings.OperationalDayCutoffHour,
            TradeName = settings.TradeName,
            LegalName = settings.LegalName,
            LogoUrl = _mediaStorage.GetPublicUrl(settings.LogoPath),
            TaxRegime = settings.TaxRegime,
            TaxId = settings.TaxId,
            LegalRepresentative = settings.LegalRepresentative,
            AddressLine = settings.AddressLine,
            City = settings.City,
            Country = settings.Country,
            PostalCode = settings.PostalCode,
            Phone = settings.Phone,
            PrinterMac = settings.PrinterMac,
            DianResolutionNumber = settings.DianResolutionNumber,
            DianResolutionFrom = settings.DianResolutionFrom,
            DianResolutionTo = settings.DianResolutionTo,
            DianNextConsecutive = settings.DianNextConsecutive,
            InvoiceNumberPrefix = settings.InvoiceNumberPrefix,
            ImpoconsumoPercent = settings.ImpoconsumoPercent,
            ShowOperationalSalesPanel = settings.ShowOperationalSalesPanel,
            InventoryEnabled = settings.InventoryEnabled,
            StockControlMode = (int)settings.StockControlMode,
            AutoGeneratePurchaseBillNumbers = settings.AutoGeneratePurchaseBillNumbers,
            KitchenTicketPrintMode = (int)settings.KitchenTicketPrintMode,
            ThermalPaperWidthMm = (int)ThermalPaperWidth.Normalize((int)settings.ThermalPaperWidthMm),
            SeparateVitrinaCafeSales = settings.SeparateVitrinaCafeSales,
            RequirePriorDayCloseBeforeNewShift = settings.RequirePriorDayCloseBeforeNewShift,
            TiqueterasEnabled = settings.TiqueterasEnabled,
            BarcodesEnabled = settings.BarcodesEnabled,
            BreBKey = settings.BreBKey,
            BreBKeyType = settings.BreBKeyType is { } keyType ? (int)keyType : null,
        };

    private static void ApplyBreBKey(TenantSettings settings, UpdateTenantSettingsDto dto)
    {
        var key = string.IsNullOrWhiteSpace(dto.BreBKey) ? null : dto.BreBKey.Trim();
        if (key is null)
        {
            settings.BreBKey = null;
            settings.BreBKeyType = null;
            return;
        }

        if (dto.BreBKeyType is not { } rawType || !Enum.IsDefined(typeof(BreBKeyType), rawType))
            throw new InvalidOperationException("Elige el tipo de la llave Bre-B.");

        settings.BreBKey = key;
        settings.BreBKeyType = (BreBKeyType)rawType;
    }

    private static KitchenTicketPrintMode NormalizePrintMode(int raw) =>
        Enum.IsDefined(typeof(KitchenTicketPrintMode), raw)
            ? (KitchenTicketPrintMode)raw
            : KitchenTicketPrintMode.Always;
}
