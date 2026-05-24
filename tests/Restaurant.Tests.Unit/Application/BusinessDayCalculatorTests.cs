using Restaurant.Application.Common;

namespace Restaurant.Tests.Unit.Application;

public sealed class BusinessDayCalculatorTests
{
    [Fact]
    public void ResolveBusinessDate_before_cutoff_uses_previous_calendar_day()
    {
        // 2026-05-24 02:30 UTC = 2026-05-23 21:30 in America/Bogota (UTC-5)
        var utc = new DateTime(2026, 5, 24, 2, 30, 0, DateTimeKind.Utc);
        var date = BusinessDayCalculator.ResolveBusinessDate(utc, "America/Bogota", 4);
        Assert.Equal(new DateOnly(2026, 5, 23), date);
    }

    [Fact]
    public void ResolveBusinessDate_after_cutoff_uses_same_calendar_day()
    {
        // 2026-05-24 10:00 UTC = 2026-05-24 05:00 in America/Bogota
        var utc = new DateTime(2026, 5, 24, 10, 0, 0, DateTimeKind.Utc);
        var date = BusinessDayCalculator.ResolveBusinessDate(utc, "America/Bogota", 4);
        Assert.Equal(new DateOnly(2026, 5, 24), date);
    }
}
