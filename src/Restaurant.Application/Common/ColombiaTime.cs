namespace Restaurant.Application.Common;

/// <summary>
/// Display clock for Host. Persist UTC; convert here for tickets, receipts, and planillas.
/// Pasto uses America/Bogota (UTC−5, no DST).
/// </summary>
public static class ColombiaTime
{
    public const string TimeZoneId = "America/Bogota";

    public static TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static DateTime ToBogota(DateTime utc)
    {
        var instant = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        };
        return TimeZoneInfo.ConvertTimeFromUtc(instant, TimeZone);
    }

    public static DateOnly CalendarDate(DateTime utc) => DateOnly.FromDateTime(ToBogota(utc));

    public static DateOnly Today() => CalendarDate(DateTime.UtcNow);

    public static string Stamp(DateTime utc, string format) => ToBogota(utc).ToString(format);

    public static string NowStamp(string format) => Stamp(DateTime.UtcNow, format);
}
