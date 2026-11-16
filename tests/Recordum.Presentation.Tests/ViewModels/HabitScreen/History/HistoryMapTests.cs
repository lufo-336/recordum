using FluentAssertions;
using Recordum.Core.Abstractions;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;
using Recordum.Presentation.ViewModels.HabitScreen.History;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.History;

public class HistoryMapTests
{
    private static readonly DateOnly Today = new(2026, 10, 28);

    private static HistoryMapViewModel Vm(params CalendarDayMarker[] markers)
        => new((from, to, _) => Task.FromResult(new CalendarMarkers(markers.Where(m => m.Date >= from && m.Date <= to).ToList())));

    [Fact]
    public async Task Week_has_seven_cells()
    {
        var vm = Vm();
        await vm.LoadAsync(new(2026, 10, 26), new(2026, 11, 1), MapShape.Week, Today, null, default);
        vm.Shape.Should().Be(MapShape.Week);
        vm.Cells.Should().HaveCount(7);
        vm.Cells.Count(c => c.IsFuture).Should().Be(4);
        vm.Cells.Single(c => c.IsToday).Date.Should().Be(Today);
    }

    [Fact]
    public async Task Month_marks_days_outside_the_month()
    {
        var vm = Vm();
        await vm.LoadAsync(new(2026, 10, 1), new(2026, 10, 31), MapShape.Month, Today, null, default);
        vm.Cells.Should().HaveCount(42);
        vm.Cells[0].Date.Should().Be(new DateOnly(2026, 9, 28));   // lunedì prima del 1° ottobre
        vm.Cells.Where(c => !c.InRange).Should().OnlyContain(c => c.Date.Month != 10);
        vm.Cells.Count(c => c.InRange).Should().Be(31);
    }

    [Fact]
    public async Task Markers_reach_the_cells()
    {
        var vm = Vm(new CalendarDayMarker(new(2026, 10, 27), 3, "5", 2));
        await vm.LoadAsync(new(2026, 10, 1), new(2026, 10, 31), MapShape.Month, Today, null, default);
        var cell = vm.Cells.Single(c => c.Date == new DateOnly(2026, 10, 27));
        cell.Step.Should().Be(3);
        cell.NoteCount.Should().Be(2);
        cell.Value.Should().Be("5");
    }

    [Fact]
    public async Task Lapse_is_an_empty_marked_cell_not_a_red_day()
    {
        var vm = Vm(
            new CalendarDayMarker(new(2026, 10, 20), 4, null, 0, MarkerTint.Danger),
            new CalendarDayMarker(new(2026, 10, 21), 1, null, 0, MarkerTint.Danger),
            new CalendarDayMarker(new(2026, 10, 22), 3, null, 0));
        await vm.LoadAsync(new(2026, 10, 1), new(2026, 10, 31), MapShape.Month, Today, null, default);

        var lapse = vm.Cells.Single(c => c.Date == new DateOnly(2026, 10, 20));
        lapse.IsLapse.Should().BeTrue();
        lapse.Step.Should().Be(0, "il giorno di una ricaduta non ha la tinta di un giorno pulito (GIO-9)");
        var gap = vm.Cells.Single(c => c.Date == new DateOnly(2026, 10, 21));
        gap.IsLapse.Should().BeFalse();
        gap.Step.Should().Be(0);
        vm.Cells.Single(c => c.Date == new DateOnly(2026, 10, 22)).Step.Should().Be(3);
    }

    [Fact]
    public async Task A_lapse_outside_the_month_is_not_marked()
    {
        // La griglia del mese ha le code dei mesi vicini: lì resta il solo numero attenuato, anche per una ricaduta.
        var vm = Vm(new CalendarDayMarker(new(2026, 9, 30), 4, null, 0, MarkerTint.Danger));
        await vm.LoadAsync(new(2026, 10, 1), new(2026, 10, 31), MapShape.Month, Today, null, default);

        var tail = vm.Cells.Single(c => c.Date == new DateOnly(2026, 9, 30));
        tail.InRange.Should().BeFalse();
        tail.IsLapse.Should().BeFalse();
    }

    [Fact]
    public async Task Future_days_are_not_selectable()
    {
        var vm = Vm();
        await vm.LoadAsync(new(2026, 10, 1), new(2026, 10, 31), MapShape.Month, Today, null, default);
        var picked = new List<DateOnly>();
        vm.DaySelected += (_, d) => picked.Add(d);

        vm.SelectCommand.Execute(vm.Cells.Single(c => c.Date == new DateOnly(2026, 10, 30)));
        vm.SelectCommand.Execute(vm.Cells.Single(c => c.Date == new DateOnly(2026, 10, 27)));

        picked.Should().Equal(new DateOnly(2026, 10, 27));
    }

    [Fact]
    public async Task Selection_follows_set_selected()
    {
        var vm = Vm();
        await vm.LoadAsync(new(2026, 10, 1), new(2026, 10, 31), MapShape.Month, Today, new DateOnly(2026, 10, 5), default);
        vm.Cells.Single(c => c.IsSelected).Date.Should().Be(new DateOnly(2026, 10, 5));

        vm.SetSelected(new DateOnly(2026, 10, 9));
        vm.Cells.Single(c => c.IsSelected).Date.Should().Be(new DateOnly(2026, 10, 9));
    }

    [Fact]
    public async Task Heatmap_is_grouped_in_weeks()
    {
        var vm = Vm();
        await vm.LoadAsync(new(2026, 1, 1), new(2026, 12, 31), MapShape.Heatmap, Today, null, default);
        vm.Weeks.Should().OnlyContain(w => w.Count == 7);
        vm.Weeks[0][0].Date.Should().Be(new DateOnly(2025, 12, 29));
        vm.Weeks.SelectMany(w => w).Count(c => c.InRange).Should().Be(365);
        vm.Cells.Should().BeEmpty();
    }

    [Fact]
    public async Task All_has_one_heatmap_per_year()
    {
        var vm = Vm();
        await vm.LoadAsync(new(2021, 1, 1), Today, MapShape.YearHeatmaps, Today, null, default);
        vm.Years.Select(y => y.Year).Should().Equal(2026, 2025, 2024, 2023, 2022, 2021);
        vm.Years[1].Weeks.SelectMany(w => w).Count(c => c.InRange).Should().Be(365);
    }

    [Fact]
    public async Task All_with_no_data_is_the_current_year()
    {
        var vm = Vm();
        await vm.LoadAsync(new(2026, 1, 1), Today, MapShape.YearHeatmaps, Today, null, default);
        vm.Years.Select(y => y.Year).Should().Equal(2026);
    }

    [Fact]
    public async Task Reloading_replaces_the_shape()
    {
        var vm = Vm();
        await vm.LoadAsync(new(2026, 10, 1), new(2026, 10, 31), MapShape.Month, Today, null, default);
        await vm.LoadAsync(new(2026, 1, 1), Today, MapShape.YearHeatmaps, Today, null, default);
        vm.Cells.Should().BeEmpty();
        vm.Weeks.Should().BeEmpty();
        vm.Years.Should().HaveCount(1);
    }
}
