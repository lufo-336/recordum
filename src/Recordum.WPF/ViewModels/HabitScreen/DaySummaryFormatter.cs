using System.Globalization;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>
/// Builds the human-readable magnitude label for the Abitudine day-summary strip, enriching the
/// per-type daily aggregate with the primary metric's unit (Counter) or scale label (State) so it
/// reads "4 km" / "Good" instead of a bare "4". Returns null when there is nothing to show.
/// The calendar cell keeps its own bare-number rule (no units — space constrained).
/// </summary>
public static class DaySummaryFormatter
{
    public static string? Label(HabitType type, decimal mag, string? unit, IReadOnlyList<string>? scaleLabels)
    {
        if (mag <= 0) return null;
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
        return val >= 1 && val <= labels.Count ? labels[val - 1] : null;
    }

    private static string FormatMag(decimal mag) =>
        mag == Math.Truncate(mag)
            ? ((long)mag).ToString(CultureInfo.CurrentCulture)
            : mag.ToString("0.#", CultureInfo.CurrentCulture);
}
