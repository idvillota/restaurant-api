using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Persistence.Seeding;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Persistence;

public sealed class DevelopmentHistoricalSeedTests
{
    [Fact]
    public async Task SeedAsync_generates_six_months_of_historical_data_once()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();

        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        await DevelopmentHistoricalDataSeeder.SeedAsync(fx.Db, NullLogger.Instance, fx.TenantContext);

        var historicalOrders = await fx.Db.SalesOrders.IgnoreQueryFilters()
            .CountAsync(s =>
                s.TenantId == DevelopmentSeedIds.TenantId
                && s.Number.StartsWith(DevelopmentHistoricalDataSeeder.HistoricalOrderNumberPrefix));
        var historicalPurchases = await fx.Db.Purchases.IgnoreQueryFilters()
            .CountAsync(p =>
                p.TenantId == DevelopmentSeedIds.TenantId
                && p.BillNumber.StartsWith("HIST-P-"));
        var ingredientCount = await fx.Db.Ingredients.IgnoreQueryFilters()
            .CountAsync(i => i.TenantId == DevelopmentSeedIds.TenantId);
        var productCount = await fx.Db.Products.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == DevelopmentSeedIds.TenantId);

        Assert.True(historicalOrders >= 1500);
        Assert.True(historicalPurchases >= 40);
        Assert.True(ingredientCount >= 30);
        Assert.True(productCount >= 25);

        var oldest = await fx.Db.SalesOrders.IgnoreQueryFilters()
            .Where(s => s.TenantId == DevelopmentSeedIds.TenantId && s.Number.StartsWith(DevelopmentHistoricalDataSeeder.HistoricalOrderNumberPrefix))
            .MinAsync(s => s.ClosedAtUtc);
        Assert.True(oldest <= DateTime.UtcNow.AddMonths(-5));

        await DevelopmentHistoricalDataSeeder.SeedAsync(fx.Db, NullLogger.Instance, fx.TenantContext);
        var afterSecondRun = await fx.Db.SalesOrders.IgnoreQueryFilters()
            .CountAsync(s =>
                s.TenantId == DevelopmentSeedIds.TenantId
                && s.Number.StartsWith(DevelopmentHistoricalDataSeeder.HistoricalOrderNumberPrefix));
        Assert.Equal(historicalOrders, afterSecondRun);
    }
}
