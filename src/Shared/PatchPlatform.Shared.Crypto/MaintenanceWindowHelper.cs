namespace PatchPlatform.Shared.Crypto;

/// <summary>
/// Evaluates whether the current time falls within a maintenance window.
/// Format: "HH:mm-HH:mm" for daily windows, e.g. "02:00-04:00"
/// Optional day-of-week prefix: "Mon-Fri 02:00-04:00"
/// </summary>
public static class MaintenanceWindowHelper
{
    public static bool IsInWindow(string windowExpression, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(windowExpression))
            return false;

        var parts = windowExpression.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string timePart;
        ISet<DayOfWeek>? allowedDays = null;

        if (parts.Length == 2)
        {
            allowedDays = ParseDays(parts[0]);
            timePart = parts[1];
        }
        else
        {
            timePart = parts[0];
        }

        if (!ParseTimeRange(timePart, out var start, out var end))
            return false;

        if (allowedDays != null && !allowedDays.Contains(utcNow.DayOfWeek))
            return false;

        var nowTime = utcNow.TimeOfDay;
        if (start <= end)
            return nowTime >= start && nowTime < end;
        else
            return nowTime >= start || nowTime < end;
    }

    private static bool ParseTimeRange(string timePart, out TimeSpan start, out TimeSpan end)
    {
        start = end = TimeSpan.Zero;
        var dash = timePart.LastIndexOf('-');
        if (dash < 0) return false;
        var s = timePart.Substring(0, dash);
        var e = timePart.Substring(dash + 1);
        return TimeSpan.TryParseExact(s, @"hh\:mm", null, out start)
            && TimeSpan.TryParseExact(e, @"hh\:mm", null, out end);
    }

    private static ISet<DayOfWeek> ParseDays(string daysPart)
    {
        var result = new HashSet<DayOfWeek>();
        var segments = daysPart.Split(',');
        foreach (var seg in segments)
        {
            var dashIdx = seg.IndexOf('-');
            if (dashIdx > 0)
            {
                var from = ParseSingleDay(seg.Substring(0, dashIdx));
                var to = ParseSingleDay(seg.Substring(dashIdx + 1));
                if (from.HasValue && to.HasValue)
                {
                    var d = from.Value;
                    while (true)
                    {
                        result.Add(d);
                        if (d == to.Value) break;
                        d = (DayOfWeek)(((int)d + 1) % 7);
                    }
                }
            }
            else
            {
                var day = ParseSingleDay(seg);
                if (day.HasValue) result.Add(day.Value);
            }
        }
        return result;
    }

    private static DayOfWeek? ParseSingleDay(string s) => s.Trim().ToLowerInvariant() switch
    {
        "mon" or "monday" => DayOfWeek.Monday,
        "tue" or "tuesday" => DayOfWeek.Tuesday,
        "wed" or "wednesday" => DayOfWeek.Wednesday,
        "thu" or "thursday" => DayOfWeek.Thursday,
        "fri" or "friday" => DayOfWeek.Friday,
        "sat" or "saturday" => DayOfWeek.Saturday,
        "sun" or "sunday" => DayOfWeek.Sunday,
        _ => null
    };
}
