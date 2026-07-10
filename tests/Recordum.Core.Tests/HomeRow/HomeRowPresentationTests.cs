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
    [InlineData(HabitType.State, "stateAvg7", KeyFigureKind.StateAverage7)]
    [InlineData(HabitType.State, "stateValue", KeyFigureKind.StateValue)]
    public void ResolveKeyFigure_honors_valid_explicit_choice(HabitType type, string id, KeyFigureKind expected)
    {
        HomeRowPresentation.ResolveKeyFigure(type, new HabitUiPrefs { KeyFigure = id })
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(HabitType.Action, "counterTotal")]   // not allowed for Action → default
    [InlineData(HabitType.Counter, "clock")]         // not allowed for Counter → default
    [InlineData(HabitType.State, "bogus")]           // unknown id → default
    public void ResolveKeyFigure_unknown_or_disallowed_id_falls_back_to_default(HabitType type, string id)
    {
        var expected = HomeRowPresentation.ResolveKeyFigure(type, null);
        HomeRowPresentation.ResolveKeyFigure(type, new HabitUiPrefs { KeyFigure = id })
            .Should().Be(expected);
    }

    // ---- Strip ----

    [Theory]
    [InlineData(HabitType.Action, HomeStrip.Days7)]
    [InlineData(HabitType.Counter, HomeStrip.Days7)]
    [InlineData(HabitType.Resistance, HomeStrip.Days14)]
    [InlineData(HabitType.Journal, HomeStrip.Days7)]
    [InlineData(HabitType.State, HomeStrip.Days7)]
    public void ResolveStrip_null_prefs_returns_type_default(HabitType type, HomeStrip expected)
    {
        HomeRowPresentation.ResolveStrip(type, null).Should().Be(expected);
        HomeRowPresentation.ResolveStrip(type, new HabitUiPrefs()).Should().Be(expected);
    }

    [Theory]
    [InlineData(HomeStrip.None)]
    [InlineData(HomeStrip.Days7)]
    [InlineData(HomeStrip.Days14)]
    public void ResolveStrip_honors_explicit_choice(HomeStrip strip)
    {
        HomeRowPresentation.ResolveStrip(HabitType.Action, new HabitUiPrefs { Strip = strip })
            .Should().Be(strip);
    }
}
