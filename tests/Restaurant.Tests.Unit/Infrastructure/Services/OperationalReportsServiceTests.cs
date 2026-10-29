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
        Assert.Equal(allProducts.Rows.Count, allProducts.TotalCount);
        Assert.False(allProducts.IsTruncated);
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
        Assert.Equal(report.Rows.Count, report.TotalCount);
        Assert.False(report.IsTruncated);
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
        Assert.Equal(report.Rows.Count, report.TotalCount);
        Assert.False(report.IsTruncated);
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

        var businessDate = DateOnly.FromDateTime(soldAt);
        fx.Db.CashMovements.Add(
            new CashMovement
            {
                Id = Guid.NewGuid(),
                TenantId = DevelopmentSeedIds.TenantId,
                BusinessDate = businessDate,
                MovementType = CashMovementType.PettyCash,
                Amount = 10000m,
                Description = "Caja menor",
                CreatedByUserId = DevelopmentSeedIds.AdminUserId,
                OccurredAtUtc = soldAt,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var start = DateOnly.FromDateTime(soldAt.Date.AddDays(-1));
        var end = DateOnly.FromDateTime(soldAt.Date.AddDays(1));
        var report = await sut.GetDailySummaryReportAsync(start, end);

        Assert.Equal("Bistró Demo", report.TenantName);
        var row = Assert.Single(report.Rows, r => r.Date == businessDate && r.TotalSales == 50000m);
        Assert.Equal(1, row.SalesOrderCount);
        Assert.Equal(1m, row.ItemsSold);
        Assert.Equal(10000m, row.TotalCashOutflows);
        Assert.Equal(1, row.CashOutflowCount);
        Assert.Equal(row.TotalSales - row.TotalPurchases - row.TotalCashOutflows, row.NetResult);
        Assert.True(report.GrandTotalSales >= 50000m);
        Assert.True(report.GrandTotalCashOutflows >= 10000m);
    }

    [Fact]
    public async Task GetDailySummaryReport_includes_outflow_only_days_and_skips_adjustment_in()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();
        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        fx.TenantContext.TenantId = DevelopmentSeedIds.TenantId;

        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-12));
        fx.Db.CashMovements.AddRange(
            new CashMovement
            {
                Id = Guid.NewGuid(),
                TenantId = DevelopmentSeedIds.TenantId,
                BusinessDate = businessDate,
                MovementType = CashMovementType.OtherOutflow,
                Amount = 25000m,
                Description = "Otro egreso",
                CreatedByUserId = DevelopmentSeedIds.AdminUserId,
                OccurredAtUtc = DateTime.UtcNow,
            },
            new CashMovement
            {
                Id = Guid.NewGuid(),
                TenantId = DevelopmentSeedIds.TenantId,
                BusinessDate = businessDate,
                MovementType = CashMovementType.AdjustmentIn,
                Amount = 8000m,
                Description = "Ajuste entrada",
                CreatedByUserId = DevelopmentSeedIds.AdminUserId,
                OccurredAtUtc = DateTime.UtcNow,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var report = await sut.GetDailySummaryReportAsync(businessDate, businessDate);
        var row = Assert.Single(report.Rows, r => r.Date == businessDate && r.TotalCashOutflows == 25000m);

        Assert.Equal(1, row.CashOutflowCount);
        Assert.Equal(row.TotalSales - row.TotalPurchases - 25000m, row.NetResult);
    }
}
