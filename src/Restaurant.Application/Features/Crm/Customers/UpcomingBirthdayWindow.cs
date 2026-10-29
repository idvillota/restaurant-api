namespace Restaurant.Application.Features.Crm.Customers;

/// <summary>
/// Upcoming birthdays are month+day occurrences from today, wrapping Dec→Jan.
/// 29 Feb is treated as 28 Feb in non-leap years.
/// </summary>
public static class UpcomingBirthdayWindow
{
    public const string FilterWeek = "week";
    public const string FilterMonth = "month";
    public const int WeekDays = 7;
    public const int MonthDays = 30;

    public static bool TryDays(string? raw, out int days)
    {
        days = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        switch (raw.Trim().ToLowerInvariant())
        {
            case FilterWeek:
            case "7":
                days = WeekDays;
                return true;
            case FilterMonth:
            case "30":
                days = MonthDays;
                return true;
            default:
                return false;
        }
    }

    public static IReadOnlyList<int> MonthDayKeys(DateOnly today, int inclusiveDays)
    {
        if (inclusiveDays < 1)
            return Array.Empty<int>();

        var keys = new HashSet<int>();
        for (var i = 0; i < inclusiveDays; i++)
        {
            var date = today.AddDays(i);
            keys.Add(Pack(date.Month, date.Day));
            if (date is { Month: 2, Day: 28 } && !DateTime.IsLeapYear(date.Year))
                keys.Add(Pack(2, 29));
        }

        return keys.ToList();
    }

    public static int Pack(int month, int day) => month * 100 + day;
}
