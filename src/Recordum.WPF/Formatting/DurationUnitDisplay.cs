using System.Globalization;
using Recordum.Core.Formatting;
using Recordum.WPF.Resources;

namespace Recordum.WPF.Formatting;

/// <summary>Localized rendering of a stored value_seconds in the metric's chosen time unit (U8):
/// "7.5 hours" / "7,5 ore" / "90 min" / "45 sec". The unit word comes from the .resx TimeUnit_*
/// strings so the Cronologia/Records label matches the localized time-unit selector in the quick-add
/// (Core's SecondsDisplay is numeric-only and can't reach the resources). Null when the code is not a
/// time unit → caller falls back to the bare h:mm rendering.</summary>
public static class DurationUnitDisplay
{
    public static string? InUnit(int seconds, string? unitCode)
    {
        if (SecondsDisplay.ConvertToUnit(seconds, unitCode) is not { } v) return null;
        var label = unitCode!.ToLowerInvariant() switch
        {
            "hours" => Strings.TimeUnit_Hours,
            "minutes" => Strings.TimeUnit_Minutes,
            "seconds" => Strings.TimeUnit_Seconds,
            _ => (string?)null,
        };
        if (label is null) return null;
        return v.ToString("0.##", CultureInfo.CurrentCulture) + " " + label;
    }
}
