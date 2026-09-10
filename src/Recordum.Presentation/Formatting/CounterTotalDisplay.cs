using Recordum.Core.Formatting;

namespace Recordum.Presentation.Formatting;

/// <summary>Rendering rule for a Counter's daily total when the primary metric is a duration (U8).
/// A Seconds metric stores value_seconds; when its unit is a time unit the total is converted
/// upstream (90s on "minutes" = 1.5) and the caller's number + unit label is right. When there is no
/// time unit — the default for a "+ Tempo" field, which ships with no unit at all — the total is
/// still raw seconds, and printing it bare contradicts the records list, which renders the very same
/// value through <see cref="DurationFormat"/> ("720" vs "12:00"). This returns the h:mm:ss form for
/// exactly that case (W23-VF2).</summary>
public static class CounterTotalDisplay
{
    /// <summary>The duration rendering of a raw-seconds total, or null when the caller should keep
    /// its own number + unit formatting (non-duration metric, or a unit that already converted it).
    /// Also null for totals outside the second-count range, which cannot be a real duration.</summary>
    public static string? AsRawDuration(bool isDurationMetric, string? unitCode, decimal total)
    {
        if (!isDurationMetric || SecondsDisplay.IsTimeUnit(unitCode)) return null;
        if (total < 0 || total > int.MaxValue) return null;
        return DurationFormat.Format((int)total);
    }
}
