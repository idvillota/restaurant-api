using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class CashierShiftServiceTests
{
    private static async Task SeedTenantUserAsync(TenantDbFixture fx, Guid userId, string email)
    {
        await fx.Db.Tenants.AddAsync(new Tenant
        {
            Id = fx.TenantId,
            Name = "Test",
            Slug = "test",
            TimeZoneId = "America/Bogota",
            IsActive = true,
        });
        await fx.Db.Users.AddAsync(new User
        {
            Id = userId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "hash",
        });
        await fx.Db.TenantSettings.AddAsync(new TenantSettings
        {
            TenantId = fx.TenantId,
            MaxDiscountPercent = 10m,
            OperationalDayCutoffHour = 4,
        });
        await fx.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task OpenShiftAsync_does_not_duplicate_user()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay);
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 100m });

        Assert.Equal("cashier@test.local", shift.CashierEmail);
        Assert.Equal(1, await fx.Db.Users.CountAsync());
        Assert.Equal(1, await fx.Db.CashierShifts.CountAsync());
    }

    [Fact]
    public async Task RecordCashMovementAsync_requires_provider_for_provider_payment()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay);
        await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 50m });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RecordCashMovementAsync(new CreateCashMovementDto
            {
                MovementType = CashMovementType.ProviderPayment,
                Amount = 20m,
                Description = "Pago",
            }));
    }

    [Fact]
    public async Task RecordCashMovementAsync_stores_provider_on_provider_payment()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var providerId = Guid.NewGuid();
        await fx.Db.Providers.AddAsync(new Provider
        {
            Id = providerId,
            TenantId = fx.TenantId,
            Name = "Distribuidora Sur",
            IsActive = true,
        });
        await fx.Db.SaveChangesAsync();

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay);
        await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 50m });

        var movement = await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.ProviderPayment,
            Amount = 20m,
            Description = "Abono",
            ProviderId = providerId,
        });

        Assert.Equal(providerId, movement.ProviderId);
        Assert.Equal("Distribuidora Sur", movement.ProviderName);

        var petty = await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.PettyCash,
            Amount = 5m,
            Description = "Taxis",
            ProviderId = providerId,
        });
        Assert.Null(petty.ProviderId);
        Assert.True(petty.IsCashDrawerPayment);
    }

    [Fact]
    public async Task Shift_report_lists_informational_outflow_but_excludes_it_from_cash_total()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay);
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 100m });

        await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.PettyCash,
            Amount = 30m,
            Description = "Sale de caja",
        });
        await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.OtherOutflow,
            Amount = 80m,
            Description = "Solo informativo",
            IsCashDrawerPayment = false,
        });

        var report = await sut.GetShiftReportAsync(shift.Id);

        Assert.Equal(2, report.CashOutflows.Count);
        Assert.Equal(30m, report.CashOutflowTotal);
        Assert.Contains(report.CashOutflows, m => m.Description == "Solo informativo" && !m.IsCashDrawerPayment);
    }

    [Fact]
    public async Task Shift_report_lists_vitrina_cafe_apart_from_general_sales()
    {
        using var fx = new TenantDbFixture();
        var sut = await SeedMixedVitrinaShiftAsync(fx, separateVitrina: true);

        var report = await sut.GetShiftReportAsync((await fx.Db.CashierShifts.SingleAsync()).Id);

        Assert.True(report.SeparateVitrinaCafeSales);
        Assert.Equal(70m, report.TotalSales);
        Assert.Equal(30m, report.VitrinaCafeSales);
        Assert.Equal(100m, report.TotalsByMethod.Single(t => t.Method == PaymentMethod.Cash).Total);
    }

    [Fact]
    public async Task Shift_report_keeps_vitrina_inside_ventas_when_flag_is_off()
    {
        using var fx = new TenantDbFixture();
        var sut = await SeedMixedVitrinaShiftAsync(fx, separateVitrina: false);

        var report = await sut.GetShiftReportAsync((await fx.Db.CashierShifts.SingleAsync()).Id);

        Assert.False(report.SeparateVitrinaCafeSales);
        Assert.Equal(100m, report.TotalSales);
        Assert.Equal(0m, report.VitrinaCafeSales);
        Assert.Equal(100m, report.TotalsByMethod.Single(t => t.Method == PaymentMethod.Cash).Total);
    }

    private static async Task<CashierShiftService> SeedMixedVitrinaShiftAsync(
        TenantDbFixture fx,
        bool separateVitrina)
    {
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var settings = await fx.Db.TenantSettings.SingleAsync();
        settings.SeparateVitrinaCafeSales = separateVitrina;
        await fx.Db.SaveChangesAsync();

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay);
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 0m });

        var customerId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        fx.Db.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = fx.TenantId,
            Name = "Cliente",
            IsActive = true,
        });
        fx.Db.Bills.Add(new Bill
        {
            Id = billId,
            TenantId = fx.TenantId,
            CustomerId = customerId,
            Number = "BILL-TEST-1",
            Status = BillStatus.Paid,
            Total = 100m,
            IssuedAtUtc = DateTime.UtcNow,
            PaidAtUtc = DateTime.UtcNow,
            CashierShiftId = shift.Id,
        });
        fx.Db.BillLines.AddRange(
            new BillLine
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                BillId = billId,
                ProductId = Guid.NewGuid(),
                ProductName = "Espresso",
                ProductTypeName = "Bebidas",
                Quantity = 1,
                UnitPrice = 70m,
                LineTotal = 70m,
            },
            new BillLine
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                BillId = billId,
                ProductId = Guid.NewGuid(),
                ProductName = "Café bolsa",
                ProductTypeName = "Vitrina Café",
                Quantity = 1,
                UnitPrice = 30m,
                LineTotal = 30m,
            });
        fx.Db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            BillId = billId,
            Amount = 100m,
            Method = PaymentMethod.Cash,
            Status = PaymentStatus.Completed,
            PaidAtUtc = DateTime.UtcNow,
            CashierShiftId = shift.Id,
        });
        await fx.Db.SaveChangesAsync();
        return sut;
    }
}
