using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Xunit;

namespace Recordum.Core.Tests.HomeRow;

public class HomeRowPresentationTests
{
    // ---- Key figure: per-type defaults (null prefs) ----

    [Theory]
    [InlineData(HabitType.Action, KeyFigureKind.Streak)]
    [InlineData(HabitType.Counter, KeyFigureKind.CounterTotal)]
    [InlineData(HabitType.Resistance, KeyFigureKind.LiveClock)]
    [InlineData(HabitType.Journal, KeyFigureKind.JournalToday)]
    [InlineData(HabitType.State, KeyFigureKind.StateValue)]
    public void ResolveKeyFigure_null_prefs_returns_type_default(HabitType type, KeyFigureKind expected)
    {
        HomeRowPresentation.ResolveKeyFigure(type, null).Should().Be(expected);
        HomeRowPresentation.ResolveKeyFigure(type, new HabitUiPrefs()).Should().Be(expected);
    }

    // ---- Key figure: explicit valid choices ----

    [Theory]
    [InlineData(HabitType.Action, "doneToday", KeyFigureKind.DoneToday)]
    [InlineData(HabitType.Action, "streak", KeyFigureKind.Streak)]
    [InlineData(HabitType.Action, "none", KeyFigureKind.None)]
    [InlineData(HabitType.Counter, "streak", KeyFigureKind.Streak)]
    [InlineData(HabitType.Counter, "counterTotal", KeyFigureKind.CounterTotal)]
    [InlineData(HabitType.Resistance, "clock", KeyFigureKind.LiveClock)]
    [InlineData(HabitType.Journal, "journalWeek", KeyFigureKind.JournalEntriesWeek)]
    [InlineData(HabitType.State, "stateValue", KeyFigureKind.StateValue)]
    public void ResolveKeyFigure_honors_valid_explicit_choice(HabitType type, string id, KeyFigureKind expected)
    {
        HomeRowPresentation.ResolveKeyFigure(type, new HabitUiPrefs { KeyFigure = id })
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(HabitType.Action, "counterTotal")]   // not allowed for Action → default
    [InlineData(HabitType.Counter, "clock")]         // not allowed for Counter → default
    [InlineData(HabitType.State, "stateAvg7")]       // deferred, not offered → default
    [InlineData(HabitType.State, "bogus")]           // unknown id → default
    public void ResolveKeyFigure_unknown_or_disallowed_id_falls_back_to_default(HabitType type, string id)
    {
        var expected = HomeRowPresentation.ResolveKeyFigure(type, null);
        HomeRowPresentation.ResolveKeyFigure(type, new HabitUiPrefs { KeyFigure = id })
            .Should().Be(expected);
    }

    // ---- Strip ----

    // GIO-3 (2026-11-13): quanti giorni si vedono lo decide la Home dalla larghezza; l'abitudine dice solo se la striscia
    // c'è. Ogni tipo la ha, ed è lunga quanto i giorni caricati (14).
    [Fact]
    public void ResolveStrip_without_prefs_shows_the_strip()
    {
        HomeRowPresentation.ResolveStrip(null).Should().Be(HomeStrip.Days14);
        HomeRowPresentation.ResolveStrip(new HabitUiPrefs()).Should().Be(HomeStrip.Days14);
    }

    [Theory]
    [InlineData(HomeStrip.None, HomeStrip.None)]
    [InlineData(HomeStrip.Days7, HomeStrip.Days14)]   // salvato prima di GIO-3: vale come "sì"
    [InlineData(HomeStrip.Days14, HomeStrip.Days14)]
    public void ResolveStrip_is_yes_or_no(HomeStrip saved, HomeStrip expected)
    {
        HomeRowPresentation.ResolveStrip(new HabitUiPrefs { Strip = saved }).Should().Be(expected);
    }

    // ---- Strip style (2.1-bis: per-type fixed visual, spec wave-2 §1) ----

    [Theory]
    [InlineData(HabitType.Action, StripStyle.Squares)]
    [InlineData(HabitType.Counter, StripStyle.Bars)]
    [InlineData(HabitType.Resistance, StripStyle.Band)]
    [InlineData(HabitType.Journal, StripStyle.Dots)]
    [InlineData(HabitType.State, StripStyle.Ramp)]
    public void ResolveStripStyle_is_fixed_per_type(HabitType type, StripStyle expected)
    {
        HomeRowPresentation.ResolveStripStyle(type).Should().Be(expected);
    }

    [Fact]
    public void ResolveStripStyle_unknown_type_falls_back_to_squares()
    {
        HomeRowPresentation.ResolveStripStyle((HabitType)99).Should().Be(StripStyle.Squares);
    }
}
