# Calendar View Modes — Design (#3 polish)

> **Status:** approved (2026-06-10). Filone #3 of 3 in the Phase 4 polish cluster (animation → habit visual identity → **calendar redesign**). This is the largest of the three.

## Goal

Let the Abitudine-tab calendar switch between **Week / Month / Year** views, and render day cells as crisp **squares**. Month is the current grid made square; Week is a single large-cell row; Year is a GitHub-style mini-heatmap in the habit color. Selecting a day in any view drives the rest of the Abitudine tab exactly as today.

## Decisions (approved)

- **Views:** Week, Month, Year. **No Day view** (redundant with the day-records panel already below the calendar).
- **Coloring:** all views use the habit-color tint (`HabitColorTint`, consistent with the current month grid and filone #2) — *not* the green ramp of the Detail-tab heatmap.
- **Default:** opens in **Month**; the chosen view is **not persisted** (opening a different habit resets to Month).
- **Year interaction:** clicking a day selects it (same as month/week).

## Architecture

A new enum and a view-mode property drive three grid builders inside the existing `CalendarNavigatorViewModel`; the view renders the active mode with a mode-appropriate panel + template. The host's marker provider becomes **date-range based** so a week or a year is fetched and normalized in one pass, reusing the existing per-month completion/note queries (no Data-layer change).

### `CalendarViewMode` enum — `Recordum.WPF.ViewModels.HabitScreen.Calendar`

```csharp
public enum CalendarViewMode { Week, Month, Year }
```

### `CalendarNavigatorViewModel` changes

- `[ObservableProperty] private CalendarViewMode _viewMode = CalendarViewMode.Month;`
- An anchor date `_displayDate` (the reference day; for Month it is the first of `DisplayYear`/`DisplayMonth`, for Week the day whose week is shown, for Year any day in `DisplayYear`). Kept in sync with the existing `DisplayYear`/`DisplayMonth` for Month.
- `[RelayCommand] private Task SetViewModeAsync(CalendarViewMode mode)` — sets `ViewMode` and reloads, keeping the current anchor/selection.
- **Grid builders** (replace the single `BuildGrid`), each populating `Cells`:
  - **Week** → 7 cells: the week containing the anchor date, starting on `CultureInfo.FirstDayOfWeek`.
  - **Month** → 42 cells: unchanged from today (first of month back to week start, 6×7).
  - **Year** → 371 cells: GitHub-style. Anchor = Jan 1 of `DisplayYear` walked back to the week's start day, then 53×7 = 371 consecutive days (mirrors the existing `YearHeatmapViewModel` 371-cell anchor formula). Cells outside the year render faint.
- **Navigation is view-aware**: `PrevCommand` / `NextCommand` step by ±1 week / ±1 month / ±1 year depending on `ViewMode`. `MonthLabel` becomes a view-aware `HeaderLabel`: Month → `"MMMM yyyy"`; Week → a day-range like `"6–12 gen 2026"`; Year → `"2026"`.
- **Markers**: the provider is range-based (below); `LoadAsync` computes the visible date span from the mode + anchor, fetches markers for that span, and assigns tint/number/notes to the cells as today.

> The existing `PrevMonthCommand`/`NextMonthCommand` are replaced by the view-aware `PrevCommand`/`NextCommand`; the month/year jump ComboBoxes are kept for Month, the year ComboBox is kept for Year, and Week shows only prev/next + the range label.

### Range-based marker provider (host)

Today: `delegate Task<CalendarMonthMarkers> CalendarMarkerProvider(int year, int month, CancellationToken ct)`, implemented by `HabitScreenViewModel.LoadMonthMarkersAsync`, which calls `GetDailyMagnitudeAsync(node, year, month)` and `ListNoteCountsInMonthAsync(node, year, month)` and normalizes tint against that month's max.

New: `delegate Task<CalendarMarkers> CalendarMarkerProvider(DateOnly from, DateOnly to, CancellationToken ct)` (rename `CalendarMonthMarkers` → `CalendarMarkers`). The host implements it by **iterating the distinct months in `[from, to]`**, calling the existing per-month `GetDailyMagnitudeAsync` / `ListNoteCountsInMonthAsync` for each, merging the results, and computing `TintStepFor` against the **max over the whole range**. Consequences:
- **Month view**: range = one month → identical behavior to today (range max == month max).
- **Week view**: range = the 7 visible days' month(s) → 1–2 month queries.
- **Year view**: range = 12 months → 12 month queries, normalized year-wide (a correct year heatmap, not per-month). 12 indexed queries on entering Year view is acceptable; Year view is not entered on every interaction.

No new Data-layer methods; the existing per-month queries are reused.

### View (`CalendarNavigator.xaml`)

- **Header**: add a segmented toggle `[ Settimana | Mese | Anno ]` (three `RadioButton`s in a `GroupName`, or `ToggleButton`s bound to `SetViewModeCommand` with the mode as parameter). Prev/next buttons + `HeaderLabel`. The month/year jump ComboBoxes show in Month (year ComboBox also in Year) via `EnumToVisibility` on `ViewMode`.
- **Body**: three `ItemsControl`s bound to `Cells`, each `Visibility`-bound to `ViewMode` via the existing `EnumToVisibility` converter:
  - **Week / Month** share a **square** day-cell template (the current month cell content — tint border, day number top-left, count center-bottom, note corner). Squares are **responsive**: the cell sets `Height="{Binding ActualWidth, RelativeSource={RelativeSource Self}}"`. Month panel = `UniformGrid Columns=7 Rows=6`; Week panel = `UniformGrid Columns=7 Rows=1`.
  - **Year** = compact heatmap: `UniformGrid Rows=7 Columns=53`, fixed ~14px squares, horizontal `ScrollViewer`, tooltip per day, click selects. Reuses `HabitColorTint` (habit color), not the Detail ramp.

### `CalendarDayCellViewModel`

- Add a `TooltipText` (date + count/number) for the Year view's per-day tooltip. Existing properties (`Date`, `DayNumber`, `Number`, `HasNumber`, `InCurrentMonth`, `IsToday`, `IsFuture`, `IsSelected`, `TintColorHex`, `TintStep`, `NoteCount`, `HasNote`, `ShowNoteCount`) are reused across all three views.

## Files

- `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarViewMode.cs` — **new**: enum.
- `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs` — **modify**: view mode, anchor, three grid builders, view-aware nav + label, range-marker call, `TooltipText` population.
- `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarDayCellViewModel.cs` — **modify**: add `TooltipText`.
- `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` (+ `.xaml.cs`) — **modify**: toggle, view-aware header, three bodies, square cells.
- `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` — **modify**: range-based `LoadMarkersAsync` (loops months, year-wide normalization); rename of the markers record.
- `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModelTests.cs` — **new/extend**: grid-builder + navigation + label tests.

## Testing

- **TDD unit (pure VM logic):**
  - Week builder → exactly 7 cells, correct start-of-week, anchor day present.
  - Month builder → 42 cells, unchanged anchor behavior (regression guard).
  - Year builder → 371 cells, first cell is the week-start on/before Jan 1, `IsToday`/`InCurrentMonth`-equivalent flags correct, faint cells outside the year.
  - View-aware navigation: from a known anchor, `NextCommand` in each mode advances by week/month/year; `PrevCommand` symmetric.
  - `HeaderLabel` formatting per mode.
  - `SetViewModeCommand` rebuilds `Cells` to the right count and preserves the selected date.
- **Range marker provider:** a fake provider asserts `LoadAsync` requests the correct `[from, to]` span per mode (week span, month span, full-year span).
- **Build:** `dotnet build src/Recordum.WPF` stays 0 warning / 0 error.
- **Visual verification (skill `run`):**
  1. Toggle Settimana/Mese/Anno; each renders correctly; cells in Week/Month are visibly **square**.
  2. Prev/next steps by week / month / year; the label matches.
  3. Year view is a habit-colored mini-heatmap; hovering a day shows a tooltip; clicking a day selects it and updates the records panel below.
  4. Selecting a day in Week/Month updates quick-add + day records as before.
  5. Habit color identity (filone #2) still applies (today/selected accents); Light and Dark.
  6. Default view is Month; switching habit resets to Month.

## Out of scope / notes

- **Day view** (rejected — redundant).
- **Persisting the chosen view** across sessions (could become an `AppSetting` later; YAGNI now).
- The **Detail-tab year heatmap** (`YearHeatmapControl`) is unchanged; the calendar's Year view is a separate, habit-color-tinted overview. Unifying the two is a possible future cleanup, not part of this work.
- No Data-layer query changes; range markers reuse the existing per-month queries.
```