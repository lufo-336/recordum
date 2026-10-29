using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.History;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.History;

public class HistoryWiringTests
{
    private static async Task<HabitScreenRig> LoadAsync()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode
        {
            Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "corsa",
            CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        await rig.Vm.LoadAsync(id);
        return rig;
    }

    [Fact]
    public async Task The_map_opens_on_this_month_with_today_selected()
    {
        var rig = await LoadAsync();
        rig.Vm.Period.From.Should().Be(new DateOnly(2026, 10, 1));
        rig.Vm.Map.Shape.Should().Be(MapShape.Month);
        rig.Vm.Map.Cells.Should().HaveCount(42);
        rig.Vm.Map.Cells.Single(c => c.IsSelected).Date.Should().Be(new DateOnly(2026, 10, 28));
    }

    [Fact]
    public async Task Clicking_a_map_day_selects_it_for_recording()
    {
        var rig = await LoadAsync();
        rig.Vm.Map.SelectCommand.Execute(rig.Vm.Map.Cells.Single(c => c.Date == new DateOnly(2026, 10, 27)));
        await Task.Delay(20);

        rig.Vm.SelectedDay.Should().Be(new DateOnly(2026, 10, 27));
        rig.Vm.Map.Cells.Single(c => c.IsSelected).Date.Should().Be(new DateOnly(2026, 10, 27));
    }

    [Fact]
    public async Task Changing_the_period_reshapes_the_map()
    {
        var rig = await LoadAsync();
        rig.Vm.Period.Period = HistoryPeriod.Week;
        await Task.Delay(20);
        rig.Vm.Map.Shape.Should().Be(MapShape.Week);
        rig.Vm.Map.Cells.Should().HaveCount(7);

        rig.Vm.Period.Period = HistoryPeriod.All;
        await Task.Delay(20);
        rig.Vm.Map.Shape.Should().Be(MapShape.YearHeatmaps);
        rig.Vm.Map.Years.Select(y => y.Year).Should().Equal(2026, 2025);
    }

    [Fact]
    public async Task Choosing_another_day_elsewhere_moves_the_map_selection()
    {
        var rig = await LoadAsync();
        await rig.Vm.SelectDayAsync(new DateOnly(2026, 10, 3));
        rig.Vm.Map.Cells.Single(c => c.IsSelected).Date.Should().Be(new DateOnly(2026, 10, 3));
    }
}
