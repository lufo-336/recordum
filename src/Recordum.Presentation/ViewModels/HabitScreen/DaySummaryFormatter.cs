using System.Globalization;
using Recordum.Core.Enums;

namespace Recordum.Presentation.ViewModels.HabitScreen;

/// <summary>
/// Builds the human-readable magnitude label for the Abitudine day-summary strip, enriching the
/// per-type daily aggregate with the primary metric's unit (Counter) or scale label (State) so it
/// reads "4 km" / "Good" instead of a bare "4". Returns null when there is nothing to show.
/// The calendar cell keeps its own bare-number rule (no units — space constrained).
/// </summary>
public static class DaySummaryFormatter
{
    /// <param name="durationText">Pre-rendered h:mm:ss for a Counter whose duration total is still raw
    /// seconds (no time unit to convert into). Wins over the number+unit form, but never resurrects a
    /// day with nothing in it.</param>
    public static string? Label(HabitType type, decimal mag, string? unit, IReadOnlyList<string>? scaleLabels,
        string? durationText = null)
    {
        if (mag <= 0) return null;
        if (durationText is not null) return durationText;
        return type switch
        {
            HabitType.State => ScaleLabel(mag, scaleLabels) ?? FormatMag(mag),
            HabitType.Counter => unit is { Length: > 0 } ? $"{FormatMag(mag)} {unit}" : FormatMag(mag),
            HabitType.Resistance => mag > 1 ? FormatMag(mag) : null,
            _ => mag > 1 ? $"{FormatMag(mag)}×" : null, // Action/Journal: hidden unless multiple
        };
    }

    private static string? ScaleLabel(decimal mag, IReadOnlyList<string>? labels)
    {
        if (labels is not { Count: > 0 }) return null;
        var val = (int)mag; // 1-based scale value
        return val >= 1 && val <= labels.Count && !string.IsNullOrWhiteSpace(labels[val - 1]) ? labels[val - 1] : null;
    }

    private static string FormatMag(decimal mag) =>
        mag == Math.Truncate(mag)
            ? ((long)mag).ToString(CultureInfo.CurrentCulture)
            : mag.ToString("0.#", CultureInfo.CurrentCulture);
}
