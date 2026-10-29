using Restaurant.Application.Features.Crm.Customers;

namespace Restaurant.Tests.Unit.Application;

public sealed class UpcomingBirthdayWindowTests
{
    [Fact]
    public void TryDays_maps_week_and_month()
    {
        Assert.True(UpcomingBirthdayWindow.TryDays("week", out var week));
        Assert.Equal(7, week);
        Assert.True(UpcomingBirthdayWindow.TryDays("month", out var month));
        Assert.Equal(30, month);
        Assert.False(UpcomingBirthdayWindow.TryDays("all", out _));
    }

    [Fact]
    public void MonthDayKeys_includes_today_and_wraps_year()
    {
        var keys = UpcomingBirthdayWindow.MonthDayKeys(new DateOnly(2026, 12, 29), 7);
        Assert.Contains(UpcomingBirthdayWindow.Pack(12, 29), keys);
        Assert.Contains(UpcomingBirthdayWindow.Pack(12, 31), keys);
        Assert.Contains(UpcomingBirthdayWindow.Pack(1, 1), keys);
        Assert.Contains(UpcomingBirthdayWindow.Pack(1, 4), keys);
        Assert.DoesNotContain(UpcomingBirthdayWindow.Pack(1, 5), keys);
        Assert.DoesNotContain(UpcomingBirthdayWindow.Pack(12, 28), keys);
    }

    [Fact]
    public void MonthDayKeys_treats_feb_29_as_feb_28_in_non_leap_year()
    {
        var keys = UpcomingBirthdayWindow.MonthDayKeys(new DateOnly(2025, 2, 27), 7);
        Assert.Contains(UpcomingBirthdayWindow.Pack(2, 28), keys);
        Assert.Contains(UpcomingBirthdayWindow.Pack(2, 29), keys);
        Assert.Contains(UpcomingBirthdayWindow.Pack(3, 1), keys);
    }

    [Fact]
    public void MonthDayKeys_does_not_invent_feb_29_when_window_misses_feb_28()
    {
        var keys = UpcomingBirthdayWindow.MonthDayKeys(new DateOnly(2025, 3, 1), 7);
        Assert.DoesNotContain(UpcomingBirthdayWindow.Pack(2, 29), keys);
    }
}
