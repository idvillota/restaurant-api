using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.Bills;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class TenantSettingsService : ITenantSettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;

    public TenantSettingsService(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
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
        settings.DianResolutionNumber = dto.DianResolutionNumber?.Trim();
        settings.DianResolutionFrom = dto.DianResolutionFrom;
        settings.DianResolutionTo = dto.DianResolutionTo;
        settings.InvoiceNumberPrefix = dto.InvoiceNumberPrefix?.Trim();
        settings.ImpoconsumoPercent = dto.ImpoconsumoPercent;

        await SyncDianNextConsecutiveAsync(settings, cancellationToken);

        _db.TenantSettings.Update(settings);
        await _db.SaveChangesAsync(cancellationToken);
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

    private static TenantSettingsDto Map(TenantSettings settings) =>
        new()
        {
            MaxDiscountPercent = settings.MaxDiscountPercent,
            OperationalDayCutoffHour = settings.OperationalDayCutoffHour,
            TradeName = settings.TradeName,
            LegalName = settings.LegalName,
            TaxRegime = settings.TaxRegime,
            TaxId = settings.TaxId,
            LegalRepresentative = settings.LegalRepresentative,
            AddressLine = settings.AddressLine,
            City = settings.City,
            Country = settings.Country,
            PostalCode = settings.PostalCode,
            Phone = settings.Phone,
            DianResolutionNumber = settings.DianResolutionNumber,
            DianResolutionFrom = settings.DianResolutionFrom,
            DianResolutionTo = settings.DianResolutionTo,
            DianNextConsecutive = settings.DianNextConsecutive,
            InvoiceNumberPrefix = settings.InvoiceNumberPrefix,
            ImpoconsumoPercent = settings.ImpoconsumoPercent,
        };
}
