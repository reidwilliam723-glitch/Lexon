namespace Lexon.Core.Grammar;

public static class QuietHours
{
    public static bool IsQuiet(TimeOnly now, TimeOnly start, TimeOnly end, bool enabled)
    {
        if (!enabled || start == end)
        {
            return false;
        }

        if (start < end)
        {
            return now >= start && now < end;
        }

        return now >= start || now < end;
    }

    public static bool TryParse(string? text, out TimeOnly time)
        => TimeOnly.TryParse(text?.Trim(), out time);

    public static DateTime WindowEnd(DateTime now, TimeOnly start, TimeOnly end)
    {
        var todayEnd = now.Date + end.ToTimeSpan();
        if (start < end)
        {
            return todayEnd;
        }

        if (TimeOnly.FromDateTime(now) >= start)
        {
            return todayEnd.AddDays(1);
        }

        return todayEnd;
    }
}
