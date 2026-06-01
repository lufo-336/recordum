using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Calendar;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Calendar;

public class CalendarNavigatorViewModelTests
{
    private static CalendarNavigatorViewModel NewSut(out FakeClock clock,
        CalendarMarkerProvider? provider = null)
    {
        clock = new FakeClock(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var sut = new CalendarNavigatorViewModel(clock) { CompletedColorHex = "#e08a4a", NoteColorHex = "#e0b341" };
        if (provider is not null) sut.MarkerProvider = provider;
        return sut;
    }

    [Fact]
    public async Task LoadAsync_builds_42_cells_with_correct_in_month_flags()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null);

        sut.Cells.Should().HaveCount(42);
        sut.Cells.Count(c => c.InCurrentMonth).Should().Be(30);
        sut.Cells.First(c => c.InCurrentMonth).Date.Should().Be(new DateOnly(2026, 6, 1));
        sut.Cells.Last(c => c.InCurrentMonth).Date.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public async Task LoadAsync_flags_today_and_future()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null);

        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 15)).IsToday.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 16)).IsFuture.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 14)).IsFuture.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_applies_markers_from_provider()
    {
        CalendarMarkerProvider provider = (y, m, ct) => Task.FromResult(
            new CalendarMonthMarkers(
                Completed: new HashSet<DateOnly> { new(2026, 6, 1), new(2026, 6, 2) },
                Notes: new HashSet<DateOnly> { new(2026, 6, 1), new(2026, 6, 9) }));
        var sut = NewSut(out _, provider);

        await sut.LoadAsync(2026, 6, selected: null);

        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 1)).Completed.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 1)).HasNote.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 2)).Completed.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 2)).HasNote.Should().BeFalse();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 9)).HasNote.Should().BeTrue();
    }

    [Fact]
    public async Task NextMonth_then_PrevMonth_returns_to_start_and_rolls_year()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 12, selected: null);

        await sut.NextMonthCommand.ExecuteAsync(null);
        sut.DisplayYear.Should().Be(2027);
        sut.DisplayMonth.Should().Be(1);

        await sut.PrevMonthCommand.ExecuteAsync(null);
        sut.DisplayYear.Should().Be(2026);
        sut.DisplayMonth.Should().Be(12);
    }

    [Fact]
    public async Task SelectDate_on_past_cell_sets_selection_and_raises_event()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null);
        DateOnly? raised = null;
        sut.SelectedDateChanged += (_, d) => raised = d;

        var cell = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 10));
        sut.SelectDateCommand.Execute(cell);

        sut.SelectedDate.Should().Be(new DateOnly(2026, 6, 10));
        raised.Should().Be(new DateOnly(2026, 6, 10));
        cell.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SelectDate_on_future_cell_is_ignored()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null);
        var future = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 20));

        sut.SelectDateCommand.Execute(future);

        sut.SelectedDate.Should().BeNull();
        future.IsSelected.Should().BeFalse();
    }
}
