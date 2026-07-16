using System.Globalization;

namespace Recordum.Core.Formatting;

/// <summary>Renders a stored value_seconds in the unit chosen on the metric (U8): "7.5 hours",
/// "90 minutes", "45 seconds". Null when the unit code is not a time unit — callers fall back to
/// their h:mm formatting. Shared by the WPF records list and the export projection.</summary>
public static class SecondsDisplay
{
    public static decimal? ConvertToUnit(int seconds, string? unitCode) => unitCode?.ToLowerInvariant() switch
    {
        "hours" => seconds / 3600m,
        "minutes" => seconds / 60m,
        "seconds" => seconds,
        _ => null,
    };

    public static string? InUnit(int seconds, string? unitCode)
    {
        if (ConvertToUnit(seconds, unitCode) is not { } v) return null;
        return v.ToString("0.##", CultureInfo.CurrentCulture) + " " + unitCode!.ToLowerInvariant();
    }
}
