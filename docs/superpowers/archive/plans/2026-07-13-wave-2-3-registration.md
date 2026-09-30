# Onda 2.3 — U8 Registrazione (repeatable, somma, chip nominate, unità tempo) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make counter registration self-explanatory: a per-habit **repeatable** flag decides between a simple fields+Save module (second same-day save **sums** in the day total) and **named, renamable series chips** ("Notte"/"Riposino", persisted in `series_labels_json`); duration fields get a **time-unit selector** (h/min/sec) normalized to seconds on save; records/export show durations in the metric's unit. Also fixes the 2.2 counter "today total" that showed occurrences instead of the metric sum.

**Architecture:** No new schema — `habit_config.repeatable` and `habit_config.series_labels_json` already exist (Onda 2.0, migration `PerTypePrefsV214`, both **versioned** in `HabitConfigHistory`). The work is service plumbing + WPF: `HabitScreenViewModel.BuildQuickAddAsync` routes the counter quick-add by config; `CounterSeriesQuickAddViewModel` gains chips (selection/rename/add) over its existing `Series` rows — non-selected rows are hidden by a row flag, NOT re-hosted, so the per-cell chrono's `RelativeSource AncestorType=ContentControl` binding keeps working; `SeriesCellViewModel` swaps the mm:ss mask for value+unit input. Templates: payload keys `repeatable`/`series_labels` + Sleep seed update via one **data-only migration** (v2.15).

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm, EF Core + SQLite, xUnit + FluentAssertions.

## Global Constraints

- **English-first strings = 3 edits each:** key in `src/Recordum.WPF/Resources/Strings.resx` (EN) + `Strings.it.resx` (IT) + manual accessor in `src/Recordum.WPF/Resources/Strings.Designer.cs`. The `guard-generated.js` hook denies Edit/Write on `*.Designer.cs` → **edit `Strings.Designer.cs` only via the Bash shell with `sed`** (this VM has no python).
- **`IClock` for time, never `DateTime.Now`.** `Completion.CompletedAt` is `DateTime` → use `clock.UtcNow.UtcDateTime`.
- **Analyzer rules are errors:** CA1859 (concrete types on private fields; also fires when a `switch` produces a single type), CA1863 (`#pragma warning disable CA1863` around low-frequency `string.Format` with resx — codebase precedent), CA1861 (hoist constant arrays to `static readonly`), CA1822 (make members static when possible).
- **FluentAssertions `ContainSingle`/`OnlyContain` use expression trees** → write `== null`, never `is null`, inside their lambdas.
- **Never `Clear()` a bound ItemsSource while persist handlers can fire** (data-wipe `4d01f9c`): guard loads with a `_loading` flag or detach first. The chips collection here has no two-way persisted selection, but `LoadMetricsAsync` must still not trigger rename persistence while rebuilding.
- **WPF ignores `StringFormat` on an object-typed `Content` binding** → use `ContentStringFormat` (bug `a47475d`).
- **Build/test:** `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo` and `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --nologo`. If the app is running it locks `Recordum.WPF.dll` — kill the specific PID named in the MSB3021 error, not all dotnet. Note: `-p:UseAppHost=false` also deletes the WPF exe → a later `dotnet run --project src/Recordum.WPF` must run **without** `--no-build`.
- **Branch:** `wave-2-3-registration` off `main` (≥ `a47475d`). The `guard-main.js` hook blocks direct commits on `main`.
- **Migrations:** `dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.WPF`; verify the generated SQL against the schema docs **before** `database update`. Never hand-edit `*.Designer.cs`/`*ModelSnapshot.cs` (hook-blocked; regen via `dotnet ef`).
- **Author visual verification closes the wave** (`verified-fixed`), not green tests alone.

## Environment / Interface facts (verified 2026-07-13)

- `HabitConfig` (`src/Recordum.Core/Models/HabitConfig.cs`) already has `public bool Repeatable` and `public string? SeriesLabelsJson`; both listed in `HabitConfigHistoryColumns.Versioned`. `IHabitConfigService.UpsertAsync(Guid habitNodeId, Action<HabitConfig> mutate, ct)` persists (and the interceptor snapshots history).
- `CounterSeriesQuickAddViewModel` (`src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/CounterSeriesQuickAddViewModel.cs`): ctor `(ICompletionService, IMetricDefinitionService, IReferenceDataQueryService, IClock, IActiveTimerService)`, `ObservableCollection<SeriesRowViewModel> Series`, `AddSeriesCommand`, `RemoveSeriesCommand(row)` (CanExecute `Series.Count > 1`), `ValidSeriesCount`, `SaveAsync` builds `CompletionValue`s with `cell.BuildValue(i)` per valid row. Built in `HabitScreenViewModel.BuildQuickAddAsync` → local `BuildCounterQuickAddAsync`: 0 metrics → `CounterTapQuickAddViewModel`, else series VM (then `LoadMetricsAsync` in the caller's `switch`).
- `SeriesCellViewModel` (same folder): duration cells (`IsDuration` = Measured+Seconds) currently use a mm:ss mask (`DurationText`, `IsDurationInvalid`, `SetDurationSeconds`); `BuildValue(seriesIndex)` writes `ValueSeconds`. Ctor `(MetricDefinition metric, string? unitCode = null, ScaleInfo? scale = null)` — `unitCode` comes from `MetricCellFactory` (codes seeded: `"hours"`, `"minutes"`, `"seconds"`, `"l"`, …).
- **The per-cell chrono button** in `MetricCellTemplate` (`src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` ~line 401) reaches `ToggleChronoCommand` via `RelativeSource AncestorType=ContentControl` → the quick-add host `ContentControl`. **Do not** wrap the selected series in a nested `ContentControl` (it would shadow that ancestor); hide non-selected rows with a row flag instead.
- `HabitScreenViewModel` (`src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`): has `_configs` (`IHabitConfigService`); `OnHabitUpdatedEvent` fires on `WellKnownEvents.HabitUpdated`, which `HabitEditorViewModel` (line ~379) publishes after every section apply → a Configura toggle can live-refresh the quick-add. `PushDaySummaryAsync` builds the day strip from `GetDailyMagnitudeAsync` + primary unit/scale labels via `ResolvePrimaryDisplayAsync` and `DaySummaryFormatter.Label(type, mag, unit, scaleLabels)`.
- **Counter totals mismatch (2.2 defect to fix here):** `ICompletionService.GetDailyMagnitudeAsync` returns **occurrences** for Counter (distinct series rows, floor 1) — but the Home row's `TodayCounterTotal` (in `HabitTreeQueryService`) **sums the primary metric's values**. `CounterTodayContent.TodayTotal` (2.2 Task 6) and `PushDaySummaryAsync` both use magnitude + append the unit code → "1 pg" after recording 22 pages. The correct per-day metric sum API exists: `ICompletionService.GetDailySumAsync(habitId, metricDefinitionId, fromDate, toDate, ct)` → `IReadOnlyList<DailyValue>`.
- `HabitTemplateService` (`src/Recordum.Data/Services/HabitTemplateService.cs`): payload DTOs are private classes deserialized with snake_case options (`s_jsonOptions`); `ConfigPayload` currently ends at `TimerTargetSeconds`. `InstantiateAsync` maps config inside `configs.UpsertAsync(newId, c => …)`.
- `HabitTemplateSeed` (`src/Recordum.Data/SeedData/HabitTemplateSeed.cs`): payloads are string constants used by EF `HasData` → changing `SleepTrackerPayload` requires a **new migration** (EF emits `UpdateData`). `AppSettingSeed` holds the `schema_version` constant (currently `2.14`); bump to `2.15` in the same migration.
- `HabitBuilderViewModel` (`src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs`): `BuildRequest()` → `ComposedHabitRequest(node, config, metrics)`; `BuildConfig(Guid nodeId)` is a `static` one-liner returning a default `HabitConfig`; `BuildTemplatePayloadJson()` serializes the composition snake_case for template save. View: `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml`.
- Configura section pattern = `HomeRowSectionViewModel` (2.1): `SectionViewModelBase`, `IsApplicable(NodeType, HabitType?)`, `_loading` guard, persistence in `OnXChanged`, registered in `HabitEditorViewModel` (property + ctor init + `Sections` array) + implicit DataTemplate in `src/Recordum.WPF/Views/HabitEditor/Sections/SectionTemplates.xaml`. **A8**: section content must respect the column width (no fixed widths > ~300px).
- Records display: `CompletionListViewModel.FormatValue` (~line 130) shows `ValueSeconds` via `DurationFormat.Format(secs)` (h:mm). `MetricDisplay.Unit` in the same file carries the metric's unit code.
- Export: `ExportQueryService` (`src/Recordum.Data/Services/Queries/ExportQueryService.cs` ~line 88) projects the primary `CompletionValue` to a bare decimal (`ValueSeconds` → raw seconds) into `ExportCompletionRow`; it already lists metrics via `metrics.ListByHabitAsync` but resolves no unit codes.
- Existing reusable strings: `HabitScreen.QuickAdd.Save` ("Save"/"Salva"), `HabitScreen.QuickAdd.SaveSeries` ("Record {0} series"/"Registra {0} serie"), `HabitScreen.QuickAdd.AddSeries` ("Add series"/"Aggiungi serie").

## Per la matrice (spec §1/§3) — cosa cambia e cosa no

Solo la registrazione **Counter con metriche** cambia faccia. Action/Journal/State/Resistance quick-add **invariati** (le celle durata di Measured ereditano però il selettore unità — è lo stesso `MetricCellTemplate`). `CounterTapQuickAddViewModel` (counter senza metriche) invariato: il tap è intrinsecamente ripetibile.

---

## Task 1: Counter day totals = metric sum (fix the 2.2 occurrences bug, two sites)

`CounterTodayContent.TodayTotal` and the day-summary strip must show the **sum of the primary metric's values** for the day (like the Home row), not the completion/series count. Bare counters (no metrics) keep the occurrence count.

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Today/CounterTodayContent.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (`PushDaySummaryAsync`)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Today/CounterTodayContentTests.cs`

**Interfaces:**
- Consumes: `ICompletionService.GetDailySumAsync(Guid habitId, Guid metricDefinitionId, DateOnly from, DateOnly to, ct)` → `IReadOnlyList<DailyValue>` (`DailyValue(DateOnly Date, decimal Value)`); `IMetricDefinitionService.ListByHabitAsync`; `FakeCompletionService.DailySumByKey[(habitId, metricId)]`.
- Produces: `CounterTodayContent.TodayTotal` = today's primary-metric sum when a metric exists, else today's magnitude.

- [ ] **Step 1: Failing test.** In `CounterTodayContentTests`, replace/extend the total test:

```csharp
[Fact]
public async Task TodayTotal_sums_the_primary_metric_not_the_occurrences()
{
    var (sut, completions, metrics, refData, _, node) = NewSut();
    var today = DateOnly.FromDateTime(Now.UtcDateTime);
    var litre = refData.Units[0];
    var metric = new MetricDefinition
    { Id = Guid.NewGuid(), HabitNodeId = node.Id, SortOrder = 0, UnitOfMeasureId = litre.Id, Name = "vol" };
    metrics.Store[metric.Id] = metric;
    // Two registrations today (1.5 + 0.5) → the magnitude would be 2 occurrences; the total must be 2.0 summed.
    completions.DailySumByKey[(node.Id, metric.Id)] = new List<DailyValue> { new(today, 2.0m) };
    completions.MagnitudeByHabit[node.Id] = new List<DailyMagnitude> { new(today, 2m) }; // decoy: same number, different meaning

    await sut.LoadAsync(node);

    sut.TodayTotal.Should().Be(2.0m);
    sut.UnitCode.Should().Be(litre.Code);
}

[Fact]
public async Task Bare_counter_without_metrics_falls_back_to_the_day_magnitude()
{
    var (sut, completions, _, _, _, node) = NewSut();
    var today = DateOnly.FromDateTime(Now.UtcDateTime);
    completions.MagnitudeByHabit[node.Id] = new List<DailyMagnitude> { new(today, 3m) };

    await sut.LoadAsync(node);

    sut.TodayTotal.Should().Be(3m); // 3 taps
}
```

Add `using Recordum.Core.Abstractions.Services.Dtos;` if missing. The existing `TodayTotal_and_UnitCode_come_from_the_day_magnitude_and_primary_metric` test asserts the old behavior — rewrite it (it seeds a metric, so it must now read from `DailySumByKey`; delete the magnitude seeding or keep it as decoy).

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --nologo --filter "FullyQualifiedName~CounterTodayContentTests"`
Expected: the new sum test FAILS (TodayTotal is the magnitude, not 2.0m with a differing seed — make the seeds differ, e.g. sum 2.0 vs magnitude 5).

- [ ] **Step 3: Implement.** In `CounterTodayContent.LoadAsync`, resolve the primary metric first and branch:

```csharp
public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(node);
    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.UtcNow, _clock.LocalTimeZone).DateTime);

    var metrics = await _metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(true);
    var primary = metrics.OrderBy(m => m.SortOrder).FirstOrDefault();

    if (primary is not null)
    {
        // Sum of the primary metric's values today — same semantics as the Home row's TodayCounterTotal.
        var sums = await _completions.GetDailySumAsync(node.Id, primary.Id, today, today, ct).ConfigureAwait(true);
        TodayTotal = sums.FirstOrDefault(s => s.Date == today)?.Value ?? 0m;
        UnitCode = await ResolveUnitCodeAsync(primary, ct).ConfigureAwait(true);
    }
    else
    {
        // Bare counter: occurrences are the total (each tap = 1).
        var mags = await _completions.GetDailyMagnitudeAsync(node.Id, today.Year, today.Month, ct).ConfigureAwait(true);
        TodayTotal = mags.FirstOrDefault(m => m.Date == today)?.Magnitude ?? 0m;
        UnitCode = null;
    }

    await _goalCard.LoadAsync(node, ct).ConfigureAwait(true);
    HasGoal = !_goalCard.IsEmpty;
    GoalFraction = HasGoal ? Math.Clamp(_goalCard.PercentComplete, 0d, 1d) : 0d;
}

private async Task<string?> ResolveUnitCodeAsync(MetricDefinition primary, CancellationToken ct)
{
    if (primary.UnitOfMeasureId is not { } uid) return null;
    var units = await _refData.ListUnitsAsync(ct).ConfigureAwait(true);
    return units.FirstOrDefault(u => u.Id == uid)?.Code;
}
```

(Adjust `DailyValue` member access to the actual record shape — check `src/Recordum.Core/Abstractions/Services/Dtos/DailyValue.cs`.) In `HabitScreenViewModel.PushDaySummaryAsync`, apply the same branch: when the node is a Counter with a primary metric, replace `mag` with the day's `GetDailySumAsync` value before calling `DaySummaryFormatter.Label` (presence still comes from `dayMag is not null`); other types unchanged.

- [ ] **Step 4: Run the counter tests + full WPF suite** — all green.
- [ ] **Step 5: Commit** — `fix(wpf): counter day totals sum the primary metric, not occurrences`

---

## Task 2: Time-unit selector on duration cells (normalize to seconds)

Replace the mm:ss mask with a decimal value + unit ComboBox (hours/min/sec). Default unit = the metric's unit code when it is h/m/s, else minutes. The chrono pours seconds → unit flips to seconds. Storage stays `value_seconds` (no schema change).

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/TimeUnit.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (the duration block of `MetricCellTemplate`)
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs` (via sed)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModelTests.cs`

**Interfaces:**
- Produces: `enum TimeUnit { Hours, Minutes, Seconds }`; record `TimeUnitOption(TimeUnit Unit, string Label)`; on `SeriesCellViewModel`: `decimal? DurationValue`, `TimeUnit DurationUnit`, `static IReadOnlyList<TimeUnitOption> TimeUnitOptions`; `SetDurationSeconds(int)` now sets `DurationUnit = Seconds` + `DurationValue = seconds`. `DurationText`/`IsDurationInvalid` are **removed** (update their tests). `BuildValue` normalizes to whole seconds.
- New strings: `TimeUnit.Hours` = "hours"/"ore", `TimeUnit.Minutes` = "min"/"min", `TimeUnit.Seconds` = "sec"/"sec".

- [ ] **Step 1: Failing tests** (replace the mask tests in `SeriesCellViewModelTests`):

```csharp
[Fact]
public void Duration_cell_normalizes_hours_to_seconds_on_build()
{
    var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
    var cell = new SeriesCellViewModel(metric, unitCode: "hours");
    cell.DurationUnit.Should().Be(TimeUnit.Hours);       // default follows the metric unit
    cell.DurationValue = 7.5m;

    var v = cell.BuildValue(0);

    v!.ValueSeconds.Should().Be(27000);                   // 7.5 h × 3600
}

[Fact]
public void Duration_cell_defaults_to_minutes_when_the_metric_has_no_time_unit()
{
    var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
    var cell = new SeriesCellViewModel(metric);
    cell.DurationUnit.Should().Be(TimeUnit.Minutes);
    cell.DurationValue = 90m;

    cell.BuildValue(0)!.ValueSeconds.Should().Be(5400);
}

[Fact]
public void Chrono_pour_sets_seconds_unit_and_value()
{
    var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
    var cell = new SeriesCellViewModel(metric, unitCode: "hours");

    cell.SetDurationSeconds(125);

    cell.DurationUnit.Should().Be(TimeUnit.Seconds);
    cell.DurationValue.Should().Be(125m);
    cell.BuildValue(0)!.ValueSeconds.Should().Be(125);
}

[Fact]
public void Empty_duration_builds_no_value()
{
    var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "d", Type = MetricType.Seconds };
    var cell = new SeriesCellViewModel(metric);
    cell.BuildValue(0).Should().BeNull();
}
```

- [ ] **Step 2: Run to verify failure** (`--filter "FullyQualifiedName~SeriesCellViewModelTests"`) — FAIL: `TimeUnit`/`DurationValue` don't exist.

- [ ] **Step 3: Implement.** `TimeUnit.cs`:

```csharp
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Input unit for duration cells (spec U8 §3): the value is typed in this unit and
/// normalized to whole seconds at save (storage stays value_seconds, no schema change).</summary>
public enum TimeUnit { Hours, Minutes, Seconds }

/// <summary>A selectable time unit with its localized label (ComboBox item).</summary>
public sealed record TimeUnitOption(TimeUnit Unit, string Label);
```

In `SeriesCellViewModel`: delete `DurationText`, `IsDurationInvalid`; add:

```csharp
[ObservableProperty] private decimal? _durationValue;
[ObservableProperty] private TimeUnit _durationUnit = TimeUnit.Minutes;

/// <summary>Static so the XAML binds it via x:Static (one list app-wide).</summary>
public static IReadOnlyList<TimeUnitOption> TimeUnitOptions { get; } =
[
    new(TimeUnit.Hours, Strings.TimeUnit_Hours),
    new(TimeUnit.Minutes, Strings.TimeUnit_Minutes),
    new(TimeUnit.Seconds, Strings.TimeUnit_Seconds),
];

public void SetDurationSeconds(int seconds)
{
    DurationUnit = TimeUnit.Seconds;
    DurationValue = seconds;
}
```

In the ctor (after `UnitLabel = unitCode;`):

```csharp
if (IsDuration)
    DurationUnit = unitCode?.ToLowerInvariant() switch
    {
        "hours" => TimeUnit.Hours,
        "seconds" => TimeUnit.Seconds,
        _ => TimeUnit.Minutes,
    };
```

`BuildValue` Seconds branch:

```csharp
case MetricType.Seconds:
    if (DurationValue is not { } dur) return null;
    v.ValueSeconds = (int)Math.Round(dur * DurationUnit switch
    {
        TimeUnit.Hours => 3600m,
        TimeUnit.Minutes => 60m,
        _ => 1m,
    });
    return v;
```

Keep `DurationFormat` (display formatting is used elsewhere); only the mask input dies. Strings (3 edits each; Designer via `sed`, e.g. `sed -i '/Today_Journal_QuickNote => Get/a\    public static string TimeUnit_Hours => Get("TimeUnit.Hours");\n    public static string TimeUnit_Minutes => Get("TimeUnit.Minutes");\n    public static string TimeUnit_Seconds => Get("TimeUnit.Seconds");' src/Recordum.WPF/Resources/Strings.Designer.cs`).

- [ ] **Step 4: XAML.** In `MetricCellTemplate`, replace the `DurationText` TextBox block with:

```xaml
<!-- Measured: Seconds → decimal value + time-unit selector; normalized to seconds on save (U8). -->
<StackPanel Orientation="Horizontal" VerticalAlignment="Center">
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsDuration}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>
    <TextBox Width="70"
             Text="{Binding DurationValue, UpdateSourceTrigger=PropertyChanged, TargetNullValue=''}"/>
    <ComboBox Width="72" Margin="4,0,0,0"
              ItemsSource="{x:Static qa:SeriesCellViewModel.TimeUnitOptions}"
              SelectedValue="{Binding DurationUnit}"
              SelectedValuePath="Unit" DisplayMemberPath="Label"/>
</StackPanel>
```

The duration cell no longer shows the raw `UnitLabel` suffix twice — leave the existing trailing `UnitLabel` TextBlock as is (it's null-gated and duration metrics rarely carry a display unit; if the author sees a double unit at verification, hide it for `IsDuration`).

- [ ] **Step 5: Run cell tests + full WPF suite** — green (fix any test still referencing `DurationText`).
- [ ] **Step 6: Commit** — `feat(wpf): time-unit selector on duration cells, normalized to seconds (U8)`

---

## Task 3: Repeatable + series-chips plumbing in the counter quick-add (VM only)

`CounterSeriesQuickAddViewModel` learns its registration mode from config and exposes named chips over its `Series` rows. Non-selected rows hide via a flag on `SeriesRowViewModel` (keeps the chrono ancestor chain intact). Renames persist to `series_labels_json`. Routing + live refresh in `HabitScreenViewModel`.

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesChipViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/CounterSeriesQuickAddViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesRowViewModel.cs` (add `IsVisibleRow`)
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (routing + `OnHabitUpdatedEvent` refresh)
- Modify: `src/Recordum.WPF/Resources/Strings.resx` / `.it.resx` / `Strings.Designer.cs` (sed)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/CounterSeriesChipsTests.cs` (new)

**Interfaces:**
- Consumes: `IHabitConfigService` (new ctor param — **update every construction site**: `HabitScreenViewModel.BuildQuickAddAsync`, `QuickAddViewModelTests`, any factory), `HabitConfig.Repeatable`, `HabitConfig.SeriesLabelsJson` (JSON `string[]`).
- Produces: `SeriesChipViewModel { int Index; string Name; bool IsSelected; bool IsEditing; string PendingName }`; on the quick-add VM: `bool IsRepeatable` (observable), `ObservableCollection<SeriesChipViewModel> Chips`, `int SelectedSeriesIndex` (observable), `void SetRegistrationConfig(bool repeatable, string? seriesLabelsJson)` (call **before** `LoadMetricsAsync`), commands `SelectSeriesCommand(SeriesChipViewModel)`, `BeginRenameChipCommand(SeriesChipViewModel)`, `CommitRenameChipCommand(SeriesChipViewModel)` (async), `CancelRenameChipCommand(SeriesChipViewModel)`; `SeriesRowViewModel.IsVisibleRow` (observable, default `true`).
- New strings: `HabitScreen.QuickAdd.SeriesFallback` = "Series {0}"/"Serie {0}", `HabitScreen.QuickAdd.RenameSeries` = "Rename series"/"Rinomina serie".

- [ ] **Step 1: Failing tests** (`CounterSeriesChipsTests.cs`; reuse the construction pattern from the existing quick-add tests, adding a `FakeHabitConfigService`):

```csharp
[Fact]
public async Task Repeatable_load_builds_one_named_chip_from_labels_with_fallback()
{
    var (vm, node, _) = await LoadRepeatableAsync(seriesLabelsJson: "[\"Notte\",\"Riposino\"]");
    vm.IsRepeatable.Should().BeTrue();
    vm.Chips.Should().HaveCount(1);
    vm.Chips[0].Name.Should().Be("Notte");
    vm.Chips[0].IsSelected.Should().BeTrue();

    vm.AddSeriesCommand.Execute(null);
    vm.Chips.Should().HaveCount(2);
    vm.Chips[1].Name.Should().Be("Riposino");     // second label
    vm.SelectedSeriesIndex.Should().Be(1);          // the new series is selected
    vm.Series[0].IsVisibleRow.Should().BeFalse();   // only the selected row shows
    vm.Series[1].IsVisibleRow.Should().BeTrue();

    vm.AddSeriesCommand.Execute(null);
    vm.Chips[2].Name.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.HabitScreen_QuickAdd_SeriesFallback, 3));
}

[Fact]
public async Task Non_repeatable_load_shows_no_chips_and_a_single_visible_row()
{
    var (vm, _, _) = await LoadRepeatableAsync(repeatable: false);
    vm.IsRepeatable.Should().BeFalse();
    vm.Chips.Should().BeEmpty();
    vm.Series.Should().HaveCount(1);
    vm.Series[0].IsVisibleRow.Should().BeTrue();
}

[Fact]
public async Task Renaming_a_chip_persists_the_labels_json()
{
    var (vm, node, configs) = await LoadRepeatableAsync(seriesLabelsJson: null);
    var chip = vm.Chips[0];
    vm.BeginRenameChipCommand.Execute(chip);
    chip.IsEditing.Should().BeTrue();
    chip.PendingName = "Notte";
    await vm.CommitRenameChipCommand.ExecuteAsync(chip);

    chip.IsEditing.Should().BeFalse();
    chip.Name.Should().Be("Notte");
    (await configs.GetAsync(node.Id))!.SeriesLabelsJson.Should().Be("[\"Notte\"]");
}

[Fact]
public async Task Selecting_a_chip_switches_the_visible_row()
{
    var (vm, _, _) = await LoadRepeatableAsync();
    vm.AddSeriesCommand.Execute(null);
    vm.SelectSeriesCommand.Execute(vm.Chips[0]);

    vm.SelectedSeriesIndex.Should().Be(0);
    vm.Series[0].IsVisibleRow.Should().BeTrue();
    vm.Series[1].IsVisibleRow.Should().BeFalse();
}
```

Helper `LoadRepeatableAsync(bool repeatable = true, string? seriesLabelsJson = null)`: builds the VM with fakes (`FakeCompletionService`, `FakeMetricDefinitionService` seeded with 1 metric for the node, `FakeReferenceData`, `FakeClock`, `FakeActiveTimerService`, `FakeHabitConfigService`), sets `Node`, calls `SetRegistrationConfig(repeatable, seriesLabelsJson)`, then `LoadMetricsAsync()`; returns `(vm, node, configs)`.

- [ ] **Step 2: Run to verify failure** — FAIL: members don't exist.

- [ ] **Step 3: Implement.** `SeriesChipViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>One named series chip in the repeatable counter quick-add (U8): "Notte", "Riposino 1", …
/// Index = the series row it fronts; renames are committed by the parent VM (persistence lives there).</summary>
public sealed partial class SeriesChipViewModel(int index, string name) : ObservableObject
{
    public int Index { get; } = index;
    [ObservableProperty] private string _name = name;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _pendingName = string.Empty;
}
```

`SeriesRowViewModel`: add `[ObservableProperty] private bool _isVisibleRow = true;` (default true keeps Measured/Resistance rows visible).

`CounterSeriesQuickAddViewModel` — new ctor param + members (full additions):

```csharp
private readonly IHabitConfigService _configs;      // ctor: append IHabitConfigService configs param, assign
private string[] _seriesLabels = [];

[ObservableProperty] private bool _isRepeatable;
[ObservableProperty] private int _selectedSeriesIndex;
public ObservableCollection<SeriesChipViewModel> Chips { get; } = [];

/// <summary>Call BEFORE LoadMetricsAsync: fixes the registration mode + the persisted series names.</summary>
public void SetRegistrationConfig(bool repeatable, string? seriesLabelsJson)
{
    IsRepeatable = repeatable;
    _seriesLabels = ParseSeriesLabels(seriesLabelsJson);
}

private static string[] ParseSeriesLabels(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return [];
    try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? []; }
    catch (System.Text.Json.JsonException) { return []; }
}

private string LabelFor(int i)
{
    if (i < _seriesLabels.Length && !string.IsNullOrWhiteSpace(_seriesLabels[i])) return _seriesLabels[i];
#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
    return string.Format(CultureInfo.CurrentCulture, Strings.HabitScreen_QuickAdd_SeriesFallback, i + 1);
#pragma warning restore CA1863
}

/// <summary>Rebuilds the chip list from the current rows, clamps the selection, and re-applies
/// row visibility. Chips carry no persist handlers, so clearing here is safe (4d01f9c rule).</summary>
private void RebuildChips()
{
    Chips.Clear();
    if (IsRepeatable)
        for (var i = 0; i < Series.Count; i++)
            Chips.Add(new SeriesChipViewModel(i, LabelFor(i)));
    if (SelectedSeriesIndex >= Series.Count) SelectedSeriesIndex = Math.Max(0, Series.Count - 1);
    ApplySelection();
}

private void ApplySelection()
{
    foreach (var chip in Chips) chip.IsSelected = chip.Index == SelectedSeriesIndex;
    for (var i = 0; i < Series.Count; i++)
        Series[i].IsVisibleRow = !IsRepeatable || i == SelectedSeriesIndex;
}

partial void OnSelectedSeriesIndexChanged(int value) => ApplySelection();

[RelayCommand]
private void SelectSeries(SeriesChipViewModel chip) => SelectedSeriesIndex = chip.Index;

[RelayCommand]
private void BeginRenameChip(SeriesChipViewModel chip)
{
    chip.PendingName = chip.Name;
    chip.IsEditing = true;
}

[RelayCommand]
private void CancelRenameChip(SeriesChipViewModel chip) => chip.IsEditing = false;

[RelayCommand]
private async Task CommitRenameChipAsync(SeriesChipViewModel chip)
{
    var name = chip.PendingName?.Trim();
    chip.IsEditing = false;
    if (string.IsNullOrEmpty(name) || name == chip.Name || Node is null) return;
    chip.Name = name;
    var labels = new string[Math.Max(_seriesLabels.Length, chip.Index + 1)];
    for (var i = 0; i < labels.Length; i++) labels[i] = i < _seriesLabels.Length ? _seriesLabels[i] : string.Empty;
    labels[chip.Index] = name;
    _seriesLabels = labels;
    var json = System.Text.Json.JsonSerializer.Serialize(_seriesLabels);
    await _configs.UpsertAsync(Node.Id, c => c.SeriesLabelsJson = json).ConfigureAwait(true);
}
```

Change `LoadMetricsAsync` (after `Series.Add(NewRow());`): `SelectedSeriesIndex = 0; RebuildChips();`. Change `AddSeries`:

```csharp
[RelayCommand]
private void AddSeries()
{
    if (_orderedMetrics.Count == 0) return;
    Series.Add(NewRow());
    RebuildChips();
    SelectedSeriesIndex = Series.Count - 1;
}
```

Change `RemoveSeries` to end with `RebuildChips();`. Add `using System.Globalization;` if missing.

- [ ] **Step 4: Routing.** In `HabitScreenViewModel.BuildQuickAddAsync`'s local `BuildCounterQuickAddAsync`:

```csharp
async Task<QuickAddViewModelBase> BuildCounterQuickAddAsync(CancellationToken c)
{
    var defs = await _metrics.ListByHabitAsync(node.Id, c).ConfigureAwait(true);
    if (defs.Count == 0)
        return new CounterTapQuickAddViewModel(_completions, _clock);
    var cfg = await _configs.GetAsync(node.Id, c).ConfigureAwait(true);
    var vm = new CounterSeriesQuickAddViewModel(_completions, _metrics, _refData, _clock, _timers, _configs);
    vm.SetRegistrationConfig(cfg?.Repeatable ?? false, cfg?.SeriesLabelsJson);
    return vm;
}
```

And in `OnHabitUpdatedEvent` (after the node refresh), live-apply a Configura repeatable flip without wiping in-progress input on unrelated edits:

```csharp
// A Configura "repeatable" flip must re-shape the open registration module (the editor publishes
// HabitUpdated after every section apply). Rebuild only when the mode actually changed, so a
// rename/recolor never wipes half-typed values.
if (QuickAdd is CounterSeriesQuickAddViewModel cs)
{
    var cfg = await _configs.GetAsync(refreshed.Id).ConfigureAwait(true);
    if (cfg is not null && cfg.Repeatable != cs.IsRepeatable)
        await BuildQuickAddAsync(refreshed, CancellationToken.None).ConfigureAwait(true);
}
```

Update every `new CounterSeriesQuickAddViewModel(...)` site (grep it: `HabitScreenViewModel` + `QuickAddViewModelTests` and friends) to pass the config service (tests: `new FakeHabitConfigService()`).

- [ ] **Step 5: Strings** (3 edits × 2 keys; Designer via sed). Run chips tests + full WPF suite — green.
- [ ] **Step 6: Commit** — `feat(wpf): repeatable routing + named series chips in the counter quick-add (U8)`

---

## Task 4: Quick-add XAML — chips row, rename UI, mode-aware Save

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (`CounterSeriesQuickAddTemplate` + the series-row template)

**Interfaces:**
- Consumes: everything Task 3 produced. Existing styles/converters: `BoolToVisible`, `InvBoolToVisible`, `Brush.*`, `Strings.HabitScreen_QuickAdd_*`.

- [ ] **Step 1: Chips row.** Inside `CounterSeriesQuickAddTemplate`, insert **above** the series `ItemsControl`:

```xaml
<!-- Series chips (repeatable only): named, renamable, one selected at a time (U8). -->
<StackPanel Orientation="Horizontal" Margin="0,0,0,8"
            Visibility="{Binding IsRepeatable, Converter={StaticResource BoolToVisible}}">
    <ItemsControl ItemsSource="{Binding Chips}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Grid Margin="0,0,6,0">
                    <StackPanel Orientation="Horizontal"
                                Visibility="{Binding IsEditing, Converter={StaticResource InvBoolToVisible}}">
                        <Button Content="{Binding Name}" Padding="10,3" Cursor="Hand"
                                Command="{Binding DataContext.SelectSeriesCommand,
                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                CommandParameter="{Binding}">
                            <Button.Style>
                                <Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
                                    <Setter Property="Foreground" Value="{DynamicResource Brush.TextMuted}"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsSelected}" Value="True">
                                            <Setter Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                                            <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Button.Style>
                        </Button>
                        <!-- ✎ rename, only on the selected chip (same inline-edit affordance as rows) -->
                        <Button Width="22" Height="22" Padding="0" Background="Transparent" BorderThickness="0"
                                Content="✎" FontSize="11"
                                ToolTip="{x:Static res:Strings.HabitScreen_QuickAdd_RenameSeries}"
                                Command="{Binding DataContext.BeginRenameChipCommand,
                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                CommandParameter="{Binding}"
                                Visibility="{Binding IsSelected, Converter={StaticResource BoolToVisible}}"/>
                    </StackPanel>
                    <TextBox Width="110"
                             Text="{Binding PendingName, UpdateSourceTrigger=PropertyChanged}"
                             Visibility="{Binding IsEditing, Converter={StaticResource BoolToVisible}}">
                        <TextBox.InputBindings>
                            <KeyBinding Key="Return"
                                        Command="{Binding DataContext.CommitRenameChipCommand,
                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                        CommandParameter="{Binding}"/>
                            <KeyBinding Key="Escape"
                                        Command="{Binding DataContext.CancelRenameChipCommand,
                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                        CommandParameter="{Binding}"/>
                        </TextBox.InputBindings>
                    </TextBox>
                </Grid>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
    <Button Content="{x:Static res:Strings.HabitScreen_QuickAdd_AddSeries}" Padding="8,3"
            Command="{Binding AddSeriesCommand}"/>
</StackPanel>
```

- [ ] **Step 2: Row visibility.** On the series-row `DataTemplate`'s root `Border` (the one with `BorderThickness="1" … CornerRadius="3"`), add `Visibility="{Binding IsVisibleRow, Converter={StaticResource BoolToVisible}}"`. Remove the old validity marker column **only if** it fights the layout — otherwise leave it.

- [ ] **Step 3: Bottom bar.** Remove the old "Aggiungi serie" button from the bottom `StackPanel` (it moved into the chips row and must not appear for non-repeatable). Replace the Save button with a mode-aware one (note `ContentStringFormat`, never `StringFormat` on the Content binding — `a47475d`):

```xaml
<Button Command="{Binding SaveCommand}" Padding="12,4">
    <Button.Style>
        <Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Content" Value="{x:Static res:Strings.HabitScreen_QuickAdd_Save}"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsRepeatable}" Value="True">
                    <Setter Property="Content" Value="{Binding ValidSeriesCount}"/>
                    <Setter Property="ContentStringFormat"
                            Value="{x:Static res:Strings.HabitScreen_QuickAdd_SaveSeries}"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```

- [ ] **Step 4: Build + full WPF suite** (XAML compiles through the test build) — green.
- [ ] **Step 5: Visual smoke** (optional but cheap): `dotnet run --project src/Recordum.WPF` — a repeatable counter shows chips + one row; a non-repeatable one shows fields + "Salva".
- [ ] **Step 6: Commit** — `feat(wpf): chips-based series UI + mode-aware Save in the counter quick-add`

---

## Task 5: "Ripetibile" nel builder e in Configura

Checkbox "Si può registrare più volte al giorno" nel builder (Livello 1, visibile per Counter) → `HabitConfig.Repeatable` al Lancia e nel payload template salvato. In Configura, nuova sezione "Registrazione" (solo Counter).

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml`
- Create: `src/Recordum.WPF/ViewModels/HabitEditor/Sections/RegistrationSectionViewModel.cs`
- Create: `src/Recordum.WPF/Views/HabitEditor/Sections/RegistrationSectionView.xaml` (+ `.xaml.cs`)
- Modify: `src/Recordum.WPF/Views/HabitEditor/Sections/SectionTemplates.xaml`, `src/Recordum.WPF/ViewModels/HabitEditor/HabitEditorViewModel.cs`
- Modify: strings ×3 file
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs` (extend), `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/RegistrationSectionTests.cs` (new)

**Interfaces:**
- Produces: `HabitBuilderViewModel.Repeatable` (observable bool, default false) → `BuildConfig` + `BuildTemplatePayloadJson` (key `repeatable`); `RegistrationSectionViewModel : SectionViewModelBase` with `Title = Strings.Editor_Section_Registration`, `bool Repeatable` persisted via `IHabitConfigService.UpsertAsync`, `IsApplicable(node, habit) => node == NodeType.Habit && habit == HabitType.Counter`, `Task LoadAsync(HabitNode node)` (follow `SchedulingSectionViewModel`'s config-backed pattern with the `_loading` guard).
- New strings: `Editor.Section.Registration` = "Recording"/"Registrazione", `Editor.Registration.Repeatable` = "Can be recorded multiple times a day"/"Si può registrare più volte al giorno".

- [ ] **Step 1: Failing tests.**

```csharp
// HabitBuilderViewModelTests
[Fact]
public void Repeatable_flows_into_the_launched_config_and_template_payload()
{
    var vm = CreateSut(); // existing helper
    vm.Name = "Sonno";
    vm.Repeatable = true;

    var request = vm.BuildRequestForTest(); // add an internal test seam if none exists: internal ComposedHabitRequest BuildRequestForTest() => BuildRequest();
    request.Config!.Repeatable.Should().BeTrue();
}
```

```csharp
// RegistrationSectionTests
[Fact]
public async Task Toggle_persists_repeatable_on_the_config()
{
    var configs = new FakeHabitConfigService();
    var section = new RegistrationSectionViewModel(configs);
    var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c" };
    await section.LoadAsync(node);

    section.Repeatable = true;
    await Task.Delay(50); // ApplyAsync is fire-and-forget in sections

    (await configs.GetAsync(node.Id))!.Repeatable.Should().BeTrue();
}

[Fact]
public void Applicable_only_to_counter_habits()
{
    var section = new RegistrationSectionViewModel(new FakeHabitConfigService());
    section.IsApplicable(NodeType.Habit, HabitType.Counter).Should().BeTrue();
    section.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeFalse();
    section.IsApplicable(NodeType.Group, null).Should().BeFalse();
}
```

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement builder.** `[ObservableProperty] private bool _repeatable;`; `BuildConfig` becomes instance:

```csharp
private HabitConfig BuildConfig(Guid nodeId) =>
    new() { Id = Guid.NewGuid(), HabitNodeId = nodeId, Repeatable = Repeatable };
```

Extend the config DTO written by `BuildTemplatePayloadJson` with `repeatable` (snake_case serializer already in place; `DefaultIgnoreCondition = WhenWritingNull` — for a bool emit it always or use `bool?` null-when-false; choose `bool? Repeatable => Repeatable ? true : null` on the DTO so old payloads stay byte-identical). XAML: add below the type/quantity block (Livello 1), visible only for Counter compositions:

```xaml
<CheckBox Content="{x:Static res:Strings.Editor_Registration_Repeatable}"
          IsChecked="{Binding Repeatable}" Margin="0,8,0,0"
          Visibility="{Binding DerivedType, Converter={StaticResource EnumToVisibility},
                       ConverterParameter={x:Static enums:HabitType.Counter}}"/>
```

(Check the file's existing namespace prefixes; `EnumToVisibility` exists — used by the calendar toggles. If `DerivedType` is nullable and the converter chokes, gate via a `ShowRepeatable` computed bool on the VM instead.)

- [ ] **Step 4: Implement the section** (mirror `HomeRowSectionViewModel`'s skeleton, but persistence goes to config):

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

/// <summary>"Registrazione" section (U8): the repeatable knob — can this counter be recorded
/// multiple times a day (named series) or once (second save sums in the day total)?</summary>
public sealed partial class RegistrationSectionViewModel(IHabitConfigService configs) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Registration;

    private readonly IHabitConfigService _configs = configs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private bool _repeatable;

    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit == HabitType.Counter;

    public async Task LoadAsync(HabitNode node)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            var cfg = await _configs.GetAsync(node.Id).ConfigureAwait(true);
            Repeatable = cfg?.Repeatable ?? false;
        }
        finally { _loading = false; }
    }

    partial void OnRepeatableChanged(bool value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.Repeatable = value).AsTask());
    }
}
```

(`ApplyAsync` comes from `SectionViewModelBase` — check its exact name/signature there and in `HomeRowSectionViewModel`, adjust if it differs.) View XAML = one CheckBox, stretch-width (A8):

```xaml
<UserControl x:Class="Recordum.WPF.Views.HabitEditor.Sections.RegistrationSectionView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:res="clr-namespace:Recordum.WPF.Resources">
    <CheckBox Content="{x:Static res:Strings.Editor_Registration_Repeatable}"
              IsChecked="{Binding Repeatable}" Margin="0,2"/>
</UserControl>
```

Register: DataTemplate in `SectionTemplates.xaml`; in `HabitEditorViewModel` add the property, construct with `configs`, insert into the `Sections` array (after Metrics/Scales — registration semantics sit naturally near metrics), **and call its `LoadAsync(node)` where the other sections load** (find the editor's section-load fan-out and add it — the wiring is symmetrical to `HomeRow`). Wire `HabitScreenEditorWiringTests` stays green (no signature change).

- [ ] **Step 5: Strings ×2 keys (3 edits each, sed for Designer). Run tests + full WPF suite** — green.
- [ ] **Step 6: Commit** — `feat(wpf): repeatable toggle in builder + Configura Registrazione section (U8)`

---

## Task 6: Records + export show durations in the metric's unit

**Files:**
- Create: `src/Recordum.Core/Formatting/SecondsDisplay.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs` (`FormatValue`)
- Modify: `src/Recordum.Data/Services/Queries/ExportQueryService.cs` (unit-aware primary value)
- Test: `tests/Recordum.Core.Tests/**/SecondsDisplayTests.cs` (new — put beside existing Core tests; if no Core test project exists, put it in `tests/Recordum.Data.Tests/Unit/SecondsDisplayTests.cs`), plus one case in the existing export query tests (`tests/Recordum.Data.Tests/Integration/ExportQueryServiceTests.cs`)

**Interfaces:**
- Produces: `Recordum.Core.Formatting.SecondsDisplay.InUnit(int seconds, string? unitCode) → string?` (null when the code is not hours/minutes/seconds → caller falls back) and `SecondsDisplay.ConvertToUnit(int seconds, string? unitCode) → decimal?` (numeric conversion for export; null when not a time unit).
- Consumes: `CompletionListViewModel.MetricDisplay.Unit` (unit code) already built in `BuildLookupAsync`.

- [ ] **Step 1: Failing tests.**

```csharp
public class SecondsDisplayTests
{
    [Theory]
    [InlineData(27000, "hours", "7.5 hours")]
    [InlineData(5400, "minutes", "90 minutes")]
    [InlineData(45, "seconds", "45 seconds")]
    public void InUnit_converts_to_the_metric_unit(int secs, string unit, string expected)
        => SecondsDisplay.InUnit(secs, unit).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("l")]
    public void InUnit_returns_null_for_non_time_units(string? unit)
        => SecondsDisplay.InUnit(90, unit).Should().BeNull();

    [Fact]
    public void ConvertToUnit_matches_the_display_conversion()
        => SecondsDisplay.ConvertToUnit(27000, "hours").Should().Be(7.5m);
}
```

(Nota: il test d'aspettativa usa `CultureInfo.InvariantCulture` — implementa con Invariant per stabilità dei test; la UI mostra comunque "7.5"/"7,5" in base al formato che scegli: usa **CurrentCulture nel display** e invariant nei numeri? No — per coerenza col resto del codebase (`FormatMag` usa CurrentCulture) usa `CultureInfo.CurrentCulture` in `InUnit` e nel test confronta con la stessa culture: `7.5m.ToString("0.##", CultureInfo.CurrentCulture) + " hours"`.)

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement.**

```csharp
using System.Globalization;

namespace Recordum.Core.Formatting;

/// <summary>Renders a stored value_seconds in the unit chosen on the metric (U8): "7.5 hours",
/// "90 minutes", "45 seconds". Null when the unit code is not a time unit — callers fall back to
/// their h:mm formatting. Shared by the WPF records list and the export projection.</summary>
public static class SecondsDisplay
{
    public static decimal? ConvertToUnit(int seconds, string? unitCode) => unitCode?.ToLowerInvariant() switch
    {
        "hours" => seconds / 3600m,
        "minutes" => seconds / 60m,
        "seconds" => seconds,
        _ => null,
    };

    public static string? InUnit(int seconds, string? unitCode)
    {
        if (ConvertToUnit(seconds, unitCode) is not { } v) return null;
        return v.ToString("0.##", CultureInfo.CurrentCulture) + " " + unitCode!.ToLowerInvariant();
    }
}
```

`CompletionListViewModel.FormatValue` — replace the seconds branch:

```csharp
if (v.ValueSeconds is { } secs)
    return Recordum.Core.Formatting.SecondsDisplay.InUnit(secs, m.Unit) ?? DurationFormat.Format(secs);
```

`ExportQueryService`: build a unit-code map next to `metricNames` (add `IReferenceDataQueryService refData` to the ctor — DI resolves it; registration exists):

```csharp
var metricList = await metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(false);
var metricNames = metricList.ToDictionary(m => m.Id, m => m.Name);
var units = await refData.ListUnitsAsync(ct).ConfigureAwait(false);
var metricUnits = metricList.ToDictionary(
    m => m.Id,
    m => m.UnitOfMeasureId is { } uid ? units.FirstOrDefault(u => u.Id == uid)?.Code : null);
```

and in the projection replace the raw-seconds fallback:

```csharp
?? (primaryValue.ValueSeconds.HasValue
    ? (Recordum.Core.Formatting.SecondsDisplay.ConvertToUnit(
           primaryValue.ValueSeconds.Value,
           metricUnits.GetValueOrDefault(primaryValue.MetricDefinitionId))
       ?? primaryValue.ValueSeconds.Value)
    : null);
```

Export test case: seed a completion with `ValueSeconds = 27000` on a metric whose unit is `hours` → the exported row's value is `7.5m`.

- [ ] **Step 4: Run Core/Data + WPF suites** — green.
- [ ] **Step 5: Commit** — `feat: records and export show durations in the metric's unit (U8)`

---

## Task 7: Template payload `repeatable`/`series_labels` + Sleep seed + migration v2.15

**Files:**
- Modify: `src/Recordum.Data/Services/HabitTemplateService.cs` (`ConfigPayload` + `InstantiateAsync` mapping)
- Modify: `src/Recordum.Data/SeedData/HabitTemplateSeed.cs` (`SleepTrackerPayload`)
- Modify: `src/Recordum.Data/SeedData/AppSettingSeed.cs` (`schema_version` → `"2.15"`)
- Create (generated): `src/Recordum.Data/Migrations/*_SleepRepeatableV215.cs`
- Modify: `CLAUDE.md` (§1 DB schema line → v2.15)
- Test: `tests/Recordum.Data.Tests/**/HabitTemplateServiceTests.cs` (extend)

**Interfaces:**
- Produces: payload keys `config.repeatable` (bool) and `config.series_labels` (string array) — same snake_case contract the builder's `BuildTemplatePayloadJson` writes (Task 5 already emits `repeatable`).

- [ ] **Step 1: Failing test.**

```csharp
[Fact]
public async Task Instantiate_maps_repeatable_and_series_labels_from_the_payload()
{
    // Arrange: insert a template whose payload carries the new keys (follow the file's existing
    // insert-template test helper).
    var payload = "{\"node\":{\"name\":\"S\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7b68ee\"}," +
                  "\"config\":{\"schedule_type\":\"Daily\",\"repeatable\":true,\"series_labels\":[\"Night\",\"Nap\"]},\"metrics\":[]}";
    var templateId = await InsertTemplateAsync(payload);

    var nodeId = await sut.InstantiateAsync(templateId, parentId: null);

    var cfg = await configs.GetAsync(nodeId);
    cfg!.Repeatable.Should().BeTrue();
    cfg.SeriesLabelsJson.Should().Be("[\"Night\",\"Nap\"]");
}
```

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement mapping.** `ConfigPayload` gains:

```csharp
public bool? Repeatable { get; set; }
public List<string>? SeriesLabels { get; set; }
```

`InstantiateAsync` config lambda gains:

```csharp
c.Repeatable = payload.Config.Repeatable ?? false;
c.SeriesLabelsJson = payload.Config.SeriesLabels is { Count: > 0 }
    ? JsonSerializer.Serialize(payload.Config.SeriesLabels)
    : null;
```

- [ ] **Step 4: Seed.** In `SleepTrackerPayload`, extend the `config` object (English-first labels — official seeds are EN):

```
"config":{"schedule_type":"Daily","streak_mode":"Daily","streak_recovery":"GraceDay","repeatable":true,"series_labels":["Night","Nap"]}
```

Update `AppSettingSeed`'s `schema_version` constant to `"2.15"`.

- [ ] **Step 5: Migration.** Run:

```bash
dotnet ef migrations add SleepRepeatableV215 --project src/Recordum.Data --startup-project src/Recordum.WPF
```

Inspect the generated file: it must contain **only** two `UpdateData` calls (habit_template payload for id `00000003-0000-0000-0000-000000000005` + app_setting `schema_version` → `2.15`), no schema DDL. Then `dotnet ef database update --project src/Recordum.Data --startup-project src/Recordum.WPF`.

- [ ] **Step 6: Docs.** In `CLAUDE.md` §1, update the schema line: `**DB schema**: v2.15 (luglio 2026) — delta \`sleep_repeatable\` v2.15 (data-only: seed Sleep ripetibile + serie nominate) > \`per_type_prefs\` v2.14 > \`attachment\` v2.13 > …` (splice ahead of the existing chain).

- [ ] **Step 7: Run full Data suite** (seed checksum tests, migration idempotence) + WPF suite — green.
- [ ] **Step 8: Commit** — `feat(data): template payload repeatable/series_labels + Sleep seed, schema v2.15`

---

## Task 8: Data lock — non-repeatable same-day registrations sum in the day total

The "somma" behavior is emergent (each save = one completion; `GetDailySumAsync` sums the metric per day). Lock it with a test so a future refactor can't regress the spec sentence.

**Files:**
- Test: `tests/Recordum.Data.Tests/Integration/CompletionServiceTests.cs` (or the file that already tests `GetDailySumAsync` — extend it)

- [ ] **Step 1: Write the (passing) lock test.**

```csharp
[Fact]
public async Task Two_same_day_registrations_sum_in_the_daily_metric_total()
{
    // U8 spec §3: non-repeatable counters record once per save; a second same-day save SUMS in the
    // day total (the UI shows one module, the data layer just aggregates).
    var habit = await SeedCounterWithMetricAsync();          // follow the file's existing seed helpers
    await RecordValueAsync(habit.NodeId, habit.MetricId, 1.5m, at: Day.AddHours(8));
    await RecordValueAsync(habit.NodeId, habit.MetricId, 0.5m, at: Day.AddHours(20));

    var sums = await sut.GetDailySumAsync(habit.NodeId, habit.MetricId, DayDate, DayDate);

    sums.Should().ContainSingle().Which.Value.Should().Be(2.0m);
}
```

Adapt names to the file's existing helpers/fixtures (SQLite integration tests exist for `GetDailySumAsync` — extend, don't duplicate scaffolding).

- [ ] **Step 2: Run** — expected PASS (it's a lock, not a change). If it FAILS, stop: the sum semantics are broken upstream — investigate before proceeding (systematic-debugging).
- [ ] **Step 3: Commit** — `test(data): lock same-day sum semantics for non-repeatable registrations (U8)`

---

## Final verification (after all tasks)

- [ ] Full Data + WPF suites green; `HomeTreeBudgetTests` green (untouched, confirm no regression).
- [ ] `dotnet run --project src/Recordum.WPF` (NOT `--no-build` — the test runs deleted the apphost) and **author visual pass**:
  - **Counter non ripetibile** (es. Lettura/Acqua): modulo = campi + **Salva** (niente chip, niente "Aggiungi serie"); registra due volte nello stesso giorno → il totale del giorno (fascia Oggi + striscia del giorno) **somma**; il totale mostra la **somma della metrica con l'unità** (non più il conteggio occorrenze).
  - **Counter ripetibile** (Sonno da template, o toggle in Configura): chip **"Night"/"Nap"** in testa, un solo modulo visibile; click su un chip cambia serie; ✎ rinomina inline e il nome **sopravvive al riavvio**; "+ Aggiungi" crea "Serie 3"; Salva dice "Registra N serie".
  - **Unità tempo**: campo durata = valore + tendina ore/min/sec (default dall'unità del metric); 7.5 ore si salva e la Cronologia la mostra "7.5 hours" (o unità equivalente); il cronometro ⏱ riversa i secondi (tendina → sec).
  - **Builder**: creando un Counter compare il check "Si può registrare più volte al giorno"; salvare come template e re-istanziare conserva il flag (template = composizione salvata).
  - **Configura**: sezione "Registrazione" visibile solo per i Counter; il toggle cambia live il modulo aperto senza perdere input su rename/colore.
  - **Export**: un export CSV/JSON di un'abitudine con durate mostra il valore nell'unità del metric.
- [ ] Tracker `docs/phase-4-issues.md`: U8 → `verified-fixed` dopo il pass visivo (chiude anche il vecchio "cosa indica quell'uno?").

## Out of scope (spec §9 + ordine §7)

Journal note-first + foto (U7) = **Onda 2.4**. Builder anteprima a 3 piani + unità/scale custom inline = **Onda 2.5**. Drag-ordering delle serie; conversioni unità oltre h/min/sec; day-summary redesign. `CounterTapQuickAddViewModel` invariato.
