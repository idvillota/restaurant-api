using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Persistence.Seeding;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Persistence;

public sealed class DevelopmentSeedTests
{
    [Fact]
    public void DevelopmentSeedIds_all_guids_are_valid()
    {
        _ = DevelopmentSeedIds.TenantId;
        Assert.Equal(10, DevelopmentSeedIds.IngredientCategoryIds.Length);
        Assert.Equal(12, DevelopmentSeedIds.IngredientIds.Length);
        Assert.Equal(10, DevelopmentSeedIds.ProviderIds.Length);
        Assert.Equal(12, DevelopmentSeedIds.ProductIds.Length);
    }

    [Fact]
    public async Task SeedAsync_populates_demo_tenant_with_related_data()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();

        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);

        var tenant = await fx.Db.Tenants.IgnoreQueryFilters()
            .SingleAsync(t => t.Slug == DevelopmentSeedIds.TenantSlug);
        Assert.Equal("Bistró Demo", tenant.Name);
        Assert.Equal("COP", tenant.CurrencyCode);

        Assert.Equal(12, await fx.Db.Ingredients.IgnoreQueryFilters().CountAsync());
        var tomates = await fx.Db.Ingredients.IgnoreQueryFilters()
            .SingleAsync(i => i.Name == "Tomates");
        Assert.Equal(IngredientUnit.Gram, tomates.Unit);
        Assert.Equal(5000m, tomates.ReorderLevel);

        var aceite = await fx.Db.Ingredients.IgnoreQueryFilters()
            .SingleAsync(i => i.Name == "Aceite de oliva");
        Assert.Equal(IngredientUnit.Milliliter, aceite.Unit);

        var demoUser = await fx.Db.Users.IgnoreQueryFilters()
            .SingleAsync(u => u.Email == DevelopmentSeedIds.DemoTestEmail);
        Assert.True(
            await fx.Db.TenantUsers.IgnoreQueryFilters().AnyAsync(
                tu => tu.UserId == demoUser.Id && tu.TenantId == DevelopmentSeedIds.TenantId));
        Assert.Equal(10, await fx.Db.Purchases.IgnoreQueryFilters().CountAsync());
        Assert.True(await fx.Db.Ingredients.IgnoreQueryFilters().AnyAsync(i => i.StockQuantity > 0 && i.UnitCost > 0));

        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        Assert.Equal(1, await fx.Db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Slug == DevelopmentSeedIds.TenantSlug));
    }
}
