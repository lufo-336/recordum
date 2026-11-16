using System.Collections.Immutable;
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Tests.Abstractions;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Quick;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Home;

/// <summary>La Home ha un giorno corrente (A1): si sceglie in testa, e titolo, conti, righe e log rapido lo seguono senza
/// rileggere il database. Martedì 10 novembre 2026, app a mezzanotte: ieri è lunedì 9.</summary>
public sealed class HomeDayTests : IDisposable
{
    private static readonly DayBoundary AppDay = new(TimeZoneInfo.Utc, TimeOnly.MinValue);
    private static readonly DateOnly Tuesday = new(2026, 11, 10);
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;

    private readonly FakeHabitTreeQueryService _query = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeTickSourceFactory _ticks = new();
    private readonly RecordingHandler _handler = new();

    public HomeDayTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
        // Oggi: fatta solo Acqua (1 di 3). Ieri: Acqua e Corsa (2 di 3). Lettura mai.
        var group = Row("Mattina", null, null, Days());
        _query.HomeRows.Add(group);
        _query.HomeRows.Add(Row("Acqua", group.HabitNodeId, HabitType.Counter, Days(1, 1), doneToday: true) with
        {
            ValuesLast14Days = [.. Enumerable.Repeat<decimal?>(null, 12), 5m, 2m],
            TodayCounterTotal = 2m,
        });
        _query.HomeRows.Add(Row("Corsa", group.HabitNodeId, HabitType.Action, Days(1, 0)));
        _query.HomeRows.Add(Row("Lettura", null, HabitType.Action, Days()));
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private static int[] Days(params int[] last) => [.. Enumerable.Repeat(0, 14 - last.Length), .. last];

    private static HomeTreeRow Row(string name, Guid? parent, HabitType? type, int[] counts, bool doneToday = false) =>
        new(Guid.NewGuid(), parent, name, "#2F7FC1", type, null, 0, doneToday, PinScope.None, 0, false,
            ImmutableArray.Create(counts[^7..]), ImmutableArray<Guid>.Empty,
            CompletionsLast14Days: ImmutableArray.Create(counts));

    private async Task<HomeViewModel> LoadedAsync()
    {
        var sut = new HomeViewModel(_query, new FakeHabitNodeService(), new FakePurgeService(),
            quickActions: new QuickActionHandlerSelector([_handler]),
            clock: _clock, resetTimes: new FakeResetTimeResolver(AppDay), ticks: _ticks);
        await sut.LoadCommand.ExecuteAsync(null);
        return sut;
    }

    private static HomeRowViewModel RowOf(HomeViewModel sut, string name) => sut.Rows.Single(r => r.Name == name);

    [Fact]
    public async Task Starts_on_today()
    {
        var sut = await LoadedAsync();

        sut.DaysBack.Should().Be(0);
        sut.IsOnToday.Should().BeTrue();
        sut.SelectedDay.Should().Be(Tuesday);
        sut.Title.Should().Be("Oggi");
        sut.DateLabel.Should().Be("martedì 10 novembre");
        sut.DayCountText.Should().Be("1 di 3");
        sut.PastDayBanner.Should().BeNull();
    }

    [Fact]
    public async Task Previous_day_moves_title_date_count_and_banner()
    {
        var sut = await LoadedAsync();

        sut.PreviousDayCommand.Execute(null);

        sut.IsOnToday.Should().BeFalse();
        sut.SelectedDay.Should().Be(Tuesday.AddDays(-1));
        sut.Title.Should().Be("Lunedì 9 novembre");
        sut.DateLabel.Should().Be("ieri");
        sut.DayCountText.Should().Be("2 di 3");
        sut.PastDayBanner.Should().Be("Stai segnando lunedì 9 novembre. Quello che registri va su quel giorno.");

        sut.PreviousDayCommand.Execute(null);
        sut.DateLabel.Should().Be("2 giorni fa");
    }

    [Fact]
    public async Task Back_to_today_clears_the_banner()
    {
        var sut = await LoadedAsync();
        sut.SelectDayCommand.Execute(3);
        sut.PastDayBanner.Should().NotBeNull();

        sut.BackToTodayCommand.Execute(null);

        sut.DaysBack.Should().Be(0);
        sut.Title.Should().Be("Oggi");
        sut.PastDayBanner.Should().BeNull();
    }

    [Fact]
    public async Task Cannot_go_past_the_window_or_into_the_future()
    {
        var sut = await LoadedAsync();

        sut.NextDayCommand.CanExecute(null).Should().BeFalse();
        sut.NextDayCommand.Execute(null);   // Execute non consulta CanExecute: tiene la guardia interna
        sut.DaysBack.Should().Be(0);

        sut.MaxDaysBack = 2;
        for (var i = 0; i < 3; i++) sut.PreviousDayCommand.Execute(null);
        sut.DaysBack.Should().Be(2);
        sut.PreviousDayCommand.CanExecute(null).Should().BeFalse();

        sut.SelectDayCommand.Execute(99);
        sut.DaysBack.Should().Be(2);
    }

    [Fact]
    public async Task Rows_mark_the_selected_day_and_use_its_values()
    {
        var sut = await LoadedAsync();
        RowOf(sut, "Acqua").StripCells.Select(c => c.IsSelected).Should().Equal([.. Enumerable.Repeat(false, 13), true]);
        RowOf(sut, "Acqua").KeyFigureText.Should().StartWith("2");
        RowOf(sut, "Corsa").IsDoneOnDay.Should().BeFalse();

        sut.PreviousDayCommand.Execute(null);

        RowOf(sut, "Acqua").StripCells.Select(c => c.IsSelected).Should().Equal([.. Enumerable.Repeat(false, 12), true, false]);
        RowOf(sut, "Acqua").StripCells[^1].IsToday.Should().BeTrue();
        RowOf(sut, "Acqua").KeyFigureText.Should().StartWith("5");
        RowOf(sut, "Corsa").IsDoneOnDay.Should().BeTrue();
        RowOf(sut, "Lettura").IsDoneOnDay.Should().BeFalse();
    }

    [Fact]
    public async Task Done_filter_and_group_progress_follow_the_day()
    {
        var sut = await LoadedAsync();
        RowOf(sut, "Mattina").GroupProgressText.Should().Be("1 di 2");
        sut.HideCompletedToday = true;
        sut.Rows.Select(r => r.Name).Should().BeEquivalentTo("Mattina", "Corsa", "Lettura");

        sut.PreviousDayCommand.Execute(null);

        sut.Rows.Select(r => r.Name).Should().BeEquivalentTo("Mattina", "Lettura");
        RowOf(sut, "Mattina").GroupProgressText.Should().Be("2 di 2");
        sut.DayCountText.Should().Be("2 di 3", "il filtro non cambia il conto");
    }

    [Fact]
    public async Task Hide_done_on_today_hides_an_abstinence_in_progress()
    {
        _query.HomeRows.Clear();
        _query.HomeRows.Add(Row("Astinenza", null, HabitType.Resistance, Days()) with { AbstinenceStatus = AbstinenceStatus.Abstaining });
        _query.HomeRows.Add(Row("Azione", null, HabitType.Action, Days()));
        var sut = await LoadedAsync();

        sut.DaysBack.Should().Be(0);
        sut.HideCompletedToday = true;

        sut.Rows.Should().ContainSingle().Which.Name.Should().Be("Azione");
    }

    [Fact]
    public async Task A_filter_that_hides_the_selected_row_clears_the_selection()
    {
        _query.HomeRows.Clear();
        _query.HomeRows.Add(Row("Alfa", null, HabitType.Action, Days()));
        _query.HomeRows.Add(Row("Beta", null, HabitType.Action, Days()));
        var sut = await LoadedAsync();
        sut.SelectedRow = RowOf(sut, "Beta");

        sut.SearchText = "Alfa";

        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task A_filter_keeps_the_selected_row_as_a_live_row()
    {
        _query.HomeRows.Clear();
        _query.HomeRows.Add(Row("Alfa", null, HabitType.Action, Days()));
        _query.HomeRows.Add(Row("Beta", null, HabitType.Action, Days()));
        var sut = await LoadedAsync();
        sut.SelectedRow = RowOf(sut, "Alfa");

        sut.SearchText = "Alfa";

        sut.SelectedRow.Should().NotBeNull();
        sut.Rows.Contains(sut.SelectedRow!).Should().BeTrue();
    }

    [Fact]
    public async Task Quick_action_on_a_past_day_passes_that_day()
    {
        var sut = await LoadedAsync();
        sut.PreviousDayCommand.Execute(null);

        await RowOf(sut, "Lettura").QuickActionCommand.ExecuteAsync(null);

        _handler.Days.Should().Equal(Tuesday.AddDays(-1));
    }

    [Fact]
    public async Task Quick_action_on_today_passes_null()
    {
        var sut = await LoadedAsync();

        await RowOf(sut, "Lettura").QuickActionCommand.ExecuteAsync(null);

        _handler.Days.Should().Equal(new DateOnly?[] { null });
    }

    [Fact]
    public async Task Changing_day_does_not_refetch()
    {
        var sut = await LoadedAsync();

        sut.PreviousDayCommand.Execute(null);
        sut.SelectDayCommand.Execute(5);

        _query.HomeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Changing_day_keeps_the_selection()
    {
        var sut = await LoadedAsync();
        var id = RowOf(sut, "Corsa").HabitNodeId;
        sut.SelectedRow = RowOf(sut, "Corsa");

        sut.PreviousDayCommand.Execute(null);

        sut.SelectedRow.Should().NotBeNull();
        sut.SelectedRow!.HabitNodeId.Should().Be(id);
        sut.SelectedRow.Should().BeSameAs(RowOf(sut, "Corsa"));
        sut.SelectedRow.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task Switching_tab_returns_to_today()
    {
        var sut = await LoadedAsync();
        sut.PreviousDayCommand.Execute(null);

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.DaysBack.Should().Be(0);
        sut.Title.Should().Be("Archiviate");
        sut.DateLabel.Should().BeEmpty();
        sut.DayCountText.Should().BeEmpty();
        sut.ShowDayHeader.Should().BeFalse();
        sut.PreviousDayCommand.CanExecute(null).Should().BeFalse();
        sut.PreviousDayCommand.Execute(null);
        sut.SelectDayCommand.Execute(2);
        sut.DaysBack.Should().Be(0, "fuori dalle Attive il giorno non si cambia");

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Trash);
        sut.Title.Should().Be("Cestino");
        sut.PreviousDayCommand.CanExecute(null).Should().BeFalse();

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Active);
        sut.Title.Should().Be("Oggi");
        sut.PreviousDayCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Day_rollover_returns_to_today_and_reloads()
    {
        // Review Focus 1: l'app resta aperta la notte su "ieri".
        var sut = await LoadedAsync();
        sut.PreviousDayCommand.Execute(null);

        _clock.Set(new DateTimeOffset(2026, 11, 11, 0, 30, 0, TimeSpan.Zero));
        _ticks.TickAll();

        sut.DaysBack.Should().Be(0);
        sut.SelectedDay.Should().Be(Tuesday.AddDays(1));
        sut.Title.Should().Be("Oggi");
        sut.DateLabel.Should().Be("mercoledì 11 novembre");
        _query.HomeCallCount.Should().Be(2);

        await RowOf(sut, "Lettura").QuickActionCommand.ExecuteAsync(null);
        _handler.Days.Should().Equal(new DateOnly?[] { null });

        _ticks.TickAll();
        _query.HomeCallCount.Should().Be(2, "lo stesso giorno non ricarica a ogni battito");
    }

    [Fact]
    public async Task A_reload_after_midnight_does_not_shift_the_selected_day()
    {
        // Un evento ricarica prima del battito: "ieri" (lunedì) non deve diventare martedì senza dirlo.
        var sut = await LoadedAsync();
        sut.PreviousDayCommand.Execute(null);

        _clock.Set(new DateTimeOffset(2026, 11, 11, 0, 30, 0, TimeSpan.Zero));
        await sut.LoadCommand.ExecuteAsync(null);

        sut.DaysBack.Should().Be(0);
        sut.SelectedDay.Should().Be(Tuesday.AddDays(1));
    }

    [Fact]
    public async Task A_reload_on_the_same_day_keeps_the_selected_day()
    {
        var sut = await LoadedAsync();
        sut.PreviousDayCommand.Execute(null);

        await sut.LoadCommand.ExecuteAsync(null);

        sut.DaysBack.Should().Be(1);
        sut.Title.Should().Be("Lunedì 9 novembre");
    }

    [Fact]
    public async Task Shrinking_the_window_clamps_the_selected_day()
    {
        // Review Focus 4: non si resta a scrivere su un giorno che non si vede.
        var sut = await LoadedAsync();
        sut.SelectDayCommand.Execute(6);

        sut.MaxDaysBack = 3;

        sut.DaysBack.Should().Be(3);
        sut.Title.Should().Be("Sabato 7 novembre");
        sut.DayHeader.Single(c => c.IsSelected).DaysBack.Should().Be(3);
    }

    [Fact]
    public async Task Day_header_has_fourteen_days_and_marks_today_and_the_selected_one()
    {
        var sut = await LoadedAsync();

        sut.DayHeader.Should().HaveCount(14);
        sut.DayHeader.Select(c => c.DaysBack).Should().BeInDescendingOrder();
        sut.DayHeader[^1].Should().Be(new DayHeaderCell("oggi", "m", "martedì 10 novembre", 0, IsToday: true, IsSelected: true));
        sut.DayHeader[^2].Should().Be(new DayHeaderCell("lun", "l", "lunedì 9 novembre", 1, IsToday: false, IsSelected: false));
        sut.ShowDayHeader.Should().BeTrue();

        sut.PreviousDayCommand.Execute(null);

        sut.DayHeader[^1].IsSelected.Should().BeFalse();
        sut.DayHeader[^1].IsToday.Should().BeTrue();
        sut.DayHeader[^2].IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task A_lagging_habit_marks_its_last_cell_for_yesterday()
    {
        // Review Focus 2: reset dell'abitudine 05:00, app a mezzanotte, sono le 02:00. Per l'abitudine è ancora lunedì:
        // "ieri" della Home è la sua ultima cella, quella che il log rapido riempie.
        _query.HomeRows.Add(Row("Notte", null, HabitType.Action, Days(1, 0, 1), doneToday: true) with
        {
            HabitDay = new DayBoundary(TimeZoneInfo.Utc, new TimeOnly(5, 0)),
            AppDay = AppDay,
        });
        _clock.Set(new DateTimeOffset(2026, 11, 10, 2, 0, 0, TimeSpan.Zero));
        var sut = await LoadedAsync();

        sut.PreviousDayCommand.Execute(null);

        RowOf(sut, "Notte").StripCells[^1].IsSelected.Should().BeTrue();
        RowOf(sut, "Notte").StripCells.Count(c => c.IsSelected).Should().Be(1);
        RowOf(sut, "Notte").IsDoneOnDay.Should().BeTrue();
        RowOf(sut, "Corsa").StripCells[^2].IsSelected.Should().BeTrue("senza un reset suo la riga segue il giorno dell'app");

        sut.PreviousDayCommand.Execute(null);   // domenica: per "Notte" è la penultima cella, vuota

        RowOf(sut, "Notte").StripCells[^2].IsSelected.Should().BeTrue();
        RowOf(sut, "Notte").IsDoneOnDay.Should().BeFalse();
    }

    private sealed class RecordingHandler : IQuickActionHandler
    {
        public List<DateOnly?> Days { get; } = [];
        public HabitType HabitType => HabitType.Action;

        public ValueTask<bool> ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default)
        {
            Days.Add(day);
            return new(true);
        }
    }
}
