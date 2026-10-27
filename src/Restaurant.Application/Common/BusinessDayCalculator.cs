namespace Restaurant.Application.Common;

public static class BusinessDayCalculator
{
    public static DateOnly ResolveCalendarDate(DateTime utcNow, string? timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            tz);
        return DateOnly.FromDateTime(local);
    }

    public static DateOnly ResolveBusinessDate(DateTime utcNow, string? timeZoneId, int cutoffHour)
    {
        var hour = Math.Clamp(cutoffHour, 0, 23);
        var tz = ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            tz);
        var date = DateOnly.FromDateTime(local);
        if (local.Hour < hour)
            date = date.AddDays(-1);
        return date;
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
