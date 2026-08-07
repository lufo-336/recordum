namespace Recordum.Core.Formatting;

/// <summary>Converts a stored value_seconds into the unit chosen on the metric (U8): hours, minutes,
/// or seconds. Numeric-only (no localized suffix — that belongs to the presentation layer). Used by
/// the aggregates: the Home row total, the "Today" total, the day strip, the goal's actual and the
/// export projection. A single recording is not an aggregate — it reads as a clock via
/// DurationFormat, so it does not come through here.</summary>
public static class SecondsDisplay
{
    /// <summary>The unit codes a duration can be expressed in, lowercase as the unit table stores them.
    /// Single source of truth: the UI filters its unit pickers with this list and <see cref="IsTimeUnit"/>
    /// answers from it, so the two can never disagree about what counts as a time unit.</summary>
    public static readonly IReadOnlyList<string> TimeUnitCodes = ["hours", "minutes", "seconds"];

    public static decimal? ConvertToUnit(int seconds, string? unitCode) => unitCode?.ToLowerInvariant() switch
    {
        "hours" => seconds / 3600m,
        "minutes" => seconds / 60m,
        "seconds" => seconds,
        _ => null,
    };

    /// <summary>True when the code is a unit a duration can be expressed in. Callers use it to tell
    /// "this total was converted" from "this total is still raw seconds" — the latter covers both a
    /// non-time unit and a legacy duration metric with no unit chosen at all.</summary>
    public static bool IsTimeUnit(string? unitCode) =>
        unitCode is not null && TimeUnitCodes.Contains(unitCode, StringComparer.OrdinalIgnoreCase);

    /// <summary>A summed duration expressed in the metric's own unit (7200s on an "hours" metric = 2),
    /// which is the unit the user typed the target/field in. Returns the total untouched when it is not
    /// a duration, when the unit is not a time unit (the total is then still a second count), or when it
    /// falls outside the second-count range — the conversion casts to int and would otherwise throw.
    /// One rule for every aggregate: daily totals, the goal's actual, the export summary.</summary>
    public static decimal ConvertTotal(decimal rawTotal, bool isDurationMetric, string? unitCode)
    {
        if (!isDurationMetric || rawTotal < 0 || rawTotal > int.MaxValue) return rawTotal;
        return ConvertToUnit((int)rawTotal, unitCode) ?? rawTotal;
    }
}
