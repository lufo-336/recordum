using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels;

public class HomeRowViewModelTests
{
    private static HomeTreeRow Sample(Guid id, string name = "x", PinScope scope = PinScope.None, bool archived = false, HabitType? type = HabitType.Action) =>
        new(id, null, name, "#000000", type, null, 0, false, scope, 0, archived, ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    [Fact]
    public void IsGroup_true_when_HabitType_is_null()
    {
        var sut = new HomeRowViewModel(Sample(Guid.NewGuid(), type: null), 0, new FakeHabitNodeService(), new FakePurgeService());
        sut.IsGroup.Should().BeTrue();
    }

    [Fact]
    public async Task SetPinScopeCommand_calls_service()
    {
        var fakeService = new FakeHabitNodeService();
        var id = Guid.NewGuid();
        var sut = new HomeRowViewModel(Sample(id), 0, fakeService, new FakePurgeService());

        await sut.SetPinScopeCommand.ExecuteAsync(PinScope.Global);

        fakeService.PinScopeCalls.Should().ContainSingle().Which.Should().Be((id, PinScope.Global));
        sut.PinScope.Should().Be(PinScope.Global);
    }

    [Fact]
    public async Task SoftDeleteCommand_calls_service()
    {
        var fakeService = new FakeHabitNodeService();
        var id = Guid.NewGuid();
        var sut = new HomeRowViewModel(Sample(id), 0, fakeService, new FakePurgeService());

        await sut.SoftDeleteCommand.ExecuteAsync(null);

        fakeService.SoftDeleteCalls.Should().Contain(id);
    }

    [Fact]
    public async Task CommitRename_rolls_back_on_service_failure()
    {
        var fakeService = new FakeHabitNodeService();
        var id = Guid.NewGuid();
        fakeService.Store[id] = new HabitNode { Id = id, Name = "old" };
        var sut = new HomeRowViewModel(Sample(id, "old"), 0, fakeService, new FakePurgeService());

        sut.BeginRenameCommand.Execute(null);
        sut.PendingName = "new";
        fakeService.NextException = new InvalidOperationException("boom");

        await sut.CommitRenameCommand.ExecuteAsync(null);

        sut.Name.Should().Be("old");
        sut.IsRenaming.Should().BeFalse();
    }

    [Fact]
    public void ContextMenu_visibility_for_Active_habit()
    {
        var sut = new HomeRowViewModel(Sample(Guid.NewGuid(), type: HabitType.Action), 0, new FakeHabitNodeService(), new FakePurgeService());
        sut.CurrentTab = HomeTab.Active;

        sut.ShowRenameMenu.Should().BeTrue();
        sut.ShowPinMenu.Should().BeTrue();
        sut.ShowArchive.Should().BeTrue();
        sut.ShowUnarchive.Should().BeFalse();
        sut.ShowSoftDelete.Should().BeTrue();
        sut.ShowRestore.Should().BeFalse();
        sut.ShowPurge.Should().BeFalse();
    }

    [Fact]
    public void ContextMenu_visibility_for_Active_group_hides_Pin()
    {
        var sut = new HomeRowViewModel(Sample(Guid.NewGuid(), type: null), 0, new FakeHabitNodeService(), new FakePurgeService());
        sut.CurrentTab = HomeTab.Active;

        sut.ShowPinMenu.Should().BeFalse();
        sut.ShowArchive.Should().BeTrue();
        sut.ShowSoftDelete.Should().BeTrue();
    }

    [Fact]
    public void ContextMenu_visibility_for_Archived()
    {
        var sut = new HomeRowViewModel(Sample(Guid.NewGuid(), archived: true), 0, new FakeHabitNodeService(), new FakePurgeService());
        sut.CurrentTab = HomeTab.Archived;

        sut.ShowRenameMenu.Should().BeTrue();
        sut.ShowPinMenu.Should().BeFalse();
        sut.ShowArchive.Should().BeFalse();
        sut.ShowUnarchive.Should().BeTrue();
        sut.ShowSoftDelete.Should().BeTrue();
        sut.ShowRestore.Should().BeFalse();
        sut.ShowPurge.Should().BeFalse();
    }

    [Fact]
    public void ContextMenu_visibility_for_Trash()
    {
        var sut = new HomeRowViewModel(Sample(Guid.NewGuid()), 0, new FakeHabitNodeService(), new FakePurgeService());
        sut.CurrentTab = HomeTab.Trash;

        sut.ShowRenameMenu.Should().BeFalse();
        sut.ShowPinMenu.Should().BeFalse();
        sut.ShowArchive.Should().BeFalse();
        sut.ShowUnarchive.Should().BeFalse();
        sut.ShowSoftDelete.Should().BeFalse();
        sut.ShowRestore.Should().BeTrue();
        sut.ShowPurge.Should().BeTrue();
    }

    [Fact]
    public async Task CommitRename_persists_via_UpdateAsync_on_success()
    {
        var fakeService = new FakeHabitNodeService();
        var id = Guid.NewGuid();
        fakeService.Store[id] = new HabitNode { Id = id, Name = "old" };
        var sut = new HomeRowViewModel(Sample(id, "old"), 0, fakeService, new FakePurgeService());

        sut.BeginRenameCommand.Execute(null);
        sut.PendingName = "new";
        await sut.CommitRenameCommand.ExecuteAsync(null);

        sut.Name.Should().Be("new");
        fakeService.Store[id].Name.Should().Be("new");
        sut.IsRenaming.Should().BeFalse();
    }

    // ---- Onda 2.1: key figure + strip ----

    private static HomeTreeRow CounterRow(Guid id, decimal total, string? unit) =>
        new(id, null, "c", "#000000", HabitType.Counter, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty,
            CompletionsLast14Days: ImmutableArray.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14),
            TodayCounterTotal: total, TodayCounterUnitCode: unit);

    private static HomeTreeRow StateRow(Guid id, int value, string label) =>
        new(id, null, "s", "#000000", HabitType.State, null, 0, true, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty,
            TodayStateValue: value); // GIO-23: l'etichetta la legge Presentation (Task 4); label resta nel chiamante

    [Fact]
    public void Counter_key_figure_shows_total_with_unit()
    {
        var sut = new HomeRowViewModel(CounterRow(Guid.NewGuid(), 22m, "pg"), 0,
            new FakeHabitNodeService(), new FakePurgeService());

        sut.KeyFigureKind.Should().Be(KeyFigureKind.CounterTotal);
        sut.KeyFigureText.Should().Be("22 pg");
        sut.ShowKeyFigure.Should().BeTrue();
    }

    [Fact]
    public void Counter_key_figure_reads_a_unitless_duration_total_as_a_duration()
    {
        // A duration (Seconds) metric with no unit stores raw seconds and has nothing to convert into:
        // the row must read "12:00" like the records list, not the bare "720" (W23-VF2).
        var row = CounterRow(Guid.NewGuid(), 720m, null) with { TodayCounterIsDuration = true };
        var sut = new HomeRowViewModel(row, 0, new FakeHabitNodeService(), new FakePurgeService());

        sut.KeyFigureText.Should().Be("12:00");
    }

    [Fact]
    public void Counter_key_figure_keeps_the_converted_value_when_the_duration_has_a_time_unit()
    {
        // Already converted upstream (90s → 1.5 minutes): the unit label path must stay untouched.
        var row = CounterRow(Guid.NewGuid(), 1.5m, "minutes") with { TodayCounterIsDuration = true };
        var sut = new HomeRowViewModel(row, 0, new FakeHabitNodeService(), new FakePurgeService());

        sut.KeyFigureText.Should().Be("1,5 minutes".Replace(',', System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]));
    }

    [Fact]
    public void State_key_figure_shows_scale_label()
    {
        var sut = new HomeRowViewModel(StateRow(Guid.NewGuid(), 3, "Bene"), 0,
            new FakeHabitNodeService(), new FakePurgeService());

        sut.KeyFigureKind.Should().Be(KeyFigureKind.StateValue);
        sut.KeyFigureText.Should().Be("Bene");
    }

    [Fact]
    public async Task Action_streak_key_figure_fills_after_LoadKeyFigureAsync()
    {
        var id = Guid.NewGuid();
        var aggregator = new FakeStreakAggregator();
        aggregator.Runs.Add(new StreakRun(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 12), 12, IsCurrent: true));
        var sut = new HomeRowViewModel(Sample(id, type: HabitType.Action), 0,
            new FakeHabitNodeService(), new FakePurgeService(),
            uiPrefs: null, streaks: aggregator, clock: new FakeClock());

        sut.KeyFigureKind.Should().Be(KeyFigureKind.Streak);
        sut.KeyFigureText.Should().BeEmpty(); // lazy: not computed until load
        sut.ShowKeyFigure.Should().BeFalse();

        await sut.LoadKeyFigureAsync(DayBoundary.Utc);

        sut.KeyFigureText.Should().Be("×12");
        sut.ShowKeyFigure.Should().BeTrue();
    }

    [Fact]
    public async Task Action_zero_streak_shows_muted_x0_instead_of_hiding_the_chip()
    {
        // 2.1-bis (A13a): a row with no current streak used to hide the chip entirely and the
        // row read as "empty". Now it shows ×0, flagged zero so the view can mute it.
        var sut = new HomeRowViewModel(Sample(Guid.NewGuid(), type: HabitType.Action), 0,
            new FakeHabitNodeService(), new FakePurgeService(),
            uiPrefs: null, streaks: new FakeStreakAggregator(), clock: new FakeClock());

        await sut.LoadKeyFigureAsync(DayBoundary.Utc);

        sut.KeyFigureText.Should().Be("×0");
        sut.ShowKeyFigure.Should().BeTrue();
        sut.IsKeyFigureZero.Should().BeTrue();
    }

    [Fact]
    public async Task Nonzero_streak_is_not_flagged_zero()
    {
        var aggregator = new FakeStreakAggregator();
        aggregator.Runs.Add(new StreakRun(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 12), 12, IsCurrent: true));
        var sut = new HomeRowViewModel(Sample(Guid.NewGuid(), type: HabitType.Action), 0,
            new FakeHabitNodeService(), new FakePurgeService(),
            uiPrefs: null, streaks: aggregator, clock: new FakeClock());

        await sut.LoadKeyFigureAsync(DayBoundary.Utc);

        sut.IsKeyFigureZero.Should().BeFalse();
    }

    // ---- 2.1-bis: per-type strip style + normalized cell fractions ----

    [Theory]
    [InlineData(HabitType.Action, StripStyle.Squares)]
    [InlineData(HabitType.Counter, StripStyle.Bars)]
    [InlineData(HabitType.Resistance, StripStyle.Band)]
    [InlineData(HabitType.Journal, StripStyle.Dots)]
    [InlineData(HabitType.State, StripStyle.Ramp)]
    public void StripVisual_is_the_fixed_per_type_style(HabitType type, StripStyle expected)
    {
        var sut = new HomeRowViewModel(Sample(Guid.NewGuid(), type: type), 0,
            new FakeHabitNodeService(), new FakePurgeService());
        sut.StripVisual.Should().Be(expected);
    }

    [Fact]
    public void StripFractions_counter_bars_are_daily_value_over_period_max()
    {
        // Values (14d): 5 at index 10, 22 today (index 13). The strip keeps every loaded day (GIO-3): the fractions are
        // value/max of the 14 days, 5/22 and 1.0, zeros elsewhere.
        var values = new decimal?[14];
        values[10] = 5m; values[13] = 22m;
        var row = new HomeTreeRow(Guid.NewGuid(), null, "c", "#000000", HabitType.Counter, null, 0, false,
            PinScope.None, 0, false, ImmutableArray.Create(0, 0, 0, 1, 0, 0, 2), ImmutableArray<Guid>.Empty,
            CompletionsLast14Days: ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 2),
            ValuesLast14Days: ImmutableArray.Create(values));

        var sut = new HomeRowViewModel(row, 0, new FakeHabitNodeService(), new FakePurgeService());

        sut.StripFractions.Length.Should().Be(14);
        sut.StripFractions[10].Should().BeApproximately(5d / 22d, 0.001);
        sut.StripFractions[13].Should().Be(1d);
        sut.StripFractions[0].Should().Be(0d);
    }

    [Fact]
    public void StripFractions_action_squares_are_binary_from_counts()
    {
        var row = new HomeTreeRow(Guid.NewGuid(), null, "a", "#000000", HabitType.Action, null, 0, false,
            PinScope.None, 0, false, ImmutableArray.Create(0, 2, 0, 0, 1, 0, 0), ImmutableArray<Guid>.Empty,
            CompletionsLast14Days: ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 1, 0, 0));

        var sut = new HomeRowViewModel(row, 0, new FakeHabitNodeService(), new FakePurgeService());

        sut.StripFractions[8].Should().Be(1d);
        sut.StripFractions[9].Should().Be(0d);
        sut.StripFractions[11].Should().Be(1d);
    }

    [Fact]
    public void StripFractions_state_ramp_is_value_over_period_max()
    {
        var values = new decimal?[14];
        values[12] = 2m; values[13] = 4m;
        var row = new HomeTreeRow(Guid.NewGuid(), null, "s", "#000000", HabitType.State, null, 0, false,
            PinScope.None, 0, false, ImmutableArray.Create(0, 0, 0, 0, 0, 1, 1), ImmutableArray<Guid>.Empty,
            CompletionsLast14Days: ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1),
            ValuesLast14Days: ImmutableArray.Create(values));

        var sut = new HomeRowViewModel(row, 0, new FakeHabitNodeService(), new FakePurgeService());

        sut.StripFractions[12].Should().Be(0.5d);
        sut.StripFractions[13].Should().Be(1d);
        sut.StripFractions[0].Should().Be(0d);
    }

    [Fact]
    public void Every_type_has_the_full_strip()
    {
        var res = new HomeTreeRow(Guid.NewGuid(), null, "r", "#000000", HabitType.Resistance, null, 0, false,
            PinScope.None, 0, false, ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty,
            CompletionsLast14Days: ImmutableArray.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14),
            AbstinenceStatus: Recordum.Core.Abstractions.Services.AbstinenceStatus.Abstaining,
            AbstinenceOpenSinceMs: new FakeClock().UtcNowMs);
        var resVm = new HomeRowViewModel(res, 0, new FakeHabitNodeService(), new FakePurgeService(),
            uiPrefs: null, streaks: null, clock: new FakeClock());

        resVm.StripLength.Should().Be(HomeStrip.Days14);
        resVm.StripCounts.Length.Should().Be(14);
        resVm.IsResistanceClock.Should().BeTrue();

        var counterVm = new HomeRowViewModel(CounterRow(Guid.NewGuid(), 1m, "x"), 0,
            new FakeHabitNodeService(), new FakePurgeService());
        // GIO-3: non più 7 per i tipi diversi dall'astinenza. La Home taglia alla larghezza, uguale per tutte le righe.
        counterVm.StripLength.Should().Be(HomeStrip.Days14);
        counterVm.StripCounts.Length.Should().Be(14);
    }

    [Fact]
    public void Counter_key_figure_uses_unit_symbol_instead_of_code()
    {
        var row = new HomeTreeRow(
            Guid.NewGuid(), null, "c", "#000000", HabitType.Counter, null, 0, false,
            PinScope.None, 0, false, ImmutableArray.Create(0, 0, 0, 0, 0, 0, 1), ImmutableArray<Guid>.Empty,
            TodayCounterTotal: 2m,
            TodayCounterUnitCode: "hours",
            TodayCounterUnitSymbol: "h",
            TodayCounterUnitName: "Hours",
            TodayCounterUnitIsOfficial: true);

        var sut = new HomeRowViewModel(row, 0, new FakeHabitNodeService(), new FakePurgeService());

        sut.KeyFigureText.Should().Be("2 h");
    }
}
