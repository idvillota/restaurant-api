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

    [Fact]
    public void ToUtcRange_uses_bogota_midnight_not_utc_midnight()
    {
        var (startUtc, endExclusive) = ColombiaTime.ToUtcRange(
            new DateOnly(2026, 11, 13),
            new DateOnly(2026, 11, 13));

        Assert.Equal(new DateTime(2026, 11, 13, 5, 0, 0, DateTimeKind.Utc), startUtc);
        Assert.Equal(new DateTime(2026, 11, 14, 5, 0, 0, DateTimeKind.Utc), endExclusive);
        Assert.Equal(21, ColombiaTime.Hour(new DateTime(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc)));
    }
}
