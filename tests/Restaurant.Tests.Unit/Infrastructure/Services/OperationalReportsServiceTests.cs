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
        var soldAt = new DateTime(2026, 1, 15, 20, 0, 0, DateTimeKind.Utc);
        var businessDate = new DateOnly(2026, 1, 15);
        await SeedPaidBillAsync(
            fx,
            businessDate,
            soldAt,
            productId,
            "Pizza margarita",
            quantity: 2m,
            unitPrice: 32000m,
            attachShift: true);

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var start = businessDate.AddDays(-1);
        var end = businessDate.AddDays(1);

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
    public async Task GetCashOutflowsReport_lists_all_outflows_and_splits_caja_totals()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        var businessDate = new DateOnly(2026, 11, 7);
        var providerId = Guid.NewGuid();

        await fx.Db.Tenants.AddAsync(new Tenant
        {
            Id = fx.TenantId,
            Name = "Local prueba",
            Slug = "local-prueba",
            TimeZoneId = "America/Bogota",
            IsActive = true,
        });
        await fx.Db.Users.AddAsync(new User
        {
            Id = userId,
            Email = "cajero@test.local",
            NormalizedEmail = "CAJERO@TEST.LOCAL",
            PasswordHash = "hash",
        });
        await fx.Db.Providers.AddAsync(new Provider
        {
            Id = providerId,
            TenantId = fx.TenantId,
            Name = "Café del valle",
            IsActive = true,
        });
        fx.Db.CashMovements.AddRange(
            new CashMovement
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                BusinessDate = businessDate,
                MovementType = CashMovementType.ProviderPayment,
                Amount = 40m,
                Description = "Pago factura",
                ProviderId = providerId,
                IsCashDrawerPayment = true,
                CreatedByUserId = userId,
                OccurredAtUtc = DateTime.UtcNow,
            },
            new CashMovement
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                BusinessDate = businessDate,
                MovementType = CashMovementType.OtherOutflow,
                Amount = 10m,
                Description = "Informativo",
                IsCashDrawerPayment = false,
                CreatedByUserId = userId,
                OccurredAtUtc = DateTime.UtcNow,
            },
            new CashMovement
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                BusinessDate = businessDate,
                MovementType = CashMovementType.AdjustmentIn,
                Amount = 8m,
                Description = "No es egreso",
                IsCashDrawerPayment = true,
                CreatedByUserId = userId,
                OccurredAtUtc = DateTime.UtcNow,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var all = await sut.GetCashOutflowsReportAsync(businessDate, businessDate, null, null, null);
        var cajaOnly = await sut.GetCashOutflowsReportAsync(businessDate, businessDate, null, null, true);
        var byProvider = await sut.GetCashOutflowsReportAsync(businessDate, businessDate, null, providerId, null);

        Assert.Equal(2, all.TotalCount);
        Assert.Equal(50m, all.TotalAmount);
        Assert.Equal(40m, all.TotalCashDrawerAmount);
        Assert.DoesNotContain(all.Rows, r => r.MovementType == CashMovementType.AdjustmentIn);
        Assert.Contains(all.Rows, r => r.ProviderName == "Café del valle");
        Assert.Equal(1, cajaOnly.TotalCount);
        Assert.Equal(40m, cajaOnly.TotalAmount);
        Assert.Equal(40m, cajaOnly.TotalCashDrawerAmount);
        Assert.Equal(1, byProvider.TotalCount);
        Assert.Equal("Café del valle", byProvider.ProviderName);
    }

    [Fact]
    public async Task GetDailySummaryReport_groups_sales_and_purchases_by_date()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();
        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        fx.TenantContext.TenantId = DevelopmentSeedIds.TenantId;

        var soldAt = new DateTime(2026, 1, 16, 21, 0, 0, DateTimeKind.Utc);
        var businessDate = new DateOnly(2026, 1, 16);
        await SeedPaidBillAsync(
            fx,
            businessDate,
            soldAt,
            DevelopmentSeedIds.ProductIds[0],
            "Pizza margarita",
            quantity: 1m,
            unitPrice: 50000m,
            attachShift: true);
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
    public async Task GetDailySummaryReport_includes_outflow_only_days_and_adds_adjustment_in_to_net()
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
        Assert.Equal(8000m, row.TotalCashInflows);
        Assert.Equal(1, row.CashInflowCount);
        Assert.Equal(row.TotalSales - row.TotalPurchases - 25000m + 8000m, row.NetResult);
    }

    [Fact]
    public async Task GetDailySummaryReport_excludes_informational_outflows_from_totals()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();
        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        fx.TenantContext.TenantId = DevelopmentSeedIds.TenantId;

        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-13));
        fx.Db.CashMovements.AddRange(
            new CashMovement
            {
                Id = Guid.NewGuid(),
                TenantId = DevelopmentSeedIds.TenantId,
                BusinessDate = businessDate,
                MovementType = CashMovementType.PettyCash,
                Amount = 15000m,
                Description = "Caja",
                IsCashDrawerPayment = true,
                CreatedByUserId = DevelopmentSeedIds.AdminUserId,
                OccurredAtUtc = DateTime.UtcNow,
            },
            new CashMovement
            {
                Id = Guid.NewGuid(),
                TenantId = DevelopmentSeedIds.TenantId,
                BusinessDate = businessDate,
                MovementType = CashMovementType.OtherOutflow,
                Amount = 40000m,
                Description = "Informativo",
                IsCashDrawerPayment = false,
                CreatedByUserId = DevelopmentSeedIds.AdminUserId,
                OccurredAtUtc = DateTime.UtcNow,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var report = await sut.GetDailySummaryReportAsync(businessDate, businessDate);
        var row = Assert.Single(report.Rows, r => r.Date == businessDate);

        Assert.Equal(15000m, row.TotalCashOutflows);
        Assert.Equal(1, row.CashOutflowCount);
    }

    [Fact]
    public async Task GetSalesReport_sunday_evening_bogota_stays_on_sunday_without_shift()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();
        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        fx.TenantContext.TenantId = DevelopmentSeedIds.TenantId;

        // Sunday 21:00 Bogota = Monday 02:00 UTC — calendar Bogotá still Sunday.
        var paidAtUtc = new DateTime(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc);
        var sunday = new DateOnly(2026, 11, 15);
        await SeedPaidBillAsync(
            fx,
            sunday,
            paidAtUtc,
            DevelopmentSeedIds.ProductIds[0],
            "Pizza margarita",
            quantity: 1m,
            unitPrice: 18000m,
            attachShift: false);

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var sundayReport = await sut.GetSalesReportAsync(sunday, sunday, productId: null);
        var mondayReport = await sut.GetSalesReportAsync(sunday.AddDays(1), sunday.AddDays(1), productId: null);

        Assert.Contains(sundayReport.Rows, r => r.Quantity == 1m && r.OperationalDate == sunday);
        Assert.DoesNotContain(mondayReport.Rows, r => r.UnitPrice == 18000m);
    }

    [Fact]
    public async Task GetDailySummaryReport_post_close_cobro_belongs_to_next_operational_day()
    {
        using var fx = new TenantDbFixture();
        var hasher = new BcryptPasswordHasher();
        await DevelopmentDataSeeder.SeedAsync(fx.Db, hasher, NullLogger.Instance, fx.TenantContext);
        fx.TenantContext.TenantId = DevelopmentSeedIds.TenantId;

        var monday = new DateOnly(2026, 11, 16);
        var tuesday = monday.AddDays(1);
        // Monday 20:00 Bogota = Tuesday 01:00 UTC, after a 19:00 close → Tuesday shift.
        var paidAtUtc = new DateTime(2026, 11, 17, 1, 0, 0, DateTimeKind.Utc);
        await SeedPaidBillAsync(
            fx,
            tuesday,
            paidAtUtc,
            DevelopmentSeedIds.ProductIds[0],
            "Pizza margarita",
            quantity: 1m,
            unitPrice: 91000m,
            attachShift: true);

        var sut = new OperationalReportsService(fx.Db, fx.TenantContext);
        var mondaySummary = await sut.GetDailySummaryReportAsync(monday, monday);
        var tuesdaySummary = await sut.GetDailySummaryReportAsync(tuesday, tuesday);
        var sales = await sut.GetSalesReportAsync(monday, tuesday, productId: null);

        Assert.DoesNotContain(mondaySummary.Rows, r => r.TotalSales == 91000m);
        var tuesdayRow = Assert.Single(tuesdaySummary.Rows, r => r.TotalSales == 91000m);
        Assert.Equal(1, tuesdayRow.SalesOrderCount);
        Assert.Contains(sales.Rows, r => r.OperationalDate == tuesday && r.UnitPrice == 91000m);
        Assert.DoesNotContain(sales.Rows, r => r.OperationalDate == monday && r.UnitPrice == 91000m);
    }

    private static async Task SeedPaidBillAsync(
        TenantDbFixture fx,
        DateOnly businessDate,
        DateTime paidAtUtc,
        Guid productId,
        string productName,
        decimal quantity,
        decimal unitPrice,
        bool attachShift)
    {
        var tenantId = DevelopmentSeedIds.TenantId;
        var lineTotal = quantity * unitPrice;
        Guid? shiftId = null;
        if (attachShift)
        {
            shiftId = Guid.NewGuid();
            fx.Db.CashierShifts.Add(
                new CashierShift
                {
                    Id = shiftId.Value,
                    TenantId = tenantId,
                    CashierUserId = DevelopmentSeedIds.AdminUserId,
                    Status = CashierShiftStatus.Open,
                    BusinessDate = businessDate,
                    OpenedAtUtc = paidAtUtc.AddHours(-8),
                    OpeningFloat = 0m,
                });
        }

        var billId = Guid.NewGuid();
        fx.Db.Bills.Add(
            new Bill
            {
                Id = billId,
                TenantId = tenantId,
                CustomerId = DevelopmentSeedIds.CustomerIds[0],
                Number = $"BILL-{billId.ToString("N")[..8]}",
                Status = BillStatus.Paid,
                Subtotal = lineTotal,
                Total = lineTotal,
                IssuedAtUtc = paidAtUtc.AddMinutes(-5),
                PaidAtUtc = paidAtUtc,
                CashierShiftId = shiftId,
                ProcessedByUserId = DevelopmentSeedIds.AdminUserId,
            });
        fx.Db.BillLines.Add(
            new BillLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BillId = billId,
                ProductId = productId,
                ProductName = productName,
                ProductTypeName = "Plato",
                Quantity = quantity,
                UnitPrice = unitPrice,
                LineTotal = lineTotal,
            });
        fx.Db.Payments.Add(
            new Payment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BillId = billId,
                Amount = lineTotal,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Completed,
                PaidAtUtc = paidAtUtc,
                CashierShiftId = shiftId,
                ProcessedByUserId = DevelopmentSeedIds.AdminUserId,
            });
        await fx.Db.SaveChangesAsync();
    }
}
