# Calendar View Modes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Week / Month / Year view modes to the Abitudine-tab calendar with square day cells, all tinted in the habit color, selecting a day in any view driving the rest of the tab as today.

**Architecture:** A `CalendarViewMode` enum + an observable `ViewMode` on `CalendarNavigatorViewModel` drive three grid builders (7 / 42 / 371 cells). The host's marker provider becomes date-range based (implemented by looping the existing per-month completion/note queries and normalizing tint across the range), so a week or a year is fetched and normalized in one pass with no Data-layer change. The view renders the active mode with a mode-appropriate panel + template; Week/Month cells are responsive squares, Year is a compact habit-colored heatmap.

**Tech Stack:** WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/archive/specs/2026-06-10-calendar-view-modes-design.md`

**Repo conventions (important):**
- **The WPF app locks its binaries while running** — close it before building: `Get-Process Recordum.WPF -ErrorAction SilentlyContinue | Stop-Process -Force` (PowerShell).
- `dotnet build src/Recordum.WPF` must stay **0 warning / 0 error** (TreatWarningsAsErrors on src).
- Tests: `dotnet test tests/Recordum.WPF.Tests --nologo`. Single: `... --filter "FullyQualifiedName~<Name>"`.
- Flaky test note: `HomeViewModelTests.CompletionRecorded_event_reloads_the_list_for_visual_feedback` fails intermittently only in the full suite; confirm green by re-running or running its class in isolation.
- Commit messages in English, footer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Culture-aware first-day-of-week and labels use `CultureInfo.CurrentCulture` (already done in the existing code).

---

## File Structure

- `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarViewMode.cs` — **new**: enum.
- `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs` — **modify**: range markers + `ReloadAsync` (Task 1); view mode, builders, nav, label (Task 2).
- `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarDayCellViewModel.cs` — **modify**: `TooltipText` (Task 3).
- `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` — **modify**: range-based `LoadMarkersAsync` (Task 1).
- `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` — **modify**: toggle + 3 bodies + square cells (Task 4).
- `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs` — **modify**: update to range provider (Task 1) + add view-mode tests (Task 2).

---

## Task 1: Range-based marker provider + ReloadAsync extraction (Month behavior preserved)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs`

This task changes the marker provider from per-month to a date range and routes the current month-grid load through it. No view modes yet — the Month grid must behave exactly as today, with all existing tests green (after their mechanical signature update).

- [ ] **Step 1: Update the existing tests to the new provider signature (they will fail to compile until the impl changes)**

In `CalendarNavigatorViewModelTests.cs`: the helper `NewSut` keeps its `CalendarMarkerProvider? provider` parameter. Replace the two provider lambdas and record name. Change:
```csharp
        CalendarMarkerProvider provider = (y, m, ct) => Task.FromResult(
            new CalendarMonthMarkers(new[]
```
(both occurrences, in `LoadAsync_applies_markers_from_provider` and `ApplyMarkers_sets_tint_number_and_notecount`) to:
```csharp
        CalendarMarkerProvider provider = (from, to, ct) => Task.FromResult(
            new CalendarMarkers(new[]
```
And in `NextMonth_then_PrevMonth_returns_to_start_and_rolls_year`, change `NextMonthCommand` → `NextCommand` and `PrevMonthCommand` → `PrevCommand`:
```csharp
        await sut.NextCommand.ExecuteAsync(null);
        sut.DisplayYear.Should().Be(2027);
        sut.DisplayMonth.Should().Be(1);

        await sut.PrevCommand.ExecuteAsync(null);
        sut.DisplayYear.Should().Be(2026);
        sut.DisplayMonth.Should().Be(12);
```

- [ ] **Step 2: Run the tests — verify they FAIL (compile errors: `CalendarMarkers`, `NextCommand` missing)**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~CalendarNavigatorViewModelTests" --nologo`
Expected: compile error / FAIL.

- [ ] **Step 3: Rename the markers record + provider delegate, range-based**

In `CalendarNavigatorViewModel.cs`, replace these two declarations near the top of the file:
```csharp
/// <summary>Day-markers for one month, supplied by the host.</summary>
public sealed record CalendarMonthMarkers(IReadOnlyList<CalendarDayMarker> Days);

/// <summary>Host-supplied source of month markers (decouples the calendar from the domain).</summary>
public delegate Task<CalendarMonthMarkers> CalendarMarkerProvider(int year, int month, CancellationToken ct);
```
with:
```csharp
/// <summary>Day-markers for a date range, supplied by the host.</summary>
public sealed record CalendarMarkers(IReadOnlyList<CalendarDayMarker> Days);

/// <summary>Host-supplied source of markers for an inclusive date range (decouples the calendar from the domain).</summary>
public delegate Task<CalendarMarkers> CalendarMarkerProvider(DateOnly from, DateOnly to, CancellationToken ct);
```

- [ ] **Step 4: Route LoadAsync through ReloadAsync and make markers range-based**

In `CalendarNavigatorViewModel.cs`, replace the `LoadAsync`, `BuildGrid`, and `ApplyMarkersAsync` methods with the following. `LoadAsync` keeps its signature (the host call site is unchanged); it now delegates building to `ReloadAsync`, which fetches markers for the visible cell span `[first cell, last cell]`:
```csharp
    public async Task LoadAsync(int year, int month, DateOnly? selected, CancellationToken ct = default)
    {
        DisplayYear = year;
        DisplayMonth = month;
        SelectedDate = selected;
        await ReloadAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Rebuilds the grid for the current view mode, applies markers for the visible span, and
    /// re-resolves the selection. All navigation/jump/view-mode changes funnel through here.</summary>
    private async Task ReloadAsync(CancellationToken ct)
    {
        _isLoading = true;
        try
        {
            BuildMonthGrid(DisplayYear, DisplayMonth);
            HeaderLabel = new DateOnly(DisplayYear, DisplayMonth, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            await ApplyMarkersAsync(ct).ConfigureAwait(true);
            RefreshSelection();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void BuildMonthGrid(int year, int month)
    {
        Cells.Clear();
        var first = new DateOnly(year, month, 1);
        int firstDow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int leading = ((int)first.DayOfWeek - firstDow + 7) % 7;
        var start = first.AddDays(-leading);
        for (int i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            Cells.Add(new CalendarDayCellViewModel(
                date,
                inCurrentMonth: date.Month == month && date.Year == year,
                isToday: date == _today,
                isFuture: date > _today));
        }
    }

    /// <summary>Fetches markers for the visible cell span and assigns tint/number/notes by date.</summary>
    private async Task ApplyMarkersAsync(CancellationToken ct)
    {
        foreach (var cell in Cells)
        {
            cell.TintColorHex = CompletedColorHex;
            cell.TintStep = 0;
            cell.Number = null;
            cell.NoteCount = 0;
        }
        if (MarkerProvider is null || Cells.Count == 0) return;

        var markers = await MarkerProvider(Cells[0].Date, Cells[^1].Date, ct).ConfigureAwait(true);
        var byDate = markers.Days.ToDictionary(d => d.Date);
        foreach (var cell in Cells)
        {
            if (byDate.TryGetValue(cell.Date, out var m))
            {
                cell.TintStep = m.TintStep;
                cell.Number = m.Number;
                cell.NoteCount = m.NoteCount;
            }
        }
    }
```

Also replace the body of `JumpAsync` to use `ReloadAsync`:
```csharp
    public Task JumpAsync(CancellationToken ct = default)
        => _isLoading ? Task.CompletedTask : ReloadAsync(ct);
```

Rename the observable backing field `_monthLabel` to `_headerLabel` and its property to `HeaderLabel`:
```csharp
    [ObservableProperty] private string _headerLabel = string.Empty;
```
(remove the old `[ObservableProperty] private string _monthLabel = string.Empty;`).

- [ ] **Step 5: Replace PrevMonth/NextMonth with view-aware Prev/Next (Month behavior only for now)**

Replace the existing `PrevMonthAsync` and `NextMonthAsync` `[RelayCommand]` methods with `PrevAsync` / `NextAsync` (this task only handles Month; Task 2 adds Week/Year branches):
```csharp
    [RelayCommand]
    private Task PrevAsync(CancellationToken ct)
    {
        var prev = new DateOnly(DisplayYear, DisplayMonth, 1).AddMonths(-1);
        DisplayYear = prev.Year;
        DisplayMonth = prev.Month;
        return ReloadAsync(ct);
    }

    [RelayCommand]
    private Task NextAsync(CancellationToken ct)
    {
        var next = new DateOnly(DisplayYear, DisplayMonth, 1).AddMonths(1);
        DisplayYear = next.Year;
        DisplayMonth = next.Month;
        return ReloadAsync(ct);
    }
```

- [ ] **Step 6: Update the host to supply range markers**

In `HabitScreenViewModel.cs`, the constructor sets `MarkerProvider = LoadMonthMarkersAsync`. Change it to `LoadMarkersAsync`:
```csharp
        Calendar = new CalendarNavigatorViewModel(clock) { MarkerProvider = LoadMarkersAsync };
```
Replace the `LoadMonthMarkersAsync` method with a range-based one that loops the months in the span, reuses the existing per-month queries, and normalizes tint across the whole range:
```csharp
    private async Task<CalendarMarkers> LoadMarkersAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var node = Node;
        if (node is null) return new CalendarMarkers([]);

        var magByDate = new Dictionary<DateOnly, decimal>();
        var noteByDate = new Dictionary<DateOnly, int>();

        var cursor = new DateOnly(from.Year, from.Month, 1);
        var last = new DateOnly(to.Year, to.Month, 1);
        while (cursor <= last)
        {
            var mags = await _completions.GetDailyMagnitudeAsync(node.Id, cursor.Year, cursor.Month, ct).ConfigureAwait(true);
            var notes = await _noteService.ListNoteCountsInMonthAsync(node.Id, cursor.Year, cursor.Month, ct).ConfigureAwait(true);
            foreach (var m in mags) magByDate[m.Date] = m.Magnitude;
            foreach (var n in notes) noteByDate[n.Date] = n.Count;
            cursor = cursor.AddMonths(1);
        }

        var rangeMax = magByDate.Count > 0 ? magByDate.Values.Max() : 0m;
        var type = node.HabitType ?? HabitType.Action;

        var days = new List<CalendarDayMarker>();
        foreach (var date in magByDate.Keys.Union(noteByDate.Keys).Distinct())
        {
            var mag = magByDate.TryGetValue(date, out var mv) ? mv : 0m;
            var noteCount = noteByDate.TryGetValue(date, out var nc) ? nc : 0;
            days.Add(new CalendarDayMarker(date, TintStepFor(type, mag, rangeMax), NumberFor(type, mag), noteCount));
        }
        return new CalendarMarkers(days);
    }
```
(Leave `TintStepFor`, `NumberFor`, `FormatMag` unchanged — they are reused.)

- [ ] **Step 7: Run the calendar + host tests — verify PASS**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~CalendarNavigatorViewModelTests" --nologo`
Expected: PASS (all existing calendar tests, now on the range provider).

- [ ] **Step 8: Build — verify 0/0**

Close the app first, then run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`. (Any other caller of `MonthLabel`/`CalendarMonthMarkers`/`NextMonthCommand` surfaces here — search and fix; the only XAML consumer of `MonthLabel`/`PrevMonthCommand`/`NextMonthCommand` is `CalendarNavigator.xaml`, rewritten in Task 4. If the build fails on that XAML binding before Task 4, that is expected — proceed; Task 4 fixes the XAML. To keep this task's build green, also do Step 9.)

- [ ] **Step 9: Keep the existing XAML compiling — point its bindings at the renamed members**

In `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml`, update the three header bindings so the project still builds (Task 4 will restructure the whole header):
- `Text="{Binding MonthLabel}"` → `Text="{Binding HeaderLabel}"`
- `Command="{Binding PrevMonthCommand}"` → `Command="{Binding PrevCommand}"`
- `Command="{Binding NextMonthCommand}"` → `Command="{Binding NextCommand}"`

Re-run Step 8 build; expected `Avvisi: 0  Errori: 0`.

- [ ] **Step 10: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs
git commit -m "refactor(wpf): calendar markers by date range + view-aware nav scaffolding"
```

---

## Task 2: CalendarViewMode + Week/Year builders + view-aware navigation

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarViewMode.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs`

- [ ] **Step 1: Create the enum**

```csharp
// src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarViewMode.cs
namespace Recordum.WPF.ViewModels.HabitScreen.Calendar;

/// <summary>Granularity of the Abitudine-tab calendar.</summary>
public enum CalendarViewMode { Week, Month, Year }
```

- [ ] **Step 2: Write the failing tests for the new behavior**

Add to `CalendarNavigatorViewModelTests.cs`:
```csharp
    [Fact]
    public async Task WeekView_builds_seven_cells_around_the_selected_date()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: new DateOnly(2026, 6, 10));

        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Week);

        sut.Cells.Should().HaveCount(7);
        sut.Cells.Should().Contain(c => c.Date == new DateOnly(2026, 6, 10));
        sut.Cells.Select(c => c.Date).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task YearView_builds_371_cells_starting_on_a_week_boundary_on_or_before_Jan1()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: null);

        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Year);

        sut.Cells.Should().HaveCount(371);
        sut.Cells[0].Date.Should().BeOnOrBefore(new DateOnly(2026, 1, 1));
        sut.Cells.Should().Contain(c => c.Date == new DateOnly(2026, 1, 1));
        sut.Cells.Should().Contain(c => c.Date == new DateOnly(2026, 12, 31));
        sut.Cells.Count(c => c.InCurrentMonth).Should().Be(365); // 2026 is not a leap year
    }

    [Fact]
    public async Task Next_in_WeekView_advances_by_seven_days()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: new DateOnly(2026, 6, 10));
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
        await sut.LoadAsync(2026, 6, selected: null);
        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Year);

        await sut.NextCommand.ExecuteAsync(null);

        sut.DisplayYear.Should().Be(2027);
        sut.Cells.Should().Contain(c => c.Date == new DateOnly(2027, 1, 1));
    }

    [Fact]
    public async Task SetViewMode_back_to_Month_restores_42_cells_and_keeps_selection()
    {
        var sut = NewSut(out _);
        await sut.LoadAsync(2026, 6, selected: new DateOnly(2026, 6, 10));
        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Year);

        await sut.SetViewModeCommand.ExecuteAsync(CalendarViewMode.Month);

        sut.Cells.Should().HaveCount(42);
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 10)).IsSelected.Should().BeTrue();
    }
```

- [ ] **Step 3: Run the new tests — verify they FAIL (`SetViewModeCommand`, `CalendarViewMode` not wired)**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~CalendarNavigatorViewModelTests" --nologo`
Expected: compile error / FAIL on the new tests.

- [ ] **Step 4: Add the view-mode state + week anchor**

In `CalendarNavigatorViewModel.cs`, add these fields/properties near the other `[ObservableProperty]` declarations:
```csharp
    [ObservableProperty] private CalendarViewMode _viewMode = CalendarViewMode.Month;
    private DateOnly _weekAnchor;
```
And set the week anchor in `LoadAsync` (so a later Week switch centers on the selection or today) — change `LoadAsync` to:
```csharp
    public async Task LoadAsync(int year, int month, DateOnly? selected, CancellationToken ct = default)
    {
        DisplayYear = year;
        DisplayMonth = month;
        SelectedDate = selected;
        _weekAnchor = selected ?? _today;
        await ReloadAsync(ct).ConfigureAwait(true);
    }
```

- [ ] **Step 5: Branch the grid build + label on the view mode**

In `ReloadAsync`, replace the unconditional month build + label with a mode switch:
```csharp
    private async Task ReloadAsync(CancellationToken ct)
    {
        _isLoading = true;
        try
        {
            switch (ViewMode)
            {
                case CalendarViewMode.Week: BuildWeekGrid(_weekAnchor); break;
                case CalendarViewMode.Year: BuildYearGrid(DisplayYear); break;
                default: BuildMonthGrid(DisplayYear, DisplayMonth); break;
            }
            HeaderLabel = BuildHeaderLabel();
            await ApplyMarkersAsync(ct).ConfigureAwait(true);
            RefreshSelection();
        }
        finally
        {
            _isLoading = false;
        }
    }
```
Add the two new builders and the label helper (place them next to `BuildMonthGrid`):
```csharp
    private void BuildWeekGrid(DateOnly anchor)
    {
        Cells.Clear();
        int firstDow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int leading = ((int)anchor.DayOfWeek - firstDow + 7) % 7;
        var start = anchor.AddDays(-leading);
        for (int i = 0; i < 7; i++)
        {
            var date = start.AddDays(i);
            Cells.Add(new CalendarDayCellViewModel(
                date, inCurrentMonth: true, isToday: date == _today, isFuture: date > _today));
        }
    }

    private void BuildYearGrid(int year)
    {
        Cells.Clear();
        var jan1 = new DateOnly(year, 1, 1);
        int firstDow = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int leading = ((int)jan1.DayOfWeek - firstDow + 7) % 7;
        var start = jan1.AddDays(-leading);
        for (int i = 0; i < 371; i++) // 53 weeks × 7
        {
            var date = start.AddDays(i);
            Cells.Add(new CalendarDayCellViewModel(
                date, inCurrentMonth: date.Year == year, isToday: date == _today, isFuture: date > _today));
        }
    }

    private string BuildHeaderLabel()
    {
        var culture = CultureInfo.CurrentCulture;
        return ViewMode switch
        {
            CalendarViewMode.Week => $"{Cells[0].Date.ToString("d MMM", culture)} – {Cells[^1].Date.ToString("d MMM yyyy", culture)}",
            CalendarViewMode.Year => DisplayYear.ToString(culture),
            _ => new DateOnly(DisplayYear, DisplayMonth, 1).ToString("MMMM yyyy", culture),
        };
    }
```

- [ ] **Step 6: View-aware Prev/Next + the SetViewMode command**

Replace `PrevAsync` / `NextAsync` from Task 1 with view-aware versions, and add `SetViewModeAsync`:
```csharp
    [RelayCommand]
    private Task PrevAsync(CancellationToken ct) => StepAsync(-1, ct);

    [RelayCommand]
    private Task NextAsync(CancellationToken ct) => StepAsync(+1, ct);

    private Task StepAsync(int direction, CancellationToken ct)
    {
        switch (ViewMode)
        {
            case CalendarViewMode.Week:
                _weekAnchor = _weekAnchor.AddDays(7 * direction);
                break;
            case CalendarViewMode.Year:
                DisplayYear += direction;
                break;
            default:
                var m = new DateOnly(DisplayYear, DisplayMonth, 1).AddMonths(direction);
                DisplayYear = m.Year;
                DisplayMonth = m.Month;
                break;
        }
        return ReloadAsync(ct);
    }

    [RelayCommand]
    private Task SetViewModeAsync(CalendarViewMode mode, CancellationToken ct)
    {
        ViewMode = mode;
        if (mode == CalendarViewMode.Week) _weekAnchor = SelectedDate ?? _today;
        return ReloadAsync(ct);
    }
```

- [ ] **Step 7: Run the calendar tests — verify PASS**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~CalendarNavigatorViewModelTests" --nologo`
Expected: PASS (existing + 5 new).

- [ ] **Step 8: Build — verify 0/0**

Close the app first, then run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`.

- [ ] **Step 9: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarViewMode.cs src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs
git commit -m "feat(wpf): calendar Week/Month/Year view modes with view-aware navigation"
```

---

## Task 3: TooltipText on the day cell (for the Year view)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarDayCellViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `CalendarNavigatorViewModelTests.cs`:
```csharp
    [Fact]
    public void DayCell_TooltipText_includes_the_date()
    {
        var cell = new CalendarDayCellViewModel(new DateOnly(2026, 6, 10), inCurrentMonth: true, isToday: false, isFuture: false);
        cell.TooltipText.Should().Contain("2026");
        cell.TooltipText.Should().Contain("10");
    }
```

- [ ] **Step 2: Run it — verify FAIL (no `TooltipText`)**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~DayCell_TooltipText" --nologo`
Expected: compile error / FAIL.

- [ ] **Step 3: Add `TooltipText`**

In `CalendarDayCellViewModel.cs`, add a computed property (the long-date plus an optional count), and notify it when `Number`/`NoteCount` change:
```csharp
    public string TooltipText
    {
        get
        {
            var date = Date.ToString("D", System.Globalization.CultureInfo.CurrentCulture);
            if (!string.IsNullOrEmpty(Number)) return $"{date} · {Number}";
            if (NoteCount > 0) return $"{date} · {NoteCount}";
            return date;
        }
    }
```
Extend the existing `OnNoteCountChanged` and `OnNumberChanged` partial methods to also raise `TooltipText`:
```csharp
    partial void OnNoteCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(ShowNoteCount));
        OnPropertyChanged(nameof(TooltipText));
    }

    partial void OnNumberChanged(string? value)
    {
        OnPropertyChanged(nameof(HasNumber));
        OnPropertyChanged(nameof(TooltipText));
    }
```

- [ ] **Step 4: Run it — verify PASS**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~DayCell_TooltipText" --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarDayCellViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs
git commit -m "feat(wpf): CalendarDayCellViewModel.TooltipText for the year view"
```

---

## Task 4: CalendarNavigator.xaml — toggle, three bodies, square cells

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml`

> No unit test (XAML). Visual verification in Task 5.

- [ ] **Step 1: Replace the file contents**

Replace the entire `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` with the following. The day-cell visual is extracted to a keyed `DayCellTemplate` (square via `Height = ActualWidth`) shared by Week + Month; Year uses a compact heatmap template. The header gains a `[Settimana|Mese|Anno]` RadioButton toggle bound to `SetViewModeCommand` (+ `EnumToBool` for the checked state). View bodies are toggled with `EnumToVisibility` (enum parameter via `{x:Static}`).

```xml
<UserControl x:Class="Recordum.WPF.Views.HabitScreen.Calendar.CalendarNavigator"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Recordum.WPF.ViewModels.HabitScreen.Calendar"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance vm:CalendarNavigatorViewModel}">
    <UserControl.Resources>
        <!-- Square day cell shared by Week + Month. Height binds to its own ActualWidth → responsive square. -->
        <DataTemplate x:Key="DayCellTemplate" DataType="{x:Type vm:CalendarDayCellViewModel}">
            <Button Margin="2" Padding="0"
                    Height="{Binding ActualWidth, RelativeSource={RelativeSource Self}}"
                    BorderThickness="1"
                    HorizontalContentAlignment="Stretch"
                    VerticalContentAlignment="Stretch"
                    Command="{Binding DataContext.SelectDateCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                    CommandParameter="{Binding}">
                <Button.Style>
                    <Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
                        <Setter Property="Background" Value="Transparent"/>
                        <Setter Property="BorderBrush" Value="Transparent"/>
                        <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsFuture}" Value="True">
                                <Setter Property="IsEnabled" Value="False"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding InCurrentMonth}" Value="False">
                                <Setter Property="Foreground" Value="{DynamicResource Brush.TextMuted}"/>
                                <Setter Property="Opacity" Value="0.45"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding IsToday}" Value="True">
                                <Setter Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                            </DataTrigger>
                            <!-- Selected: filled accent background (= habit color via the HabitScreen
                                 Brush.Accent override), with contrast-aware text. -->
                            <DataTrigger Binding="{Binding IsSelected}" Value="True">
                                <Setter Property="Background" Value="{DynamicResource Brush.Accent}"/>
                                <Setter Property="Foreground" Value="{Binding TintColorHex, Converter={StaticResource ContrastForeground}}"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Button.Style>
                <Grid>
                    <Border CornerRadius="6">
                        <Border.Background>
                            <MultiBinding Converter="{StaticResource HabitColorTint}">
                                <Binding Path="TintColorHex"/>
                                <Binding Path="TintStep"/>
                            </MultiBinding>
                        </Border.Background>
                    </Border>
                    <TextBlock Text="{Binding DayNumber}" HorizontalAlignment="Left" VerticalAlignment="Top"
                               Margin="5,3,0,0" FontSize="{StaticResource FontSize.Small}"/>
                    <TextBlock Text="{Binding Number}" HorizontalAlignment="Center" VerticalAlignment="Bottom"
                               Margin="0,0,0,4" FontWeight="SemiBold" FontSize="{StaticResource FontSize.Small}"
                               Visibility="{Binding HasNumber, Converter={StaticResource BoolToVisible}}"/>
                    <Path HorizontalAlignment="Right" VerticalAlignment="Top" Data="M 0,0 L 13,0 L 13,13 Z"
                          Visibility="{Binding HasNote, Converter={StaticResource BoolToVisible}}"
                          Fill="{Binding DataContext.NoteColorHex, RelativeSource={RelativeSource AncestorType=ItemsControl}, Converter={StaticResource HexToBrush}}"/>
                    <TextBlock Text="{Binding NoteCount}" HorizontalAlignment="Right" VerticalAlignment="Top"
                               Margin="0,0,1,0" FontSize="8" FontWeight="Bold"
                               Foreground="{Binding DataContext.NoteColorHex, RelativeSource={RelativeSource AncestorType=ItemsControl}, Converter={StaticResource ContrastForeground}}"
                               Visibility="{Binding ShowNoteCount, Converter={StaticResource BoolToVisible}}"/>
                </Grid>
            </Button>
        </DataTemplate>

        <!-- Compact heatmap cell for the Year view: fixed small square, tooltip, click selects. -->
        <DataTemplate x:Key="YearCellTemplate" DataType="{x:Type vm:CalendarDayCellViewModel}">
            <Button Width="14" Height="14" Margin="1" Padding="0" BorderThickness="0"
                    ToolTip="{Binding TooltipText}" ToolTipService.InitialShowDelay="300"
                    Command="{Binding DataContext.SelectDateCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                    CommandParameter="{Binding}">
                <Button.Style>
                    <Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
                        <Setter Property="Background" Value="Transparent"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsFuture}" Value="True">
                                <Setter Property="IsEnabled" Value="False"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding InCurrentMonth}" Value="False">
                                <Setter Property="Opacity" Value="0.25"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding IsToday}" Value="True">
                                <Setter Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                                <Setter Property="BorderThickness" Value="1"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding IsSelected}" Value="True">
                                <Setter Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                                <Setter Property="BorderThickness" Value="2"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Button.Style>
                <Border CornerRadius="2">
                    <Border.Background>
                        <MultiBinding Converter="{StaticResource HabitColorTint}">
                            <Binding Path="TintColorHex"/>
                            <Binding Path="TintStep"/>
                        </MultiBinding>
                    </Border.Background>
                </Border>
            </Button>
        </DataTemplate>
    </UserControl.Resources>

    <Border Padding="12" BorderThickness="0,0,0,1" BorderBrush="{DynamicResource Brush.Border}">
        <StackPanel>
            <!-- Header: view toggle, prev / label / jump / next -->
            <Grid Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column="0" Orientation="Horizontal" Margin="0,0,12,0">
                    <RadioButton GroupName="calview" Content="{x:Static res:Strings.Calendar_View_Week}"
                                 Command="{Binding SetViewModeCommand}" CommandParameter="{x:Static vm:CalendarViewMode.Week}"
                                 IsChecked="{Binding ViewMode, Mode=OneWay, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:CalendarViewMode.Week}}"/>
                    <RadioButton GroupName="calview" Content="{x:Static res:Strings.Calendar_View_Month}" Margin="6,0,0,0"
                                 Command="{Binding SetViewModeCommand}" CommandParameter="{x:Static vm:CalendarViewMode.Month}"
                                 IsChecked="{Binding ViewMode, Mode=OneWay, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:CalendarViewMode.Month}}"/>
                    <RadioButton GroupName="calview" Content="{x:Static res:Strings.Calendar_View_Year}" Margin="6,0,0,0"
                                 Command="{Binding SetViewModeCommand}" CommandParameter="{x:Static vm:CalendarViewMode.Year}"
                                 IsChecked="{Binding ViewMode, Mode=OneWay, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:CalendarViewMode.Year}}"/>
                </StackPanel>

                <Button Grid.Column="1" Content="&#x2039;" Width="28" Command="{Binding PrevCommand}"/>
                <TextBlock Grid.Column="2" Text="{Binding HeaderLabel}" VerticalAlignment="Center"
                           Margin="8,0" FontWeight="SemiBold" Foreground="{DynamicResource Brush.Text}"/>
                <ComboBox Grid.Column="3" Width="64" Margin="0,0,4,0"
                          ItemsSource="{Binding SelectableMonths}" SelectedItem="{Binding DisplayMonth}"
                          SelectionChanged="OnJumpChanged"
                          Visibility="{Binding ViewMode, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static vm:CalendarViewMode.Month}}"/>
                <ComboBox Grid.Column="4" Width="74"
                          ItemsSource="{Binding SelectableYears}" SelectedItem="{Binding DisplayYear}"
                          SelectionChanged="OnJumpChanged"/>
                <Button Grid.Column="5" Content="&#x203A;" Width="28" Margin="4,0,0,0" Command="{Binding NextCommand}"/>
            </Grid>

            <!-- Weekday header (Week + Month only — hidden in Year via the Style trigger below) -->
            <ItemsControl ItemsSource="{Binding WeekdayHeaders}">
                <ItemsControl.Style>
                    <Style TargetType="ItemsControl">
                        <Setter Property="Visibility" Value="Visible"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding ViewMode}" Value="Year">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </ItemsControl.Style>
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate><UniformGrid Columns="7"/></ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding}" HorizontalAlignment="Center"
                                   FontSize="{StaticResource FontSize.Small}" Foreground="{DynamicResource Brush.TextMuted}"/>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <!-- Month body: 6×7 square grid -->
            <ItemsControl ItemsSource="{Binding Cells}" Margin="0,4,0,0"
                          ItemTemplate="{StaticResource DayCellTemplate}"
                          Visibility="{Binding ViewMode, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static vm:CalendarViewMode.Month}}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate><UniformGrid Columns="7" Rows="6"/></ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
            </ItemsControl>

            <!-- Week body: single row of 7 square cells -->
            <ItemsControl ItemsSource="{Binding Cells}" Margin="0,4,0,0"
                          ItemTemplate="{StaticResource DayCellTemplate}"
                          Visibility="{Binding ViewMode, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static vm:CalendarViewMode.Week}}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate><UniformGrid Columns="7" Rows="1"/></ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
            </ItemsControl>

            <!-- Year body: 7×53 compact heatmap, horizontal scroll -->
            <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled" Margin="0,4,0,0"
                          Visibility="{Binding ViewMode, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static vm:CalendarViewMode.Year}}">
                <ItemsControl ItemsSource="{Binding Cells}" ItemTemplate="{StaticResource YearCellTemplate}">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate><UniformGrid Rows="7" Columns="53" FlowDirection="LeftToRight"/></ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                </ItemsControl>
            </ScrollViewer>
        </StackPanel>
    </Border>
</UserControl>
```

> Note: the file needs the `res` namespace for the toggle labels. Add `xmlns:res="clr-namespace:Recordum.WPF.Resources"` to the `<UserControl …>` root (the other HabitScreen views already use this namespace). The weekday-header `ItemsControl` is hidden only in Year via the inline `<ItemsControl.Style>` `DataTrigger` (do **not** also put a `Visibility=` attribute on it — a local attribute value would beat the Style setter and the trigger would never hide it).

- [ ] **Step 2: Add the three view-label strings**

Add to `src/Recordum.WPF/Resources/Strings.resx` (English) — three `<data>` entries (mirror the existing entry format):
```xml
  <data name="Calendar.View.Week" xml:space="preserve">
    <value>Week</value>
  </data>
  <data name="Calendar.View.Month" xml:space="preserve">
    <value>Month</value>
  </data>
  <data name="Calendar.View.Year" xml:space="preserve">
    <value>Year</value>
  </data>
```
And to `src/Recordum.WPF/Resources/Strings.it.resx` (Italian):
```xml
  <data name="Calendar.View.Week" xml:space="preserve">
    <value>Settimana</value>
  </data>
  <data name="Calendar.View.Month" xml:space="preserve">
    <value>Mese</value>
  </data>
  <data name="Calendar.View.Year" xml:space="preserve">
    <value>Anno</value>
  </data>
```
Then add the three accessors to `src/Recordum.WPF/Resources/Strings.Designer.cs` (mirror the existing one-line accessor style, e.g. `public static string Calendar_View_Week => Get("Calendar.View.Week");`):
```csharp
    public static string Calendar_View_Week => Get("Calendar.View.Week");
    public static string Calendar_View_Month => Get("Calendar.View.Month");
    public static string Calendar_View_Year => Get("Calendar.View.Year");
```

- [ ] **Step 3: Build — verify 0/0**

Close the app first, then run: `dotnet build src/Recordum.WPF --nologo`
Expected: `Avvisi: 0  Errori: 0`. (Common failures: missing `res` xmlns; a missing string accessor; the weekday-header having both a `Visibility=` attribute and a `Style` Visibility setter — keep only the Style.)

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "feat(wpf): calendar view-mode toggle + square cells + year heatmap body"
```

---

## Task 5: Verify + tracker

**Files:**
- Modify: `docs/phase-4-issues.md`

- [ ] **Step 1: Full WPF suite**

Run: `dotnet test tests/Recordum.WPF.Tests --nologo`
Expected: PASS. If the only failure is `HomeViewModelTests.CompletionRecorded_event_reloads_the_list_for_visual_feedback`, re-run that class in isolation to confirm it's the known flake.

- [ ] **Step 2: Visual verification (skill `run`)**

Launch the app, open a habit, go to the Abitudine tab. Confirm:
1. The toggle shows **Settimana / Mese / Anno**; the current one is highlighted; Mese is the default.
2. **Mese**: 6×7 grid, day cells are visibly **square**; tint/number/note markers as before; today + selected accents in the habit color.
3. **Settimana**: a single row of 7 large square cells for the selected week; prev/next move by one week; label shows the week range.
4. **Anno**: a habit-colored mini-heatmap (7×53); hovering a day shows a tooltip; clicking a day selects it and updates the records panel below; prev/next move by year; label shows the year.
5. Selecting a day in any view updates quick-add + day records; switching habit resets to Month; Light and Dark both legible.

- [ ] **Step 3: Update the tracker**

In `docs/phase-4-issues.md`, add an entry under the visual-identity cluster recording the calendar redesign: Week/Month/Year view modes + square cells + habit-colored year heatmap; range-based markers (year-wide normalization, no Data-layer change); spec `2026-06-10-calendar-view-modes-design.md`; with the commit range.

- [ ] **Step 4: Commit**

```bash
git add docs/phase-4-issues.md
git commit -m "docs(phase-4): calendar Week/Month/Year view modes + square cells implemented"
```

---

## Self-Review (result)

- **Spec coverage:** range marker provider (year-wide normalization, no Data change) → Task 1; `CalendarViewMode` + Week/Month/Year builders + view-aware nav + label → Task 2; `TooltipText` for year → Task 3; toggle + 3 bodies + square cells + year heatmap + view labels → Task 4; testing (pure builders/nav/label unit + visual) → Tasks 1–5. All spec sections covered. Day view correctly absent; coloring uses `HabitColorTint`; default Month; not persisted (no AppSetting touched).
- **Placeholder scan:** no TBD/TODO; every code step shows full code; commands have expected output.
- **Type consistency:** `CalendarMarkers` record + `CalendarMarkerProvider(DateOnly,DateOnly,CancellationToken)` defined in Task 1 and consumed by the host `LoadMarkersAsync` (Task 1) and the VM `ApplyMarkersAsync` (Task 1); `HeaderLabel` (renamed from `MonthLabel`) defined Task 1, bound in XAML Task 1 Step 9 + Task 4; `PrevCommand`/`NextCommand` generated from `PrevAsync`/`NextAsync` (Task 1, revised Task 2) and used in tests + XAML; `SetViewModeCommand` from `SetViewModeAsync(CalendarViewMode, CancellationToken)` (Task 2) used in tests + XAML; `CalendarViewMode` (Task 2) used by builders, `EnumToBool`/`EnumToVisibility` params, and tests; `TooltipText` (Task 3) used by the `YearCellTemplate` (Task 4); `DayCellTemplate`/`YearCellTemplate` keys consistent within Task 4; `Calendar_View_Week/Month/Year` string accessors (Task 4 Step 2) used in Task 4 Step 1.
- **Known risks:**
  1. The day-cell square binding `Height="{Binding ActualWidth, RelativeSource=Self}"` relies on the `UniformGrid` giving each cell a definite width; this works because `UniformGrid` stretches columns. If a cell renders non-square in verification, the fallback is a fixed cell size — a small XAML tweak, flagged in Task 5 Step 2.
  2. The weekday-header visibility uses a `Style` `DataTrigger` on `ViewMode == Year`; ensure no competing `Visibility=` attribute remains (noted in Task 4).
  3. Year view performs up to 12 month-queries on entry — acceptable per the spec; not optimized here.
```