using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.QuickAdd;

public class QuickAddViewModelBaseTests
{
    [Fact]
    public void CompletionDate_defaults_to_clock_today()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 14, 0, 0, TimeSpan.Zero));
        var sut = new ActionQuickAddViewModel(new FakeCompletionService(), clock);

        sut.CompletionDate.Should().Be(new DateTime(2026, 5, 21));
    }
}

public class ActionQuickAddViewModelTests
{
    [Fact]
    public async Task SaveAsync_today_records_completion_with_IsRetroactive_false()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var cs = new FakeCompletionService();
        var sut = new ActionQuickAddViewModel(cs, clock)
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Action },
            CompletionDate = new DateTime(2026, 5, 21),
        };

        await sut.SaveAsync();

        cs.LastRecord!.HabitNodeId.Should().Be(sut.Node!.Id);
        cs.LastRecord.IsRetroactive.Should().BeFalse();
        cs.LastRecord.CompletedAt.Should().Be(clock.UtcNow.UtcDateTime);
    }

    [Fact]
    public async Task SaveAsync_past_date_sets_IsRetroactive_true()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));
        var cs = new FakeCompletionService();
        var sut = new ActionQuickAddViewModel(cs, clock)
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Action },
            CompletionDate = new DateTime(2026, 5, 19),
        };

        await sut.SaveAsync();

        cs.LastRecord!.IsRetroactive.Should().BeTrue();
        cs.LastRecord.CompletedAt.Date.Should().Be(new DateTime(2026, 5, 19));
    }

    [Fact]
    public async Task SaveAsync_raises_CompletionSaved_event()
    {
        var sut = new ActionQuickAddViewModel(new FakeCompletionService(), new FakeClock())
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Action },
        };
        Guid? fired = null;
        sut.CompletionSaved += (_, id) => fired = id;

        await sut.SaveAsync();

        fired.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAsync_without_node_is_noop()
    {
        var cs = new FakeCompletionService();
        var sut = new ActionQuickAddViewModel(cs, new FakeClock());

        await sut.SaveAsync();

        cs.LastRecord.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_captures_exception_into_ErrorMessage()
    {
        var cs = new FakeCompletionService { NextException = new InvalidOperationException("boom") };
        var sut = new ActionQuickAddViewModel(cs, new FakeClock())
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Action },
        };

        await sut.SaveAsync();

        sut.ErrorMessage.Should().Be("boom");
        sut.IsSaving.Should().BeFalse();
    }
}

public class JournalQuickAddViewModelTests
{
    [Fact]
    public async Task SaveAsync_empty_notes_sets_ErrorMessage_and_skips_diary()
    {
        var diary = new FakeDiaryService();
        var sut = new JournalQuickAddViewModel(new FakeCompletionService(), new FakeClock(), diary)
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Journal },
            Notes = "",
        };

        await sut.SaveAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        diary.SaveCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_with_notes_saves_a_diary_entry_for_the_completion_day()
    {
        var diary = new FakeDiaryService();
        var habitId = Guid.NewGuid();
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero));
        var sut = new JournalQuickAddViewModel(new FakeCompletionService(), clock, diary)
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.Journal },
            Notes = "Hello.",
            CompletionDate = new DateTime(2026, 6, 17, 0, 0, 0, DateTimeKind.Utc),
        };

        await sut.SaveAsync();

        diary.SaveCalls.Should().ContainSingle();
        diary.SaveCalls[0].HabitId.Should().Be(habitId);
        diary.SaveCalls[0].Day.Should().Be(new DateOnly(2026, 6, 17));
        diary.SaveCalls[0].Text.Should().Be("Hello.");
    }
}

public class CounterSeriesQuickAddViewModelTests
{
    private static readonly int[] ExpectedSeriesIndices = [0, 0, 1, 1];

    private static MetricDefinition IntMetric(string name, Guid habitId) =>
        new() { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = name, Type = MetricType.Integer, SortOrder = 0 };

    private static MetricDefinition SecondsMetric(Guid habitId) =>
        new() { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "Tempo", Type = MetricType.Seconds, SortOrder = 1 };

    private static (CounterSeriesQuickAddViewModel vm, FakeCompletionService completions) CreateCounterSeriesVm()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var reps = IntMetric("Ripetizioni", habitId);
        var tempo = SecondsMetric(habitId);
        metrics.Store[reps.Id] = reps;
        metrics.Store[tempo.Id] = tempo;
        var vm = new CounterSeriesQuickAddViewModel(cs, metrics, new FakeReferenceData(), new FakeClock(), new FakeActiveTimerService(), new FakeHabitConfigService())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.Counter },
        };
        return (vm, cs);
    }

    private static (CounterSeriesQuickAddViewModel vm, FakeActiveTimerService timers) CreateCounterSeriesVmWithTimers()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var reps = IntMetric("Ripetizioni", habitId);
        var tempo = SecondsMetric(habitId);
        metrics.Store[reps.Id] = reps;
        metrics.Store[tempo.Id] = tempo;
        var timers = new FakeActiveTimerService { StopWithoutRecordingFactory = _ => 95 };
        var vm = new CounterSeriesQuickAddViewModel(cs, metrics, new FakeReferenceData(), new FakeClock(), timers, new FakeHabitConfigService())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.Counter },
        };
        return (vm, timers);
    }

    private static (FakeCompletionService cs, FakeMetricDefinitionService metrics, CounterSeriesQuickAddViewModel sut, Guid habitId, MetricDefinition reps, MetricDefinition load)
        CreateSut()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var reps = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "reps", Type = MetricType.Integer, SortOrder = 0 };
        var load = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "load", Type = MetricType.Decimal, SortOrder = 1 };
        metrics.Store[reps.Id] = reps;
        metrics.Store[load.Id] = load;
        var sut = new CounterSeriesQuickAddViewModel(cs, metrics, new FakeReferenceData(), new FakeClock(), new FakeActiveTimerService(), new FakeHabitConfigService())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.Resistance },
        };
        return (cs, metrics, sut, habitId, reps, load);
    }

    [WpfFact]
    public async Task Cells_carry_unit_label()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var habitId = Guid.NewGuid();
        var unitId = rd.Units[0].Id;
        await metrics.CreateAsync(new MetricDefinition { HabitNodeId = habitId, Name = "Distance", Type = MetricType.Decimal, UnitOfMeasureId = unitId, SortOrder = 0 });
        var sut = new CounterSeriesQuickAddViewModel(cs, metrics, rd, new FakeClock(), new FakeActiveTimerService(), new FakeHabitConfigService())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.Counter },
        };

        await sut.LoadMetricsAsync();

        sut.Series.Should().ContainSingle();
        sut.Series[0].Cells[0].UnitLabel.Should().Be(rd.Units[0].Code);
    }

    [Fact]
    public async Task LoadMetricsAsync_seeds_one_empty_series_with_one_cell_per_metric()
    {
        var (_, _, sut, _, _, _) = CreateSut();

        await sut.LoadMetricsAsync();

        sut.Series.Should().ContainSingle();
        sut.Series[0].Cells.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAsync_emits_one_value_per_filled_cell_per_series_with_correct_SeriesIndex()
    {
        var (cs, _, sut, _, _, _) = CreateSut();
        await sut.LoadMetricsAsync();
        sut.Series[0].Cells[0].IntegerValue = 8;
        sut.Series[0].Cells[1].DecimalValue = 50m;
        sut.AddSeriesCommand.Execute(null);
        sut.Series[1].Cells[0].IntegerValue = 6;
        sut.Series[1].Cells[1].DecimalValue = 60m;

        await sut.SaveAsync();

        cs.LastValues.Should().HaveCount(4);
        cs.LastValues!.Select(v => v.SeriesIndex).Should().BeEquivalentTo(ExpectedSeriesIndices);
    }

    [Fact]
    public async Task SaveAsync_skips_empty_cells()
    {
        var (cs, _, sut, _, _, _) = CreateSut();
        await sut.LoadMetricsAsync();
        sut.Series[0].Cells[0].IntegerValue = 8;
        // Cells[1].DecimalValue left null

        await sut.SaveAsync();

        cs.LastValues.Should().ContainSingle();
        cs.LastValues![0].ValueInteger.Should().Be(8);
    }

    [Fact]
    public async Task SaveAsync_all_cells_empty_sets_ErrorMessage()
    {
        var (cs, _, sut, _, _, _) = CreateSut();
        await sut.LoadMetricsAsync();

        await sut.SaveAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        cs.LastRecord.Should().BeNull();
    }

    [Fact]
    public async Task RemoveSeries_keeps_at_least_one()
    {
        var (_, _, sut, _, _, _) = CreateSut();
        await sut.LoadMetricsAsync();

        sut.RemoveSeriesCommand.Execute(sut.Series[0]);

        sut.Series.Should().ContainSingle();
    }

    [Fact]
    public async Task RemoveSeries_cannot_execute_with_a_single_series_and_re_evaluates_on_add()
    {
        var (_, _, sut, _, _, _) = CreateSut();
        await sut.LoadMetricsAsync();

        sut.RemoveSeriesCommand.CanExecute(sut.Series[0]).Should().BeFalse();

        sut.AddSeriesCommand.Execute(null);
        sut.RemoveSeriesCommand.CanExecute(sut.Series[0]).Should().BeTrue();

        sut.RemoveSeriesCommand.Execute(sut.Series[0]);
        sut.RemoveSeriesCommand.CanExecute(sut.Series[0]).Should().BeFalse();
    }

    [Fact]
    public async Task Save_skips_empty_rows_and_reindexes_series()
    {
        var (vm, completions) = CreateCounterSeriesVm();
        await vm.LoadMetricsAsync();
        vm.AddSeriesCommand.Execute(null);
        vm.AddSeriesCommand.Execute(null);                 // 3 righe
        vm.Series[0].Cells[0].IntegerValue = 14;
        vm.Series[2].Cells[0].IntegerValue = 11;           // la riga 1 resta vuota
        await vm.SaveAsync();
        var indexes = completions.LastValues!.Select(v => v.SeriesIndex).Distinct().OrderBy(i => i).ToList();
        Assert.Equal(new[] { 0, 1 }, indexes);             // reindicizzate senza buchi
    }

    [Fact]
    public async Task Missing_field_in_valid_row_produces_no_value()
    {
        var (vm, completions) = CreateCounterSeriesVm();
        await vm.LoadMetricsAsync();
        vm.Series[0].Cells[0].IntegerValue = 14;           // Ripetizioni sì, Tempo no
        await vm.SaveAsync();
        Assert.Single(completions.LastValues!);            // un solo CompletionValue (spec D6)
    }

    [Fact]
    public async Task Chrono_toggle_fills_the_duration_cell()
    {
        var (vm, timers) = CreateCounterSeriesVmWithTimers();
        await vm.LoadMetricsAsync();
        var cell = vm.Series[0].Cells.First(c => c.IsDuration);

        await vm.ToggleChronoCommand.ExecuteAsync(cell);  // start
        Assert.True(vm.ChronoRunning);

        await vm.ToggleChronoCommand.ExecuteAsync(cell);  // stop → 95s
        Assert.Equal(TimeUnit.Seconds, cell.DurationUnit);
        Assert.Equal(95m, cell.DurationValue);
        Assert.False(vm.ChronoRunning);
        Assert.Single(timers.StopWithoutRecordingCalls);
    }
}

public class CounterTapQuickAddViewModelTests
{
    private static (CounterTapQuickAddViewModel vm, FakeCompletionService completions, FakeClock clock) CreateCounterTapVm()
    {
        var cs = new FakeCompletionService();
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
        var vm = new CounterTapQuickAddViewModel(cs, clock)
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter },
        };
        return (vm, cs, clock);
    }

    [Fact]
    public async Task Save_failure_shows_a_localized_message_not_the_raw_exception()
    {
        // CR21: a raw EF/SQLite error (e.g. "UNIQUE constraint failed") must never leak into the UI.
        var (vm, completions, _) = CreateCounterTapVm();
        completions.NextException = new InvalidOperationException("UNIQUE constraint failed");

        await vm.TapCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be(Strings.QuickAdd_Error_Unexpected);
        vm.ErrorMessage.Should().NotContain("UNIQUE");
    }

    [Fact]
    public async Task Tap_records_bare_completion_and_increments_day_count()
    {
        var (vm, completions, _) = CreateCounterTapVm();
        await vm.LoadDayAsync();

        await vm.TapCommand.ExecuteAsync(null);

        vm.DayCount.Should().Be(1);
        completions.LastValues.Should().BeNull(); // nessun CompletionValue (occorrenza nuda)
    }

    [Fact]
    public async Task UndoLast_deletes_newest_completion_of_the_day()
    {
        var (vm, completions, clock) = CreateCounterTapVm();

        await vm.TapCommand.ExecuteAsync(null);
        var first = completions.Store.Keys.Single();
        clock.Advance(TimeSpan.FromSeconds(1));
        await vm.TapCommand.ExecuteAsync(null);
        var second = completions.Store.Keys.Single(k => k != first);

        await vm.UndoLastCommand.ExecuteAsync(null);

        vm.DayCount.Should().Be(1);
        completions.DeleteCalls.Single().Should().Be(second); // l'ultima registrata
    }
}

public class SeriesRowViewModelTests
{
    private static MetricDefinition IntMetric(string name) =>
        new() { Id = Guid.NewGuid(), Name = name, Type = MetricType.Integer };

    private static MetricDefinition SecondsMetric() =>
        new() { Id = Guid.NewGuid(), Name = "Tempo", Type = MetricType.Seconds };

    [Fact]
    public void Row_is_valid_when_any_cell_has_a_value()
    {
        var row = new SeriesRowViewModel(new[] { IntMetric("Ripetizioni"), SecondsMetric() });
        Assert.False(row.IsValid);
        row.Cells[0].IntegerValue = 14;
        Assert.True(row.IsValid);
    }
}

public class SeriesCellViewModelDurationTests
{
    private static MetricDefinition SecondsMetric() =>
        new() { Id = Guid.NewGuid(), Name = "Tempo", Type = MetricType.Seconds };

    [Fact]
    public void Seconds_cell_parses_duration_into_value_seconds()
    {
        var cell = new SeriesCellViewModel(SecondsMetric()) { DurationUnit = TimeUnit.Minutes, DurationValue = 62.083m };
        var v = cell.BuildValue(0);
        Assert.NotNull(v);
        Assert.Equal(3725, v!.ValueSeconds); // 62.083 min × 60 rounds to 3725s
        Assert.Null(v.ValueInteger);
    }

    [Fact]
    public void Empty_duration_value_yields_null()
    {
        var cell = new SeriesCellViewModel(SecondsMetric());
        Assert.Null(cell.BuildValue(0));
    }

    [Fact]
    public void SetDurationSeconds_round_trips_value()
    {
        var cell = new SeriesCellViewModel(SecondsMetric());
        cell.SetDurationSeconds(3725);
        Assert.Equal(TimeUnit.Seconds, cell.DurationUnit);
        Assert.Equal(3725m, cell.DurationValue);
    }
}
