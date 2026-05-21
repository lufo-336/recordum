using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
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

public class CounterQuickAddViewModelTests
{
    [Fact]
    public async Task LoadMetricsAsync_selects_first_metric_by_SortOrder()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var m0 = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "count", Type = MetricType.Integer, SortOrder = 0 };
        var m1 = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "second", Type = MetricType.Integer, SortOrder = 1 };
        metrics.Store[m0.Id] = m0;
        metrics.Store[m1.Id] = m1;
        var sut = new CounterQuickAddViewModel(cs, metrics, new FakeClock())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.Counter },
        };

        await sut.LoadMetricsAsync();

        sut.PrimaryMetric.Should().BeSameAs(m0);
    }

    [Fact]
    public async Task SaveAsync_records_completion_with_one_value_using_primary_metric()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "count", Type = MetricType.Integer, SortOrder = 0 };
        metrics.Store[m.Id] = m;
        var sut = new CounterQuickAddViewModel(cs, metrics, new FakeClock())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.Counter },
        };
        await sut.LoadMetricsAsync();
        sut.Value = 5;

        await sut.SaveAsync();

        cs.LastValues.Should().ContainSingle();
        cs.LastValues![0].MetricDefinitionId.Should().Be(m.Id);
        cs.LastValues[0].ValueInteger.Should().Be(5);
        cs.LastValues[0].SeriesIndex.Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_without_primary_metric_is_noop()
    {
        var cs = new FakeCompletionService();
        var sut = new CounterQuickAddViewModel(cs, new FakeMetricDefinitionService(), new FakeClock())
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter },
        };

        await sut.SaveAsync();

        cs.LastRecord.Should().BeNull();
    }
}

public class JournalQuickAddViewModelTests
{
    [Fact]
    public async Task SaveAsync_empty_notes_sets_ErrorMessage_and_skips_record()
    {
        var cs = new FakeCompletionService();
        var sut = new JournalQuickAddViewModel(cs, new FakeClock())
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Journal },
            Notes = "",
        };

        await sut.SaveAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        cs.LastRecord.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_with_notes_records_completion()
    {
        var cs = new FakeCompletionService();
        var sut = new JournalQuickAddViewModel(cs, new FakeClock())
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Journal },
            Notes = "Hello.",
        };

        await sut.SaveAsync();

        cs.LastRecord!.Notes.Should().Be("Hello.");
    }
}

public class StateQuickAddViewModelTests
{
    [Fact]
    public async Task LoadMetricsAsync_parses_LabelsJson_into_ScaleLabels()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Type = MetricType.Integer, LabelsJson = "[\"Low\",\"Mid\",\"High\"]", SortOrder = 0 };
        metrics.Store[m.Id] = m;
        var sut = new StateQuickAddViewModel(cs, metrics, new FakeClock())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.State },
        };

        await sut.LoadMetricsAsync();

        sut.ScaleLabels.Should().Equal("Low", "Mid", "High");
    }

    [Fact]
    public async Task SaveAsync_records_CompletionValue_with_selected_scale_index_as_integer()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Type = MetricType.Integer, LabelsJson = "[\"Low\",\"Mid\",\"High\"]", SortOrder = 0 };
        metrics.Store[m.Id] = m;
        var sut = new StateQuickAddViewModel(cs, metrics, new FakeClock())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.State },
        };
        await sut.LoadMetricsAsync();
        sut.SelectedScaleIndex = 2;

        await sut.SaveAsync();

        cs.LastValues!.Single().ValueInteger.Should().Be(2);
    }

    [Fact]
    public async Task SaveAsync_without_selected_index_is_noop()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        metrics.Store[Guid.NewGuid()] = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Type = MetricType.Integer, SortOrder = 0 };
        var sut = new StateQuickAddViewModel(cs, metrics, new FakeClock())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.State },
        };
        await sut.LoadMetricsAsync();

        await sut.SaveAsync();

        cs.LastRecord.Should().BeNull();
    }
}

public class ResistanceQuickAddViewModelTests
{
    private static readonly int[] ExpectedSeriesIndices = [0, 0, 1, 1];


    private static (FakeCompletionService cs, FakeMetricDefinitionService metrics, ResistanceQuickAddViewModel sut, Guid habitId, MetricDefinition reps, MetricDefinition load)
        CreateSut()
    {
        var cs = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var habitId = Guid.NewGuid();
        var reps = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "reps", Type = MetricType.Integer, SortOrder = 0 };
        var load = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "load", Type = MetricType.Decimal, SortOrder = 1 };
        metrics.Store[reps.Id] = reps;
        metrics.Store[load.Id] = load;
        var sut = new ResistanceQuickAddViewModel(cs, metrics, new FakeClock())
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.Resistance },
        };
        return (cs, metrics, sut, habitId, reps, load);
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
}
