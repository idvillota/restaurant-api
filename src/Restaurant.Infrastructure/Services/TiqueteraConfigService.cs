using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.LunchTickets;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class TiqueteraConfigService(
    ApplicationDbContext db,
    ICurrentTenantContext tenantContext) : ITiqueteraConfigService
{
    public async Task<TiqueteraConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var enabled = await IsEnabledAsync(cancellationToken);
        var packs = await db.Products.AsNoTracking()
            .Include(p => p.ProductType)
            .Where(p => p.LunchTicketPackSize != null && p.LunchTicketPackSize > 0)
            .OrderBy(p => p.LunchTicketPackSize)
            .ThenBy(p => p.Name)
            .Select(p => MapPack(p))
            .ToListAsync(cancellationToken);

        var redeemable = await db.Products.AsNoTracking()
            .Where(p => p.IsLunchTicketRedeemable && p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new TiqueteraRedeemableProductDto
            {
                ProductId = p.Id,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
            })
            .ToListAsync(cancellationToken);

        return new TiqueteraConfigDto
        {
            TiqueterasEnabled = enabled,
            Packs = packs,
            RedeemableProducts = redeemable,
        };
    }

    public async Task<TiqueteraPackDto> CreatePackAsync(
        CreateTiqueteraPackDto dto,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        LunchTicketCheckoutRules.ValidateProductFlags(dto.LunchTicketPackSize, isLunchTicketRedeemable: false);

        if (!await db.ProductTypes.AnyAsync(t => t.Id == dto.ProductTypeId && t.IsActive, cancellationToken))
            throw new InvalidOperationException("La categoría del menú no existe o está inactiva.");

        var name = dto.Name.Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("El nombre es obligatorio.");

        if (await db.Products.AnyAsync(p => p.IsActive && p.Name == name, cancellationToken))
            throw new InvalidOperationException("Ya existe un producto activo con ese nombre.");

        var entity = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = ResolveTenantId(),
            ProductTypeId = dto.ProductTypeId,
            CompositionType = EProductType.Prepared,
            Name = name,
            UnitPrice = decimal.Round(dto.UnitPrice, 2, MidpointRounding.AwayFromZero),
            IsActive = true,
            LunchTicketPackSize = dto.LunchTicketPackSize,
            IsLunchTicketRedeemable = false,
        };

        await db.Products.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        entity = await db.Products.AsNoTracking()
            .Include(p => p.ProductType)
            .FirstAsync(p => p.Id == entity.Id, cancellationToken);
        return MapPack(entity);
    }

    public async Task<TiqueteraPackDto?> UpdatePackAsync(
        Guid productId,
        UpdateTiqueteraPackDto dto,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        LunchTicketCheckoutRules.ValidateProductFlags(dto.LunchTicketPackSize, isLunchTicketRedeemable: false);

        var entity = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (entity is null || entity.LunchTicketPackSize is null or <= 0)
            return null;

        if (!await db.ProductTypes.AnyAsync(t => t.Id == dto.ProductTypeId && t.IsActive, cancellationToken))
            throw new InvalidOperationException("La categoría del menú no existe o está inactiva.");

        var name = dto.Name.Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("El nombre es obligatorio.");

        if (await db.Products.AnyAsync(p => p.Id != productId && p.IsActive && p.Name == name, cancellationToken))
            throw new InvalidOperationException("Ya existe un producto activo con ese nombre.");

        entity.Name = name;
        entity.ProductTypeId = dto.ProductTypeId;
        entity.UnitPrice = decimal.Round(dto.UnitPrice, 2, MidpointRounding.AwayFromZero);
        entity.LunchTicketPackSize = dto.LunchTicketPackSize;
        entity.IsLunchTicketRedeemable = false;
        entity.IsActive = dto.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        entity = await db.Products.AsNoTracking()
            .Include(p => p.ProductType)
            .FirstAsync(p => p.Id == productId, cancellationToken);
        return MapPack(entity);
    }

    public async Task<bool> DeactivatePackAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        var entity = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (entity is null || entity.LunchTicketPackSize is null or <= 0)
            return false;

        entity.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TiqueteraConfigDto> SetRedeemableProductsAsync(
        SetTiqueteraRedeemableProductsDto dto,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        var ids = dto.ProductIds.Distinct().ToList();
        var products = await db.Products
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        if (ids.Count > 0)
        {
            var missing = ids.Where(id => products.All(p => p.Id != id)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException("Uno o más productos no existen o están inactivos.");

            var packsSelected = products
                .Where(p => ids.Contains(p.Id) && p.LunchTicketPackSize is > 0)
                .Select(p => p.Name)
                .ToList();
            if (packsSelected.Count > 0)
                throw new InvalidOperationException(
                    $"No puedes marcar un paquete como almuerzo redimible: {string.Join(", ", packsSelected)}.");
        }

        foreach (var product in products)
        {
            var shouldRedeem = ids.Contains(product.Id);
            if (product.IsLunchTicketRedeemable == shouldRedeem)
                continue;
            if (shouldRedeem && product.LunchTicketPackSize is > 0)
                continue;
            product.IsLunchTicketRedeemable = shouldRedeem;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetConfigAsync(cancellationToken);
    }

    private async Task<bool> IsEnabledAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        return await db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.TiqueterasEnabled)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task EnsureEnabledAsync(CancellationToken cancellationToken)
    {
        if (!await IsEnabledAsync(cancellationToken))
            throw new InvalidOperationException("Este local no tiene tiqueteras habilitadas.");
    }

    private Guid ResolveTenantId()
    {
        if (tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }

    private static TiqueteraPackDto MapPack(Product p) =>
        new()
        {
            ProductId = p.Id,
            Name = p.Name,
            ProductTypeId = p.ProductTypeId,
            ProductTypeName = p.ProductType?.Name ?? string.Empty,
            UnitPrice = p.UnitPrice,
            LunchTicketPackSize = p.LunchTicketPackSize ?? 0,
            IsActive = p.IsActive,
        };
}
