using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HeatmapCellViewModelTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(7, 3)]
    [InlineData(8, 4)]
    [InlineData(20, 4)]
    public void IntensityStep_maps_count_per_5_step_ramp(int count, int expected)
    {
        var sut = new HeatmapCellViewModel(new DateOnly(2026, 5, 21), count);
        sut.IntensityStep.Should().Be(expected);
    }
}

public class HeatmapViewModelTests
{
    private static HeatmapViewModel NewSut(
        FakeCompletionService? completions = null,
        FakeClock? clock = null,
        FakeGoalService? goals = null,
        FakeHabitConfigService? configs = null) =>
        new(
            completions ?? new FakeCompletionService(),
            clock ?? new FakeClock(),
            new IntensityRampResolver(),
            goals ?? new FakeGoalService(),
            configs ?? new FakeHabitConfigService());

    [Fact]
    public async Task LoadAsync_builds_91_cells_ending_today()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 0, 0, 0, TimeSpan.Zero));
        var cs = new FakeCompletionService();
        var habitId = Guid.NewGuid();
        cs.HeatmapByHabit[habitId] = [new HeatmapCell(new DateOnly(2026, 5, 20), 1)];
        var sut = NewSut(cs, clock);

        await sut.LoadAsync(habitId);

        sut.Cells.Should().HaveCount(91);
        sut.Cells[^1].Date.Should().Be(new DateOnly(2026, 5, 21));
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 5, 20)).Count.Should().Be(1);
        sut.Cells.Where(c => c.Date != new DateOnly(2026, 5, 20)).Should().OnlyContain(c => c.Count == 0);
    }

    [Fact]
    public void ClickCellCommand_emits_DateClicked_with_cell_date()
    {
        var sut = NewSut(clock: new FakeClock(new DateTimeOffset(2026, 5, 21, 0, 0, 0, TimeSpan.Zero)));
        DateOnly? requested = null;
        sut.DateClicked += (_, d) => requested = d;

        sut.ClickCellCommand.Execute(new HeatmapCellViewModel(new DateOnly(2026, 5, 10), 0));

        requested.Should().Be(new DateOnly(2026, 5, 10));
    }

    [Fact]
    public void ClickCellCommand_null_cell_is_noop()
    {
        var sut = NewSut();
        var fired = false;
        sut.DateClicked += (_, _) => fired = true;

        sut.ClickCellCommand.Execute(null);

        fired.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_uses_proportional_ramp_when_goal_active()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
        var cs = new FakeCompletionService();
        var goalSvc = new FakeGoalService();
        var habitId = Guid.NewGuid();
        var goal = new Goal { Id = Guid.NewGuid(), HabitNodeId = habitId, TargetValue = 8m, PeriodType = GoalPeriodType.Open };
        goalSvc.Store[goal.Id] = goal;
        // count 4 with target 8 → ramp 2/4/6/8 → 4 falls in [Step2..Step3) → step 3
        cs.HeatmapByHabit[habitId] = [new HeatmapCell(new DateOnly(2026, 5, 22), 4)];

        var sut = NewSut(cs, clock, goalSvc);
        await sut.LoadAsync(habitId);

        var todayCell = sut.Cells.Last();
        todayCell.IntensityStep.Should().Be(3);
    }
}
