using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.PublicMenu;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class PublicMenuService : IPublicMenuService
{
    private readonly ApplicationDbContext _db;
    private readonly IProductImageStorage _productImages;

    public PublicMenuService(ApplicationDbContext db, IProductImageStorage productImages)
    {
        _db = db;
        _productImages = productImages;
    }

    public async Task<PublicMenuDto?> GetByTenantSlugAsync(string tenantSlug, CancellationToken cancellationToken = default)
    {
        var slug = tenantSlug.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(slug))
            return null;

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, cancellationToken);

        if (tenant is null)
            return null;

        var categories = await _db.ProductTypes.AsNoTracking()
            .Where(pt => pt.TenantId == tenant.Id && pt.IsActive)
            .OrderBy(pt => pt.SortOrder)
            .ThenBy(pt => pt.Name)
            .Select(pt => new PublicMenuCategoryDto
            {
                Id = pt.Id,
                Name = pt.Name,
                Description = pt.Description,
                SortOrder = pt.SortOrder,
            })
            .ToListAsync(cancellationToken);

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.TenantId == tenant.Id && p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.UnitPrice,
                p.ImagePath,
                p.ProductTypeId,
            })
            .ToListAsync(cancellationToken);

        var productsByCategory = products
            .GroupBy(p => p.ProductTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var category in categories)
        {
            if (!productsByCategory.TryGetValue(category.Id, out var categoryProducts))
            {
                category.Products = [];
                continue;
            }

            category.Products = categoryProducts
                .Select(p => new PublicMenuProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    UnitPrice = p.UnitPrice,
                    ImageUrl = _productImages.GetPublicUrl(p.ImagePath),
                })
                .ToList();
        }

        return new PublicMenuDto
        {
            TenantName = tenant.Name,
            TenantSlug = tenant.Slug,
            CurrencyCode = tenant.CurrencyCode,
            Categories = categories.Where(c => c.Products.Count > 0).ToList(),
        };
    }
}
