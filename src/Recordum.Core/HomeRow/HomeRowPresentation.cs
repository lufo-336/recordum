using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Core.HomeRow;

/// <summary>
/// The kind of key figure shown in a Home row's right zone, resolved from the habit
/// type + per-habit <see cref="HabitUiPrefs"/>. Pure presentation logic (no I/O), so it
/// is reused by the row VM and the builder preview.
/// </summary>
public enum KeyFigureKind
{
    None,
    Streak,
    DoneToday,
    CounterTotal,
    LiveClock,
    StateValue,
    JournalToday,
    JournalEntriesWeek,
    StateAverage7,
}

/// <summary>
/// Resolves the per-type Home-row key figure and strip length from a habit's
/// <see cref="HabitType"/> and its (optional) <see cref="HabitUiPrefs"/>. Per-type
/// defaults come from the Onda 2.1 matrix (spec §1); an explicit, type-allowed pref
/// wins, anything unknown or disallowed falls back to the default.
/// </summary>
public static class HomeRowPresentation
{
    /// <summary>Maps a persisted key-figure id to its kind. Ids are the stable strings written to <see cref="HabitUiPrefs.KeyFigure"/>.</summary>
    private static readonly Dictionary<string, KeyFigureKind> IdToKind =
        new Dictionary<string, KeyFigureKind>(StringComparer.Ordinal)
        {
            ["none"] = KeyFigureKind.None,
            ["streak"] = KeyFigureKind.Streak,
            ["doneToday"] = KeyFigureKind.DoneToday,
            ["counterTotal"] = KeyFigureKind.CounterTotal,
            ["clock"] = KeyFigureKind.LiveClock,
            ["stateValue"] = KeyFigureKind.StateValue,
            ["stateAvg7"] = KeyFigureKind.StateAverage7,
            ["journalToday"] = KeyFigureKind.JournalToday,
            ["journalWeek"] = KeyFigureKind.JournalEntriesWeek,
        };

    /// <summary>The key figures each type may show — the first entry is the default. Drives both resolution and the Configure option list.</summary>
    public static IReadOnlyList<KeyFigureKind> AllowedKeyFigures(HabitType type) => type switch
    {
        HabitType.Action => new[] { KeyFigureKind.Streak, KeyFigureKind.DoneToday, KeyFigureKind.None },
        HabitType.Counter => new[] { KeyFigureKind.CounterTotal, KeyFigureKind.Streak, KeyFigureKind.None },
        HabitType.Resistance => new[] { KeyFigureKind.LiveClock, KeyFigureKind.None },
        HabitType.Journal => new[] { KeyFigureKind.JournalToday, KeyFigureKind.JournalEntriesWeek, KeyFigureKind.None },
        HabitType.State => new[] { KeyFigureKind.StateValue, KeyFigureKind.StateAverage7, KeyFigureKind.None },
        _ => new[] { KeyFigureKind.None },
    };

    /// <summary>Resolves the key figure a row shows: explicit type-allowed pref wins; otherwise the type default (first allowed).</summary>
    public static KeyFigureKind ResolveKeyFigure(HabitType type, HabitUiPrefs? prefs)
    {
        var allowed = AllowedKeyFigures(type);
        if (prefs?.KeyFigure is { } id
            && IdToKind.TryGetValue(id, out var kind)
            && allowed.Contains(kind))
        {
            return kind;
        }
        return allowed[0];
    }

    /// <summary>Resolves the strip length: explicit pref wins; otherwise the type default (Resistance = 14 days, others = 7).</summary>
    public static HomeStrip ResolveStrip(HabitType type, HabitUiPrefs? prefs)
    {
        if (prefs?.Strip is { } strip)
            return strip;
        return type == HabitType.Resistance ? HomeStrip.Days14 : HomeStrip.Days7;
    }
}
