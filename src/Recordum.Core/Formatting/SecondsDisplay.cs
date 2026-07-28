namespace Recordum.Core.Formatting;

/// <summary>Converts a stored value_seconds into the unit chosen on the metric (U8): hours, minutes,
/// or seconds. Numeric-only (no localized suffix — that belongs to the presentation layer): the WPF
/// records list wraps this with a localized unit word (DurationUnitDisplay), the export projection
/// uses the bare number. Null when the unit code is not a time unit — callers fall back to their own
/// h:mm rendering.</summary>
public static class SecondsDisplay
{
    public static decimal? ConvertToUnit(int seconds, string? unitCode) => unitCode?.ToLowerInvariant() switch
    {
        "hours" => seconds / 3600m,
        "minutes" => seconds / 60m,
        "seconds" => seconds,
        _ => null,
    };

    /// <summary>True when the code is a unit a duration can be expressed in. Callers use it to tell
    /// "this total was converted" from "this total is still raw seconds" — the latter covers both a
    /// non-time unit and the far more common case of a duration metric with no unit chosen at all.</summary>
    public static bool IsTimeUnit(string? unitCode) => ConvertToUnit(1, unitCode) is not null;
}
