using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
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
    [Fact]
    public async Task LoadAsync_builds_91_cells_ending_today()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 0, 0, 0, TimeSpan.Zero));
        var cs = new FakeCompletionService();
        var habitId = Guid.NewGuid();
        cs.HeatmapByHabit[habitId] = [new HeatmapCell(new DateOnly(2026, 5, 20), 1)];
        var sut = new HeatmapViewModel(cs, clock);

        await sut.LoadAsync(habitId);

        sut.Cells.Should().HaveCount(91);
        sut.Cells[^1].Date.Should().Be(new DateOnly(2026, 5, 21));
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 5, 20)).Count.Should().Be(1);
        sut.Cells.Where(c => c.Date != new DateOnly(2026, 5, 20)).Should().OnlyContain(c => c.Count == 0);
    }

    [Fact]
    public void ClickCellCommand_emits_DateClicked_with_cell_date()
    {
        var sut = new HeatmapViewModel(new FakeCompletionService(), new FakeClock(new DateTimeOffset(2026, 5, 21, 0, 0, 0, TimeSpan.Zero)));
        DateOnly? requested = null;
        sut.DateClicked += (_, d) => requested = d;

        sut.ClickCellCommand.Execute(new HeatmapCellViewModel(new DateOnly(2026, 5, 10), 0));

        requested.Should().Be(new DateOnly(2026, 5, 10));
    }

    [Fact]
    public void ClickCellCommand_null_cell_is_noop()
    {
        var sut = new HeatmapViewModel(new FakeCompletionService(), new FakeClock());
        var fired = false;
        sut.DateClicked += (_, _) => fired = true;

        sut.ClickCellCommand.Execute(null);

        fired.Should().BeFalse();
    }
}
