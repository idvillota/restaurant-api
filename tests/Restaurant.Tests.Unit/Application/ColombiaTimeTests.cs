using Restaurant.Application.Common;

namespace Restaurant.Tests.Unit.Application;

public sealed class ColombiaTimeTests
{
    [Fact]
    public void ToBogota_converts_utc_minus_five()
    {
        var utc = new DateTime(2026, 11, 13, 17, 0, 0, DateTimeKind.Utc);
        var local = ColombiaTime.ToBogota(utc);
        Assert.Equal(new DateTime(2026, 11, 13, 12, 0, 0), local);
    }

    [Fact]
    public void CalendarDate_uses_bogota_not_utc_date()
    {
        var utc = new DateTime(2026, 11, 14, 3, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 11, 13), ColombiaTime.CalendarDate(utc));
        Assert.Equal("20261113", ColombiaTime.Stamp(utc, "yyyyMMdd"));
    }
}
