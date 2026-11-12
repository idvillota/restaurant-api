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
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
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
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
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
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
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
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
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
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
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

    [Fact]
    public async Task OpenShiftAsync_allows_unclosed_prior_day_when_setting_is_off()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var today = (await operationalDay.ResolveAsync()).BusinessDate;
        await AddShiftAsync(fx, userId, today.AddDays(-1), CashierShiftStatus.Closed);

        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 10m });

        Assert.Equal(today, shift.BusinessDate);
        var context = await sut.GetBusinessDayContextAsync();
        Assert.False(context.RequirePriorDayCloseBeforeNewShift);
        Assert.Null(context.PendingPriorBusinessDate);
    }

    [Fact]
    public async Task OpenShiftAsync_blocks_when_prior_activity_day_is_not_closed()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");
        await EnablePriorDayCloseAsync(fx);

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var today = (await operationalDay.ResolveAsync()).BusinessDate;
        var prior = today.AddDays(-1);
        await AddShiftAsync(fx, userId, prior, CashierShiftStatus.Closed);

        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 10m }));

        Assert.Contains(prior.ToString("yyyy-MM-dd"), error.Message);

        var context = await sut.GetBusinessDayContextAsync();
        Assert.True(context.RequirePriorDayCloseBeforeNewShift);
        Assert.Equal(prior, context.PendingPriorBusinessDate);
        Assert.Empty(context.PendingPriorOpenShifts);
    }

    [Fact]
    public async Task OpenShiftAsync_allows_when_prior_activity_day_is_closed()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");
        await EnablePriorDayCloseAsync(fx);

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var today = (await operationalDay.ResolveAsync()).BusinessDate;
        var prior = today.AddDays(-1);
        await AddShiftAsync(fx, userId, prior, CashierShiftStatus.Closed);
        await AddClosedDayAsync(fx, userId, prior);

        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 10m });
        Assert.Equal(today, shift.BusinessDate);
    }

    [Fact]
    public async Task OpenShiftAsync_ignores_prior_calendar_days_without_activity()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");
        await EnablePriorDayCloseAsync(fx);

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 10m });
        Assert.NotEqual(default, shift.Id);
    }

    [Fact]
    public async Task OpenShiftAsync_blocks_oldest_unclosed_activity_date()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");
        await EnablePriorDayCloseAsync(fx);

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var today = (await operationalDay.ResolveAsync()).BusinessDate;
        var oldest = today.AddDays(-2);
        await AddShiftAsync(fx, userId, oldest, CashierShiftStatus.Closed);
        await AddShiftAsync(fx, userId, today.AddDays(-1), CashierShiftStatus.Closed);

        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var context = await sut.GetBusinessDayContextAsync();
        Assert.Equal(oldest, context.PendingPriorBusinessDate);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.OpenShiftAsync(new OpenCashierShiftDto()));
        Assert.Contains(oldest.ToString("yyyy-MM-dd"), error.Message);
    }

    [Fact]
    public async Task ForceCloseShiftAsync_closes_another_users_open_shift()
    {
        using var fx = new TenantDbFixture();
        var managerId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        fx.TenantContext.UserId = managerId;
        await SeedTenantUserAsync(fx, managerId, "manager@test.local");
        await fx.Db.Users.AddAsync(new User
        {
            Id = cashierId,
            Email = "other@test.local",
            NormalizedEmail = "OTHER@TEST.LOCAL",
            PasswordHash = "hash",
        });
        await fx.Db.SaveChangesAsync();

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var prior = (await operationalDay.ResolveAsync()).BusinessDate.AddDays(-1);
        var leftoverId = await AddShiftAsync(fx, cashierId, prior, CashierShiftStatus.Open);

        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var report = await sut.ForceCloseShiftAsync(leftoverId, "arqueo gerente");

        Assert.Equal(CashierShiftStatus.Closed, report.Shift.Status);
        Assert.Equal(report.Shift.ExpectedCash, report.Shift.CountedCash);
        Assert.Equal(0m, report.Shift.CashOverShort);
        var stored = await fx.Db.CashierShifts.SingleAsync(s => s.Id == leftoverId);
        Assert.Contains("arqueo gerente", stored.ClosingNotes);
    }

    [Fact]
    public async Task GetBusinessDayContext_lists_leftover_open_shifts_on_pending_day()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");
        await EnablePriorDayCloseAsync(fx);

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var prior = (await operationalDay.ResolveAsync()).BusinessDate.AddDays(-1);
        var leftoverId = await AddShiftAsync(fx, userId, prior, CashierShiftStatus.Open);

        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var context = await sut.GetBusinessDayContextAsync();

        Assert.Equal(prior, context.PendingPriorBusinessDate);
        Assert.Equal(leftoverId, Assert.Single(context.PendingPriorOpenShifts).Id);
    }

    [Fact]
    public async Task VoidCashMovement_excludes_amount_from_open_shift_cash_total()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 100m });
        var movement = await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.PettyCash,
            Amount = 30m,
            Description = "Duplicado",
        });

        var voided = await sut.VoidCashMovementAsync(
            movement.Id,
            new VoidCashMovementDto { Reason = "Doble registro" },
            canVoidClosedShift: false);

        Assert.True(voided.IsVoided);
        var report = await sut.GetShiftReportAsync(shift.Id);
        Assert.Equal(0m, report.CashOutflowTotal);
        Assert.Contains(report.CashOutflows, m => m.Id == movement.Id && m.IsVoided);
    }

    [Fact]
    public async Task VoidCashMovement_rejects_other_cashier_on_open_shift()
    {
        using var fx = new TenantDbFixture();
        var creatorId = Guid.NewGuid();
        fx.TenantContext.UserId = creatorId;
        await SeedTenantUserAsync(fx, creatorId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 50m });
        var movement = await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.PettyCash,
            Amount = 10m,
            Description = "Taxi",
        });

        fx.TenantContext.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.VoidCashMovementAsync(movement.Id, new VoidCashMovementDto(), canVoidClosedShift: false));
    }

    [Fact]
    public async Task VoidCashMovement_rejects_when_operational_day_is_closed()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 50m });
        var movement = await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.PettyCash,
            Amount = 10m,
            Description = "Taxi",
        });

        var today = (await operationalDay.ResolveAsync()).BusinessDate;
        await AddClosedDayAsync(fx, userId, today);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.VoidCashMovementAsync(movement.Id, new VoidCashMovementDto(), canVoidClosedShift: true));
    }

    [Fact]
    public async Task VoidCashMovement_recalculates_expected_cash_on_closed_shift()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 100m });
        var movement = await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.PettyCash,
            Amount = 30m,
            Description = "Duplicado",
        });
        await sut.CloseShiftAsync(shift.Id, new CloseCashierShiftDto { CountedCash = 70m });

        await sut.VoidCashMovementAsync(
            movement.Id,
            new VoidCashMovementDto { Reason = "Error" },
            canVoidClosedShift: true);

        var stored = await fx.Db.CashierShifts.SingleAsync(s => s.Id == shift.Id);
        Assert.Equal(100m, stored.ExpectedCash);
        Assert.Equal(70m, stored.CountedCash);
    }

    [Fact]
    public async Task VoidCashMovement_rejects_second_void()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 50m });
        var movement = await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.PettyCash,
            Amount = 10m,
            Description = "Taxi",
        });
        await sut.VoidCashMovementAsync(movement.Id, new VoidCashMovementDto(), canVoidClosedShift: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.VoidCashMovementAsync(movement.Id, new VoidCashMovementDto(), canVoidClosedShift: false));
    }

    [Fact]
    public async Task RecordCashMovementAsync_rejects_adjustment_in_without_daily_closure()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 100m });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RecordCashMovementAsync(new CreateCashMovementDto
            {
                MovementType = CashMovementType.AdjustmentIn,
                Amount = 30m,
                Description = "Corrección egreso duplicado",
            }));
    }

    [Fact]
    public async Task AdjustmentIn_increases_expected_cash_on_open_shift()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay, new NoopPlanillaPdfService());
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 100m });

        await sut.RecordCashMovementAsync(new CreateCashMovementDto
        {
            MovementType = CashMovementType.PettyCash,
            Amount = 30m,
            Description = "Duplicado ayer en libros",
        });
        await sut.RecordCashMovementAsync(
            new CreateCashMovementDto
            {
                MovementType = CashMovementType.AdjustmentIn,
                Amount = 30m,
                Description = "Corrección egreso duplicado del día anterior",
            },
            canRecordCashAdjustment: true);

        var report = await sut.GetShiftReportAsync(shift.Id);
        Assert.Equal(30m, report.CashOutflowTotal);
        Assert.Equal(30m, report.CashInflowTotal);
        Assert.Single(report.CashInflows);
        Assert.Equal(CashMovementType.AdjustmentIn, report.CashInflows[0].MovementType);

        var closed = await sut.CloseShiftAsync(shift.Id, new CloseCashierShiftDto { CountedCash = 100m });
        Assert.Equal(100m, closed.Shift.ExpectedCash);
        Assert.Equal(0m, closed.Shift.CashOverShort);
    }

    private static async Task EnablePriorDayCloseAsync(TenantDbFixture fx)
    {
        var settings = await fx.Db.TenantSettings.SingleAsync();
        settings.RequirePriorDayCloseBeforeNewShift = true;
        await fx.Db.SaveChangesAsync();
    }

    private static async Task<Guid> AddShiftAsync(
        TenantDbFixture fx,
        Guid cashierUserId,
        DateOnly businessDate,
        CashierShiftStatus status)
    {
        var id = Guid.NewGuid();
        fx.Db.CashierShifts.Add(new CashierShift
        {
            Id = id,
            TenantId = fx.TenantId,
            CashierUserId = cashierUserId,
            Status = status,
            BusinessDate = businessDate,
            OpenedAtUtc = DateTime.UtcNow.AddDays(-1),
            ClosedAtUtc = status == CashierShiftStatus.Closed ? DateTime.UtcNow.AddHours(-12) : null,
            OpeningFloat = 50m,
            ExpectedCash = status == CashierShiftStatus.Closed ? 50m : null,
            CountedCash = status == CashierShiftStatus.Closed ? 50m : null,
        });
        await fx.Db.SaveChangesAsync();
        return id;
    }

    private static async Task AddClosedDayAsync(TenantDbFixture fx, Guid userId, DateOnly businessDate)
    {
        fx.Db.DailyClosures.Add(new DailyClosure
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            BusinessDate = businessDate,
            Status = DailyClosureStatus.Closed,
            ClosedAtUtc = DateTime.UtcNow.AddHours(-8),
            ClosedByUserId = userId,
        });
        await fx.Db.SaveChangesAsync();
    }
}
