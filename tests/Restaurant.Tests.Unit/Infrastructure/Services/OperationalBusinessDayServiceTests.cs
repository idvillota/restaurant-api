using Restaurant.Application.Common;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class OperationalBusinessDayServiceTests
{
    [Fact]
    public async Task ResolveAsync_skips_closed_day_to_next_open_day()
    {
        using var fx = new TenantDbFixture();
        var clockDate = BusinessDayCalculator.ResolveBusinessDate(DateTime.UtcNow, "UTC", 4);

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
        await fx.Db.DailyClosures.AddAsync(new DailyClosure
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            BusinessDate = clockDate,
            Status = DailyClosureStatus.Closed,
            ClosedAtUtc = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();

        var sut = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var day = await sut.ResolveAsync();

        Assert.Equal(clockDate.AddDays(1), day.BusinessDate);
        Assert.Equal(DailyClosureStatus.Open, day.ClosureStatus);
        Assert.True(day.IsAdvancedBeyondClock);
    }
}
