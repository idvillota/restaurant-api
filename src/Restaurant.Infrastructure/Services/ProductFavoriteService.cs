using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.ProductFavorites;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class ProductFavoriteService : IProductFavoriteService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;

    public ProductFavoriteService(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ProductFavoriteListDto> ListAsync(CancellationToken cancellationToken = default)
    {
        var membershipId = await ResolveMembershipIdAsync(cancellationToken);
        await PruneInactiveAsync(membershipId, cancellationToken);
        return await LoadAsync(membershipId, cancellationToken);
    }

    public async Task<ProductFavoriteListDto> AddAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var membershipId = await ResolveMembershipIdAsync(cancellationToken);
        await PruneInactiveAsync(membershipId, cancellationToken);

        var exists = await _db.TenantUserProductFavorites
            .AnyAsync(f => f.TenantUserId == membershipId && f.ProductId == productId, cancellationToken);
        if (exists)
            return await LoadAsync(membershipId, cancellationToken);

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Product was not found.");

        var count = await _db.TenantUserProductFavorites
            .CountAsync(f => f.TenantUserId == membershipId, cancellationToken);
        if (count >= TenantUserProductFavorite.MaxPerMembership)
            throw new InvalidOperationException("Maximum of 10 favorite products.");

        var nextOrder = count == 0
            ? 0
            : await _db.TenantUserProductFavorites
                .Where(f => f.TenantUserId == membershipId)
                .MaxAsync(f => f.SortOrder, cancellationToken) + 1;

        _db.TenantUserProductFavorites.Add(new TenantUserProductFavorite
        {
            Id = Guid.NewGuid(),
            TenantUserId = membershipId,
            ProductId = product.Id,
            SortOrder = nextOrder,
        });
        await _db.SaveChangesAsync(cancellationToken);
        return await LoadAsync(membershipId, cancellationToken);
    }

    public async Task<ProductFavoriteListDto> RemoveAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var membershipId = await ResolveMembershipIdAsync(cancellationToken);
        var row = await _db.TenantUserProductFavorites
            .FirstOrDefaultAsync(f => f.TenantUserId == membershipId && f.ProductId == productId, cancellationToken);
        if (row is not null)
        {
            _db.TenantUserProductFavorites.Remove(row);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await PruneInactiveAsync(membershipId, cancellationToken);
        return await LoadAsync(membershipId, cancellationToken);
    }

    private async Task<Guid> ResolveMembershipIdAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.TenantId.HasValue || !_tenantContext.UserId.HasValue)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var membershipId = await _db.TenantUsers
            .Where(tu => tu.TenantId == _tenantContext.TenantId.Value
                && tu.UserId == _tenantContext.UserId.Value
                && tu.IsActive)
            .Select(tu => tu.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (membershipId == Guid.Empty)
            throw new UnauthorizedAccessException("Active tenant membership not found.");

        return membershipId;
    }

    private async Task PruneInactiveAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        var stale = await _db.TenantUserProductFavorites
            .Where(f => f.TenantUserId == membershipId)
            .Where(f => !_db.Products.Any(p => p.Id == f.ProductId && p.IsActive))
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
            return;

        _db.TenantUserProductFavorites.RemoveRange(stale);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProductFavoriteListDto> LoadAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        var productIds = await _db.TenantUserProductFavorites
            .AsNoTracking()
            .Where(f => f.TenantUserId == membershipId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.CreatedAtUtc)
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);

        return new ProductFavoriteListDto
        {
            ProductIds = productIds,
            MaxCount = TenantUserProductFavorite.MaxPerMembership,
        };
    }
}
