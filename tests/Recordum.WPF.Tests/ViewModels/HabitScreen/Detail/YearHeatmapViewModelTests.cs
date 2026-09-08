using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Detail;

public sealed class YearHeatmapViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_371_cells_with_today_in_range()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var completions = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var vm = new YearHeatmapViewModel(completions, metrics, new IntensityRampResolver(), new FakeAbstinenceService(), clock);

        var habit = new HabitNode { Id = Guid.NewGuid(), Name = "h", HabitType = HabitType.Action, NodeType = NodeType.Habit };
        await vm.LoadAsync(habit, config: null, activeGoal: null, DayBoundary.Utc);

        Assert.Equal(371, vm.Cells.Count);
        Assert.Contains(vm.Cells, c => c.Date == DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }

    [Fact]
    public async Task LoadAsync_uses_count_for_Action_habit()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var habit = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Action, NodeType = NodeType.Habit };
        var completions = new FakeCompletionService();
        completions.HeatmapByHabit[habit.Id] = new List<HeatmapCell> { new(today, 5) };

        var vm = new YearHeatmapViewModel(completions, new FakeMetricDefinitionService(), new IntensityRampResolver(), new FakeAbstinenceService(), clock);
        await vm.LoadAsync(habit, null, null, DayBoundary.Utc);

        var todayCell = vm.Cells.Single(c => c.Date == today);
        Assert.Equal(3, todayCell.IntensityStep);
    }

    [Fact]
    public async Task LoadAsync_uses_DailySum_for_Counter_with_primary_metric()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var habit = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Counter, NodeType = NodeType.Habit };
        var completions = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var metricId = Guid.NewGuid();
        metrics.Store[metricId] = new MetricDefinition { Id = metricId, HabitNodeId = habit.Id, SortOrder = 0, Name = "v", Type = MetricType.Decimal };
        completions.DailySumByKey[(habit.Id, metricId)] = new List<DailyValue> { new(today, 50m) };

        var goal = new Goal { TargetValue = 100m, PeriodType = GoalPeriodType.Open };
        var vm = new YearHeatmapViewModel(completions, metrics, new IntensityRampResolver(), new FakeAbstinenceService(), clock);
        await vm.LoadAsync(habit, null, goal, DayBoundary.Utc);

        // ramp 25/50/75/100 → 50 ∈ [Step2..Step3) → step 3
        var todayCell = vm.Cells.Single(c => c.Date == today);
        Assert.Equal(3, todayCell.IntensityStep);
    }

    [Fact]
    public async Task LoadAsync_colors_clean_abstinence_days_for_Resistance()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var habit = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Resistance, NodeType = NodeType.Habit };
        var abstinence = new FakeAbstinenceService();
        // Closed period 2026-05-10 → 2026-05-13 (relapse day 05-13). Clean days = 05-10, 05-11, 05-12.
        abstinence.Periods[habit.Id] = new List<AbstinencePeriod>
        {
            new()
            {
                Id = Guid.NewGuid(), HabitNodeId = habit.Id,
                StartedAtMs = new DateTimeOffset(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds(),
                EndedAtMs = new DateTimeOffset(new DateTime(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds(),
            },
        };

        var vm = new YearHeatmapViewModel(new FakeCompletionService(), new FakeMetricDefinitionService(), new IntensityRampResolver(), abstinence, clock);
        await vm.LoadAsync(habit, null, null, DayBoundary.Utc);

        Assert.Equal(4, vm.Cells.Single(c => c.Date == new DateOnly(2026, 5, 11)).IntensityStep); // clean → full
        Assert.Equal(0, vm.Cells.Single(c => c.Date == new DateOnly(2026, 5, 13)).IntensityStep); // relapse day → blank
        Assert.Equal(0, vm.Cells.Single(c => c.Date == new DateOnly(2026, 5, 20)).IntensityStep); // gap after relapse → blank
    }

    [Fact]
    public async Task LoadAsync_marks_future_cells_out_of_range()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var habit = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Action, NodeType = NodeType.Habit };

        var vm = new YearHeatmapViewModel(new FakeCompletionService(), new FakeMetricDefinitionService(), new IntensityRampResolver(), new FakeAbstinenceService(), clock);
        await vm.LoadAsync(habit, null, null, DayBoundary.Utc);

        var future = vm.Cells.Where(c => c.Date > today).ToList();
        Assert.NotEmpty(future);
        Assert.All(future, c => Assert.True(c.IsOutOfRange));
        Assert.All(future, c => Assert.Equal(0, c.IntensityStep));
    }
}
