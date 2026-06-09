using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Persistence.Seeding;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class OperationalReportsServiceTests
{
    [Fact]
    public async Task GetSalesReport_filters_by_product_and_date()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();
        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        fx.TenantContext.TenantId = DevelopmentSeedIds.TenantId;

        var productId = DevelopmentSeedIds.ProductIds[0];
        var soldAt = DateTime.UtcNow.AddDays(-5);
        var orderId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        fx.Db.SalesOrders.Add(
            new SalesOrder
            {
                Id = orderId,
                TenantId = DevelopmentSeedIds.TenantId,
                Number = "TEST-SO-1",
                OpenedAtUtc = soldAt,
                Status = SalesOrderStatus.Paid,
                Subtotal = 32000m,
                TaxAmount = 0m,
                Total = 32000m,
                ClosedAtUtc = soldAt,
            });
        fx.Db.SalesOrderLines.Add(
            new SalesOrderLine
            {
                Id = lineId,
                TenantId = DevelopmentSeedIds.TenantId,
                SalesOrderId = orderId,
                ProductId = productId,
                Quantity = 2m,
                UnitPrice = 32000m,
                LineTotal = 64000m,
                SentToKitchenAtUtc = soldAt,
            });
        await fx.Db.SaveChangesAsync();

        fx.Db.Entry(fx.Db.SalesOrderLines.Single(l => l.Id == lineId)).Property(nameof(SalesOrderLine.CreatedAtUtc))
            .CurrentValue = soldAt;
        await fx.Db.SaveChangesAsync();

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var start = DateOnly.FromDateTime(soldAt.Date.AddDays(-1));
        var end = DateOnly.FromDateTime(soldAt.Date.AddDays(1));

        var allProducts = await sut.GetSalesReportAsync(start, end, productId: null);
        var oneProduct = await sut.GetSalesReportAsync(start, end, productId);

        Assert.Equal("Bistró Demo", allProducts.TenantName);
        Assert.Contains(allProducts.Rows, r => r.ProductName == "Pizza margarita" && r.Quantity == 2m);
        Assert.All(oneProduct.Rows, r => Assert.Equal("Pizza margarita", r.ProductName));
    }

    [Fact]
    public async Task GetIngredientsReport_returns_active_ingredients()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();
        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        fx.TenantContext.TenantId = DevelopmentSeedIds.TenantId;

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var report = await sut.GetIngredientsReportAsync(nameFilter: null);

        Assert.NotEmpty(report.Rows);
        Assert.Contains(report.Rows, r => r.IngredientName == "Tomates");
        Assert.Contains(report.Rows, r => r.IngredientName == "Tomates" && r.ReorderLevel == 5000m);
    }

    [Fact]
    public async Task GetPurchasesReport_returns_lines_in_range()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();
        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        fx.TenantContext.TenantId = DevelopmentSeedIds.TenantId;

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var end = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = end.AddDays(-60);
        var report = await sut.GetPurchasesReportAsync(start, end, null, null);

        Assert.NotEmpty(report.Rows);
        Assert.All(report.Rows, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.ProviderName));
            Assert.False(string.IsNullOrWhiteSpace(r.IngredientName));
        });
    }

    [Fact]
    public async Task GetDailySummaryReport_groups_sales_and_purchases_by_date()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();
        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        fx.TenantContext.TenantId = DevelopmentSeedIds.TenantId;

        var soldAt = DateTime.UtcNow.AddDays(-5);
        var orderId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        fx.Db.SalesOrders.Add(
            new SalesOrder
            {
                Id = orderId,
                TenantId = DevelopmentSeedIds.TenantId,
                Number = "TEST-SO-DS-1",
                OpenedAtUtc = soldAt,
                Status = SalesOrderStatus.Paid,
                Subtotal = 50000m,
                TaxAmount = 0m,
                Total = 50000m,
                ClosedAtUtc = soldAt,
            });
        fx.Db.SalesOrderLines.Add(
            new SalesOrderLine
            {
                Id = lineId,
                TenantId = DevelopmentSeedIds.TenantId,
                SalesOrderId = orderId,
                ProductId = DevelopmentSeedIds.ProductIds[0],
                Quantity = 1m,
                UnitPrice = 50000m,
                LineTotal = 50000m,
                SentToKitchenAtUtc = soldAt,
            });
        await fx.Db.SaveChangesAsync();

        fx.Db.Entry(fx.Db.SalesOrderLines.Single(l => l.Id == lineId)).Property(nameof(SalesOrderLine.CreatedAtUtc))
            .CurrentValue = soldAt;
        await fx.Db.SaveChangesAsync();

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var start = DateOnly.FromDateTime(soldAt.Date.AddDays(-1));
        var end = DateOnly.FromDateTime(soldAt.Date.AddDays(1));
        var report = await sut.GetDailySummaryReportAsync(start, end);

        Assert.Equal("Bistró Demo", report.TenantName);
        Assert.Contains(report.Rows, r =>
            r.Date == DateOnly.FromDateTime(soldAt)
            && r.TotalSales == 50000m
            && r.SalesOrderCount == 1
            && r.ItemsSold == 1m);
        Assert.True(report.GrandTotalSales >= 50000m);
    }
}
