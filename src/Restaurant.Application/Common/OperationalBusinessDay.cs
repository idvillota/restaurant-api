namespace Restaurant.Application.Common;

public static class OperationalBusinessDay
{
    public static DateOnly ResolveEffective(
        DateTime utcNow,
        string? timeZoneId,
        int cutoffHour,
        DateOnly? activeOperationalBusinessDate)
    {
        var clockDate = BusinessDayCalculator.ResolveBusinessDate(utcNow, timeZoneId, cutoffHour);
        if (activeOperationalBusinessDate is { } active && active > clockDate)
            return active;
        return clockDate;
    }

    public static bool ShouldClearActiveDate(DateOnly clockDate, DateOnly? activeOperationalBusinessDate) =>
        activeOperationalBusinessDate is not null && clockDate >= activeOperationalBusinessDate;
}
