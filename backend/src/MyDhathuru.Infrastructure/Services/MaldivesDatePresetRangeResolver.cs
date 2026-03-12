namespace MyDhathuru.Infrastructure.Services;

internal static class MaldivesDatePresetRangeResolver
{
    private static readonly TimeSpan MaldivesOffset = TimeSpan.FromHours(5);

    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc)? Resolve(
        string? preset,
        DateOnly? customStartDate = null,
        DateOnly? customEndDate = null)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            if (customStartDate.HasValue && customEndDate.HasValue)
            {
                return BuildRange(customStartDate.Value, customEndDate.Value);
            }

            return null;
        }

        var key = NormalizePreset(preset);
        if (key is "all" or "none")
        {
            return null;
        }

        var maldivesNow = DateTimeOffset.UtcNow.ToOffset(MaldivesOffset);
        var today = DateOnly.FromDateTime(maldivesNow.DateTime);

        return key switch
        {
            "today" => BuildRange(today, today),
            "yesterday" => BuildRange(today.AddDays(-1), today.AddDays(-1)),
            "last7days" => BuildRange(today.AddDays(-6), today),
            "last30days" => BuildRange(today.AddDays(-29), today),
            "lastweek" => BuildLastWeekRange(today),
            "lastmonth" => BuildLastMonthRange(today),
            _ => null
        };
    }

    private static string NormalizePreset(string preset)
    {
        return new string(
            preset.Trim().ToLowerInvariant()
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '-' && ch != '_')
                .ToArray());
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) BuildLastWeekRange(DateOnly today)
    {
        var currentWeekStart = StartOfWeek(today, DayOfWeek.Sunday);
        var previousWeekStart = currentWeekStart.AddDays(-7);
        var previousWeekEnd = currentWeekStart.AddDays(-1);

        return BuildRange(previousWeekStart, previousWeekEnd);
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) BuildLastMonthRange(DateOnly today)
    {
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthEnd = currentMonthStart.AddDays(-1);

        return BuildRange(previousMonthStart, previousMonthEnd);
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) BuildRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var startUtc = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), MaldivesOffset).ToUniversalTime();
        var endUtc = new DateTimeOffset(endDate.ToDateTime(TimeOnly.MaxValue), MaldivesOffset).ToUniversalTime();

        return (startUtc, endUtc);
    }

    private static DateOnly StartOfWeek(DateOnly date, DayOfWeek firstDayOfWeek)
    {
        var offset = (7 + (date.DayOfWeek - firstDayOfWeek)) % 7;
        return date.AddDays(-offset);
    }
}

