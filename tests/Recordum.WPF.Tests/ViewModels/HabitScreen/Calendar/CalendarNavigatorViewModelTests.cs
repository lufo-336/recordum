using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;
using Xunit;
using Recordum.Core.Time;

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
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);

        sut.Cells.Should().HaveCount(42);
        sut.Cells.Count(c => c.InCurrentMonth).Should().Be(30);
        sut.Cells.First(c => c.InCurrentMonth).Date.Should().Be(new DateOnly(2026, 6, 1));
        sut.Cells.Last(c => c.InCurrentMonth).Date.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public async Task JumpAsync_before_LoadAsync_is_a_safe_noop()
    {
        // Completion/note events can fire JumpAsync on a tab whose calendar hasn't loaded yet
        // (DisplayYear/Month still 0). BuildMonthGrid(0,0) would throw "un-representable DateTime".
        var sut = NewSut(out _);

        var act = async () => await sut.JumpAsync();

        await act.Should().NotThrowAsync();
        sut.Cells.Should().BeEmpty();   // no grid built before an explicit LoadAsync
    }

    [Fact]
    public async Task LoadAsync_flags_today_and_future()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);

        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 15)).IsToday.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 16)).IsFuture.Should().BeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 14)).IsFuture.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_applies_markers_from_provider()
    {
        CalendarMarkerProvider provider = (from, to, ct) => Task.FromResult(
            new CalendarMarkers(new[]
            {
                new CalendarDayMarker(new DateOnly(2026, 6, 1), TintStep: 4, Number: null, NoteCount: 1),
                new CalendarDayMarker(new DateOnly(2026, 6, 2), TintStep: 4, Number: "2×", NoteCount: 0),
                new CalendarDayMarker(new DateOnly(2026, 6, 9), TintStep: 0, Number: null, NoteCount: 3),
            }));
        var sut = NewSut(out _, provider);

        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);

        var c1 = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 1));
        c1.TintStep.Should().Be(4);
        c1.HasNote.Should().BeTrue();
        c1.ShowNoteCount.Should().BeFalse();

        var c2 = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 2));
        c2.TintStep.Should().Be(4);
        c2.Number.Should().Be("2×");
        c2.HasNumber.Should().BeTrue();
        c2.HasNote.Should().BeFalse();

        var c9 = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 9));
        c9.TintStep.Should().Be(0);
        c9.NoteCount.Should().Be(3);
        c9.HasNote.Should().BeTrue();
        c9.ShowNoteCount.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyMarkers_sets_tint_number_and_notecount()
    {
        CalendarMarkerProvider provider = (from, to, ct) => Task.FromResult(
            new CalendarMarkers(new[]
            {
                new CalendarDayMarker(new DateOnly(2026, 6, 2), TintStep: 3, Number: "3", NoteCount: 0),
                new CalendarDayMarker(new DateOnly(2026, 6, 4), TintStep: 0, Number: null, NoteCount: 2),
            }));
        var sut = NewSut(out _, provider);

        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);

        var c2 = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 2));
        c2.TintStep.Should().Be(3);
        c2.Number.Should().Be("3");
        c2.NoteCount.Should().Be(0);
        var c4 = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 4));
        c4.TintStep.Should().Be(0);
        c4.NoteCount.Should().Be(2);
    }

    [Fact]
    public void SelectableYears_span_at_least_15_years_back()
    {
        var sut = NewSut(out _);
        sut.SelectableYears.Should().Contain(2011);
        sut.SelectableYears.Should().Contain(2026);
    }

    [Fact]
    public async Task NextMonth_then_PrevMonth_returns_to_start_and_rolls_year()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 12, selected: null, DayBoundary.Utc);

        await sut.NextCommand.ExecuteAsync(null);
        sut.DisplayYear.Should().Be(2027);
        sut.DisplayMonth.Should().Be(1);

        await sut.PrevCommand.ExecuteAsync(null);
        sut.DisplayYear.Should().Be(2026);
        sut.DisplayMonth.Should().Be(12);
    }

    [Fact]
    public async Task SelectDate_on_past_cell_sets_selection_and_raises_event()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);
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
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);
        var future = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 20));

        sut.SelectDateCommand.Execute(future);

        sut.SelectedDate.Should().BeNull();
        future.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task JumpAsync_preserves_selection_on_rebuilt_cells()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);
        sut.SelectDateCommand.Execute(sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 10)));

        await sut.JumpAsync();

        // Cells are rebuilt by JumpAsync; the new cell for the selected date must be marked selected.
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 10)).IsSelected.Should().BeTrue();
        sut.SelectedDate.Should().Be(new DateOnly(2026, 6, 10));
    }

    [Fact]
    public async Task WeekView_builds_seven_cells_around_the_selected_date()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: new DateOnly(2026, 6, 10), DayBoundary.Utc);

        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Week);

        sut.Cells.Should().HaveCount(7);
        sut.Cells.Should().Contain(c => c.Date == new DateOnly(2026, 6, 10));
        sut.Cells.Select(c => c.Date).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task YearView_builds_twelve_month_blocks_each_a_six_by_seven_grid()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);

        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Year);

        sut.MonthBlocks.Should().HaveCount(12);
        sut.MonthBlocks.Should().OnlyContain(b => b.Cells.Count == 42);
        sut.Cells.Should().BeEmpty();

        var june = sut.MonthBlocks.Single(b => b.Month == 6);
        june.Cells.Count(c => c.InCurrentMonth).Should().Be(30);
        june.Cells.Should().Contain(c => c.Date == new DateOnly(2026, 6, 30) && c.InCurrentMonth);

        // The whole year is covered: every day of 2026 appears as an in-month cell in exactly one block.
        sut.MonthBlocks.Sum(b => b.Cells.Count(c => c.InCurrentMonth)).Should().Be(365); // 2026 is not a leap year
    }

    [Fact]
    public async Task Next_in_WeekView_advances_by_seven_days()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: new DateOnly(2026, 6, 10), DayBoundary.Utc);
        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Week);
        var firstBefore = sut.Cells[0].Date;

        await sut.NextCommand.ExecuteAsync(null);

        sut.Cells[0].Date.Should().Be(firstBefore.AddDays(7));
        sut.Cells.Should().HaveCount(7);
    }

    [Fact]
    public async Task Next_in_YearView_advances_by_one_year()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);
        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Year);

        await sut.NextCommand.ExecuteAsync(null);

        sut.DisplayYear.Should().Be(2027);
        sut.MonthBlocks.Single(b => b.Month == 1).Cells
            .Should().Contain(c => c.Date == new DateOnly(2027, 1, 1) && c.InCurrentMonth);
    }

    [Fact]
    public async Task YearHeatmap_toggle_builds_378_week_grouped_cells()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);
        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Year);

        await sut.SetYearHeatmapCommand.ExecuteAsync(true);

        sut.YearAsHeatmap.Should().BeTrue();
        sut.YearWeeks.Should().HaveCount(54); // 54 (not 53) so Dec 31 is always covered — CR22
        sut.YearWeeks.Should().OnlyContain(w => w.Count == 7);
        sut.Cells.Should().HaveCount(378);
        sut.MonthBlocks.Should().BeEmpty();
    }

    [Fact]
    public async Task YearHeatmap_includes_Dec_31_of_a_leap_year_whose_Jan1_is_on_the_week_edge()
    {
        // CR22: with 53 weeks, a leap year whose Jan 1 sits on the last column of the week (leading==6)
        // dropped Dec 31 (last cell = Jan1+364 < Dec 31 = Jan1+365). Pick such a year for the current
        // culture and assert the 31st is rendered.
        int firstDow = (int)System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int year = Enumerable.Range(2012, 60).First(y =>
            DateTime.IsLeapYear(y) && (((int)new DateOnly(y, 1, 1).DayOfWeek - firstDow + 7) % 7) == 6);

        var sut = NewSut(out _);
        await sut.LoadAsync(year, 6, selected: null, DayBoundary.Utc);
        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Year);
        await sut.SetYearHeatmapCommand.ExecuteAsync(true);

        sut.Cells.Should().Contain(c => c.Date == new DateOnly(year, 12, 31));
    }

    [Fact]
    public async Task YearHeatmap_back_to_months_restores_blocks()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);
        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Year);
        await sut.SetYearHeatmapCommand.ExecuteAsync(true);

        await sut.SetYearHeatmapCommand.ExecuteAsync(false);

        sut.MonthBlocks.Should().HaveCount(12);
        sut.YearWeeks.Should().BeEmpty();
        sut.Cells.Should().BeEmpty();
    }

    [Fact]
    public async Task SetViewMode_back_to_Month_restores_42_cells_and_keeps_selection()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: new DateOnly(2026, 6, 10), DayBoundary.Utc);
        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Year);

        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Month);

        sut.Cells.Should().HaveCount(42);
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 10)).IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task Selecting_a_completed_day_keeps_both_tint_and_selection_independent()
    {
        // F0-13: the selected day (ring) must not be confused with a completed day (fill). The VM keeps
        // TintStep and IsSelected as independent signals so the view can render a ring over the tint.
        CalendarMarkerProvider provider = (from, to, ct) => Task.FromResult(
            new CalendarMarkers(new[]
            {
                new CalendarDayMarker(new DateOnly(2026, 6, 10), TintStep: 4, Number: null, NoteCount: 0),
            }));
        var sut = NewSut(out _, provider);
        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);

        var cell = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 10));
        sut.SelectDateCommand.Execute(cell);

        cell.TintStep.Should().BeGreaterThan(0);   // completion fill survives
        cell.IsSelected.Should().BeTrue();          // selection ring is a separate signal
    }

    [Fact]
    public void DayCell_TooltipText_includes_the_date()
    {
        var cell = new CalendarDayCellViewModel(new DateOnly(2026, 6, 10), inCurrentMonth: true, isToday: false, isFuture: false);
        cell.TooltipText.Should().Contain("2026");
        cell.TooltipText.Should().Contain("10");
    }
}
