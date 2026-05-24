using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class DailyClosureAdvanceDayTests
{
    [Fact]
    public async Task CloseDailyAsync_advances_active_operational_business_date()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;

        await fx.Db.Tenants.AddAsync(new Tenant
        {
            Id = fx.TenantId,
            Name = "Test",
            Slug = "test",
            TimeZoneId = "UTC",
            IsActive = true,
        });
        await fx.Db.TenantSettings.AddAsync(new TenantSettings
        {
            TenantId = fx.TenantId,
            MaxDiscountPercent = 10m,
            OperationalDayCutoffHour = 4,
        });
        await fx.Db.SaveChangesAsync();

        var clockDate = BusinessDayCalculator.ResolveBusinessDate(DateTime.UtcNow, "UTC", 4);
        var businessDate = clockDate;
        var expectedNext = businessDate.AddDays(1);

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var cashierShifts = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay);
        var dailyClosure = new DailyClosureService(fx.Db, fx.TenantContext, cashierShifts);

        var report = await dailyClosure.CloseDailyAsync(
            businessDate,
            new CloseDailyClosureDto { Notes = "End of day" });

        Assert.Equal(expectedNext, report.NextOperationalBusinessDate);

        var settings = await fx.Db.TenantSettings.SingleAsync(s => s.TenantId == fx.TenantId);
        Assert.Equal(expectedNext, settings.ActiveOperationalBusinessDate);

        var context = await cashierShifts.GetBusinessDayContextAsync();
        Assert.Equal(expectedNext, context.BusinessDate);
        Assert.True(context.IsAdvancedBeyondClock);
    }
}
