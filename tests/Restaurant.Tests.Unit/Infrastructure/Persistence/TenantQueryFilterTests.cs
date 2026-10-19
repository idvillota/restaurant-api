using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Persistence;

public sealed class TenantQueryFilterTests
{
    [Fact]
    public async Task Products_query_returns_empty_when_tenant_context_is_null()
    {
        using var fx = new NoTenantDbFixture();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var typeA = Guid.NewGuid();
        var typeB = Guid.NewGuid();

        fx.Db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "A", Slug = "a", IsActive = true },
            new Tenant { Id = tenantB, Name = "B", Slug = "b", IsActive = true });
        fx.Db.ProductTypes.AddRange(
            new ProductType { Id = typeA, TenantId = tenantA, Name = "Food A", IsActive = true },
            new ProductType { Id = typeB, TenantId = tenantB, Name = "Food B", IsActive = true });
        fx.Db.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA,
                ProductTypeId = typeA,
                Name = "Product A",
                CompositionType = EProductType.Prepared,
                UnitPrice = 1m,
                IsActive = true,
            },
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB,
                ProductTypeId = typeB,
                Name = "Product B",
                CompositionType = EProductType.Prepared,
                UnitPrice = 2m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        Assert.Null(fx.TenantContext.TenantId);

        var visible = await fx.Db.Products.AsNoTracking().ToListAsync();
        Assert.Empty(visible);

        var all = await fx.Db.Products.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Products_query_scopes_to_current_tenant_only()
    {
        using var fx = new TenantDbFixture();
        var otherTenant = Guid.NewGuid();
        var typeMine = Guid.NewGuid();
        var typeOther = Guid.NewGuid();
        var mineProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();

        fx.Db.Tenants.Add(new Tenant { Id = otherTenant, Name = "Other", Slug = "other", IsActive = true });
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeMine, TenantId = fx.TenantId, Name = "Mine", IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = mineProductId,
                TenantId = fx.TenantId,
                ProductTypeId = typeMine,
                Name = "Mine product",
                CompositionType = EProductType.Prepared,
                UnitPrice = 1m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        // SaveChanges stamps TenantId from context on Added rows — switch context to seed another tenant.
        fx.TenantContext.TenantId = otherTenant;
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeOther, TenantId = otherTenant, Name = "Theirs", IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = otherProductId,
                TenantId = otherTenant,
                ProductTypeId = typeOther,
                Name = "Other product",
                CompositionType = EProductType.Prepared,
                UnitPrice = 2m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        fx.TenantContext.TenantId = fx.TenantId;

        var visible = await fx.Db.Products.AsNoTracking().Select(p => p.Name).ToListAsync();
        Assert.Single(visible);
        Assert.Equal("Mine product", visible[0]);

        var all = await fx.Db.Products.IgnoreQueryFilters().AsNoTracking().CountAsync();
        Assert.Equal(2, all);
    }
}
