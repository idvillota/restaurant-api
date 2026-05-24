using Restaurant.Application.Common;

namespace Restaurant.Tests.Unit.Application;

public sealed class OperationalBusinessDayTests
{
    [Fact]
    public void ResolveEffective_uses_active_date_when_ahead_of_clock()
    {
        var utc = new DateTime(2026, 5, 24, 2, 0, 0, DateTimeKind.Utc);
        var clock = BusinessDayCalculator.ResolveBusinessDate(utc, "America/Bogota", 4);
        var active = clock.AddDays(1);

        var effective = OperationalBusinessDay.ResolveEffective(utc, "America/Bogota", 4, active);

        Assert.Equal(active, effective);
        Assert.True(effective > clock);
    }

    [Fact]
    public void ResolveEffective_uses_clock_when_active_is_null()
    {
        var utc = new DateTime(2026, 5, 24, 10, 0, 0, DateTimeKind.Utc);
        var clock = BusinessDayCalculator.ResolveBusinessDate(utc, "America/Bogota", 4);

        var effective = OperationalBusinessDay.ResolveEffective(utc, "America/Bogota", 4, null);

        Assert.Equal(clock, effective);
    }

    [Fact]
    public void ShouldClearActiveDate_when_clock_reaches_active()
    {
        var clock = new DateOnly(2026, 5, 25);
        var active = new DateOnly(2026, 5, 25);
        Assert.True(OperationalBusinessDay.ShouldClearActiveDate(clock, active));
    }
}
