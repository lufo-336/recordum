# Calendar Cell Markers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ridisegnare la cella-giorno del calendario (tab Abitudine) con sfondo tinto type-aware (heat), numero conteggio che risolve il caso "3 serie = 1 completamento", angolino nota colorato per-abitudine, striscia dettaglio del giorno; più due quick-fix (range anni, DatePicker ridondante).

**Architecture:** Due query nuove in `Recordum.Data` (magnitudine giornaliera type-aware in `ICompletionService`, conteggio note in `INoteService`). Il host `HabitScreenViewModel` compone le query e calcola dati presentation-ready (TintStep / Number / NoteCount); il `CalendarNavigator` resta muto e riusabile e li renderizza. Nessuna migrazione (i dati e `note_marker_color` esistono già in schema v2.8).

**Tech Stack:** C#/.NET 10, EF Core + SQLite, WPF + CommunityToolkit.Mvvm, xUnit.

**Spec:** `docs/superpowers/specs/2026-06-02-calendar-cell-markers-design.md`

**Convenzioni di progetto:**
- TDD rosso→verde, commit frequenti. Mai `DateTime.Now`; usare `IClock`/`TimeProvider`.
- Test: `dotnet test tests/Recordum.Data.Tests` e `dotnet test tests/Recordum.WPF.Tests`.
- L'app aperta blocca `Recordum.WPF.exe`: chiuderla prima di build.
- Stringhe utente in `.resx` (EN primario in `Strings.resx`, IT in `Strings.it.resx`).

---

## File map

| File | Azione | Responsabilità |
|---|---|---|
| `src/Recordum.Core/Abstractions/Services/Dtos/DailyMagnitude.cs` | Create | DTO `(Date, Magnitude)` |
| `src/Recordum.Core/Abstractions/Services/Dtos/DayNoteCount.cs` | Create | DTO `(Date, Count)` |
| `src/Recordum.Core/Abstractions/Services/ICompletionService.cs` | Modify | + `GetDailyMagnitudeAsync` |
| `src/Recordum.Core/Abstractions/Services/INoteService.cs` | Modify | + `ListNoteCountsInMonthAsync` |
| `src/Recordum.Data/Services/CompletionService.cs` | Modify | impl magnitudine type-aware |
| `src/Recordum.Data/Services/NoteService.cs` | Modify | impl conteggio note |
| `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs` | Modify | DTO marker, range anni, ApplyMarkers |
| `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarDayCellViewModel.cs` | Modify | TintStep/Number/NoteCount/TintColorHex |
| `src/Recordum.WPF/Converters/HabitColorTintConverter.cs` | Create | `(hex, step)` → brush alpha |
| `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` | Modify | LoadMonthMarkersAsync compone+calcola |
| `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` | Modify | template cella nuovo |
| `src/Recordum.WPF/ViewModels/HabitScreen/Notes/DayNoteViewModel.cs` | Modify | SummaryText giorno |
| `src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml` | Modify | mostra striscia dettaglio |
| `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` | Modify | QuickAdd: DatePicker → label read-only |
| `src/Recordum.WPF/Resources/Strings.resx` (+`.it.resx`) | Modify | nuove stringhe |
| `tests/Recordum.Data.Tests/...` | Create/Modify | test query |
| `tests/Recordum.WPF.Tests/...` | Create/Modify | test VM/converter |

> **Nota App.xaml**: i converter sono registrati come risorse in `App.xaml`. Verificare il pattern esistente (`HexToBrushConverter` ecc.) e aggiungere il nuovo lì.

---

## Task 1: `ListNoteCountsInMonthAsync` (Data)

**Files:**
- Create: `src/Recordum.Core/Abstractions/Services/Dtos/DayNoteCount.cs`
- Modify: `src/Recordum.Core/Abstractions/Services/INoteService.cs`
- Modify: `src/Recordum.Data/Services/NoteService.cs`
- Test: `tests/Recordum.Data.Tests/NoteServiceTests.cs` (esistente; aggiungere)

- [ ] **Step 1: DTO**

`DayNoteCount.cs`:
```csharp
namespace Recordum.Core.Abstractions.Services.Dtos;

public sealed record DayNoteCount(DateOnly Date, int Count);
```

- [ ] **Step 2: Interface**

In `INoteService.cs` aggiungere (e `using Recordum.Core.Abstractions.Services.Dtos;` in testa):
```csharp
ValueTask<IReadOnlyList<DayNoteCount>> ListNoteCountsInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default);
```

- [ ] **Step 3: Failing test**

In `NoteServiceTests.cs`:
```csharp
[Fact]
public async Task ListNoteCountsInMonth_groups_by_target_date()
{
    await using var fx = new DbFixture();
    var svc = fx.CreateNoteService();
    var habitId = await fx.SeedHabitAsync();
    var d = new DateOnly(2026, 6, 2);
    await svc.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "a", TargetDate = d, UpdatedAtMs = 1 });
    await svc.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "b", TargetDate = d, UpdatedAtMs = 2 });
    await svc.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "c", TargetDate = new DateOnly(2026, 6, 5), UpdatedAtMs = 3 });
    await svc.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "d", TargetDate = new DateOnly(2026, 7, 1), UpdatedAtMs = 4 });

    var counts = await svc.ListNoteCountsInMonthAsync(habitId, 2026, 6);

    Assert.Equal(2, counts.Count);
    Assert.Equal(2, counts.Single(c => c.Date == d).Count);
    Assert.Equal(1, counts.Single(c => c.Date == new DateOnly(2026, 6, 5)).Count);
}
```
> Adatta `DbFixture`/`SeedHabitAsync`/`CreateNoteService` ai helper realmente presenti in `NoteServiceTests.cs`.

- [ ] **Step 4: Run — verify FAIL** · `dotnet test tests/Recordum.Data.Tests --filter ListNoteCountsInMonth` → FAIL (metodo assente).

- [ ] **Step 5: Implementation**

In `NoteService.cs` (using `Recordum.Core.Abstractions.Services.Dtos;`):
```csharp
public async ValueTask<IReadOnlyList<DayNoteCount>> ListNoteCountsInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default)
{
    var first = new DateOnly(year, month, 1);
    var next = first.AddMonths(1);
    await using var db = await dbFactory.CreateDbContextAsync(ct);
    return await db.Notes.AsNoTracking()
        .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate != null
                    && n.TargetDate >= first && n.TargetDate < next)
        .GroupBy(n => n.TargetDate!.Value)
        .Select(g => new DayNoteCount(g.Key, g.Count()))
        .OrderBy(d => d.Date)
        .ToListAsync(ct);
}
```

- [ ] **Step 6: Run — verify PASS** · stesso filtro → PASS.

- [ ] **Step 7: Commit** · `git commit -m "feat(data): per-day note counts for calendar markers"`

---

## Task 2: `GetDailyMagnitudeAsync` (Data, type-aware)

**Files:**
- Create: `src/Recordum.Core/Abstractions/Services/Dtos/DailyMagnitude.cs`
- Modify: `src/Recordum.Core/Abstractions/Services/ICompletionService.cs`
- Modify: `src/Recordum.Data/Services/CompletionService.cs`
- Test: `tests/Recordum.Data.Tests/CompletionServiceTests.cs` (esistente; aggiungere)

- [ ] **Step 1: DTO**

`DailyMagnitude.cs`:
```csharp
namespace Recordum.Core.Abstractions.Services.Dtos;

public sealed record DailyMagnitude(DateOnly Date, decimal Magnitude);
```

- [ ] **Step 2: Interface**

In `ICompletionService.cs`:
```csharp
ValueTask<IReadOnlyList<DailyMagnitude>> GetDailyMagnitudeAsync(
    Guid habitNodeId, int year, int month, CancellationToken ct = default);
```

- [ ] **Step 3: Failing tests** (uno per tipo)

In `CompletionServiceTests.cs` (adatta i helper di seed esistenti — `DbFixture`, seed di nodo con `HabitType`, `RecordAsync` con `CompletionValue`):
```csharp
[Fact]
public async Task Magnitude_resistance_counts_distinct_series_within_one_completion()
{
    await using var fx = new DbFixture();
    var svc = fx.CreateCompletionService();
    var (habitId, metricId) = await fx.SeedHabitAsync(HabitType.Resistance);
    // UN completamento con 3 serie (SeriesIndex 0,1,2)
    await svc.RecordAsync(
        new Completion { HabitNodeId = habitId, CompletedAt = new DateTime(2026,6,2,10,0,0,DateTimeKind.Utc), CompletedAtMs = Ms(2026,6,2) },
        values: new[] {
            new CompletionValue { MetricDefinitionId = metricId, SeriesIndex = 0, ValueInteger = 12 },
            new CompletionValue { MetricDefinitionId = metricId, SeriesIndex = 1, ValueInteger = 10 },
            new CompletionValue { MetricDefinitionId = metricId, SeriesIndex = 2, ValueInteger = 8 },
        });

    var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

    Assert.Equal(3m, mags.Single(m => m.Date == new DateOnly(2026,6,2)).Magnitude);
}

[Fact]
public async Task Magnitude_counter_sums_primary_metric()
{
    await using var fx = new DbFixture();
    var svc = fx.CreateCompletionService();
    var (habitId, metricId) = await fx.SeedHabitAsync(HabitType.Counter);
    await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = new DateTime(2026,6,3,9,0,0,DateTimeKind.Utc), CompletedAtMs = Ms(2026,6,3) },
        values: new[] { new CompletionValue { MetricDefinitionId = metricId, SeriesIndex = 0, ValueInteger = 60 } });
    await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = new DateTime(2026,6,3,20,0,0,DateTimeKind.Utc), CompletedAtMs = Ms(2026,6,3)+1 },
        values: new[] { new CompletionValue { MetricDefinitionId = metricId, SeriesIndex = 0, ValueInteger = 40 } });

    var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

    Assert.Equal(100m, mags.Single(m => m.Date == new DateOnly(2026,6,3)).Magnitude);
}

[Fact]
public async Task Magnitude_action_counts_completions()
{
    await using var fx = new DbFixture();
    var svc = fx.CreateCompletionService();
    var (habitId, _) = await fx.SeedHabitAsync(HabitType.Action);
    await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = new DateTime(2026,6,4,8,0,0,DateTimeKind.Utc), CompletedAtMs = Ms(2026,6,4) });
    await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = new DateTime(2026,6,4,18,0,0,DateTimeKind.Utc), CompletedAtMs = Ms(2026,6,4)+1 });

    var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

    Assert.Equal(2m, mags.Single(m => m.Date == new DateOnly(2026,6,4)).Magnitude);
}

[Fact]
public async Task Magnitude_state_takes_latest_value()
{
    await using var fx = new DbFixture();
    var svc = fx.CreateCompletionService();
    var (habitId, metricId) = await fx.SeedHabitAsync(HabitType.State);
    await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = new DateTime(2026,6,5,8,0,0,DateTimeKind.Utc), CompletedAtMs = Ms(2026,6,5) },
        values: new[] { new CompletionValue { MetricDefinitionId = metricId, SeriesIndex = 0, ValueInteger = 2 } });
    await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = new DateTime(2026,6,5,21,0,0,DateTimeKind.Utc), CompletedAtMs = Ms(2026,6,5)+1 },
        values: new[] { new CompletionValue { MetricDefinitionId = metricId, SeriesIndex = 0, ValueInteger = 4 } });

    var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

    Assert.Equal(4m, mags.Single(m => m.Date == new DateOnly(2026,6,5)).Magnitude); // l'ultima
}

[Fact]
public async Task Magnitude_excludes_other_months_and_empty_days()
{
    await using var fx = new DbFixture();
    var svc = fx.CreateCompletionService();
    var (habitId, _) = await fx.SeedHabitAsync(HabitType.Action);
    await svc.RecordAsync(new Completion { HabitNodeId = habitId, CompletedAt = new DateTime(2026,7,1,8,0,0,DateTimeKind.Utc), CompletedAtMs = Ms(2026,7,1) });

    var mags = await svc.GetDailyMagnitudeAsync(habitId, 2026, 6);

    Assert.Empty(mags);
}
```
> `Ms(y,m,d)` = helper locale `=> new DateTimeOffset(new DateTime(y,m,d,0,0,0,DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds();` — definiscilo nel test se non esiste. `SeedHabitAsync(HabitType)` deve creare nodo + (per counter/resistance/state) una `MetricDefinition`; estendi il helper esistente per accettare il tipo e restituire l'eventuale metricId.

- [ ] **Step 4: Run — verify FAIL** · `dotnet test tests/Recordum.Data.Tests --filter Magnitude` → FAIL.

- [ ] **Step 5: Implementation**

In `CompletionService.cs` (using `Recordum.Core.Enums;`, `Recordum.Core.Abstractions.Services.Dtos;`):
```csharp
public async ValueTask<IReadOnlyList<DailyMagnitude>> GetDailyMagnitudeAsync(
    Guid habitNodeId, int year, int month, CancellationToken ct = default)
{
    var first = new DateOnly(year, month, 1);
    var next = first.AddMonths(1);
    var fromMs = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
    var toMs = new DateTimeOffset(next.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();

    await using var db = await dbFactory.CreateDbContextAsync(ct);
    var node = await db.HabitNodes.AsNoTracking()
        .Where(n => n.Id == habitNodeId)
        .Select(n => new { n.HabitType })
        .FirstOrDefaultAsync(ct);
    if (node is null) return [];

    Guid? primaryMetricId = await db.MetricDefinitions.AsNoTracking()
        .Where(m => m.HabitNodeId == habitNodeId)
        .OrderBy(m => m.SortOrder)
        .Select(m => (Guid?)m.Id)
        .FirstOrDefaultAsync(ct);

    var comps = await db.Completions.AsNoTracking()
        .Where(c => c.HabitNodeId == habitNodeId && c.CompletedAtMs >= fromMs && c.CompletedAtMs < toMs)
        .Select(c => new
        {
            c.CompletedAt,
            c.CompletedAtMs,
            Values = c.Values.Select(v => new
            {
                v.SeriesIndex,
                v.MetricDefinitionId,
                Numeric = (decimal?)v.ValueDecimal ?? (decimal?)v.ValueInteger ?? (decimal?)v.ValueSeconds ?? 0m,
            }).ToList(),
        })
        .ToListAsync(ct);

    var result = new List<DailyMagnitude>();
    foreach (var g in comps.GroupBy(c => DateOnly.FromDateTime(c.CompletedAt)))
    {
        decimal mag = node.HabitType switch
        {
            HabitType.Resistance => g.Sum(c => c.Values.Select(v => v.SeriesIndex).Distinct().Count()),
            HabitType.Counter => g.SelectMany(c => c.Values)
                .Where(v => primaryMetricId == null || v.MetricDefinitionId == primaryMetricId.Value)
                .Sum(v => v.Numeric),
            HabitType.State => g.OrderByDescending(c => c.CompletedAtMs)
                .SelectMany(c => c.Values)
                .Where(v => primaryMetricId == null || v.MetricDefinitionId == primaryMetricId.Value)
                .Select(v => v.Numeric)
                .DefaultIfEmpty(0m)
                .First(),
            // Action, Journal, null → conteggio completamenti
            _ => g.Count(),
        };
        if (mag > 0) result.Add(new DailyMagnitude(g.Key, mag));
    }
    return result.OrderBy(d => d.Date).ToList();
}
```
> Nota: il `GroupBy`/`SelectMany` è in-memory (dopo `ToListAsync`), come già fa `GetHeatmapAsync`. Il `DefaultIfEmpty().First()` per State usa l'ultima completion in ordine discendente di `CompletedAtMs`.

- [ ] **Step 6: Run — verify PASS** · `--filter Magnitude` → tutti PASS.

- [ ] **Step 7: Commit** · `git commit -m "feat(data): type-aware daily magnitude for calendar markers"`

---

## Task 3: `HabitColorTintConverter` (WPF)

**Files:**
- Create: `src/Recordum.WPF/Converters/HabitColorTintConverter.cs`
- Modify: `src/Recordum.WPF/App.xaml` (registrare la risorsa)
- Test: `tests/Recordum.WPF.Tests/Converters/HabitColorTintConverterTests.cs` (Create)

- [ ] **Step 1: Failing test**
```csharp
using System.Globalization;
using System.Windows.Media;
using Recordum.WPF.Converters;
using Xunit;

public class HabitColorTintConverterTests
{
    [Fact]
    public void Step0_is_transparent()
    {
        var c = new HabitColorTintConverter();
        var b = (SolidColorBrush)c.Convert(new object[] { "#5fb37a", 0 }, typeof(Brush), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0, b.Color.A);
    }

    [Fact]
    public void Higher_step_has_higher_alpha()
    {
        var c = new HabitColorTintConverter();
        var b1 = (SolidColorBrush)c.Convert(new object[] { "#5fb37a", 1 }, typeof(Brush), null!, CultureInfo.InvariantCulture);
        var b4 = (SolidColorBrush)c.Convert(new object[] { "#5fb37a", 4 }, typeof(Brush), null!, CultureInfo.InvariantCulture);
        Assert.True(b4.Color.A > b1.Color.A);
        Assert.Equal(0x5f, b4.Color.R); // colore abitudine preservato
    }
}
```
> I test WPF girano STA: seguire l'attributo/collection già usato negli altri test converter di `tests/Recordum.WPF.Tests` (es. `[StaFact]` se presente). Adatta.

- [ ] **Step 2: Run — verify FAIL** · `dotnet test tests/Recordum.WPF.Tests --filter HabitColorTint` → FAIL.

- [ ] **Step 3: Implementation**

`HabitColorTintConverter.cs`:
```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

/// <summary>Multi-binding [hexColor, intStep 0..4] → SolidColorBrush del colore abitudine
/// ad alpha crescente. Step 0 = trasparente (giorno non fatto).</summary>
public sealed class HabitColorTintConverter : IMultiValueConverter
{
    private static readonly byte[] Alpha = { 0x00, 0x38, 0x66, 0x9e, 0xd8 }; // step 0..4

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string hex || values[1] is not int step)
            return Brushes.Transparent;
        step = Math.Clamp(step, 0, 4);
        if (step == 0) return Brushes.Transparent;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(Color.FromArgb(Alpha[step], c.R, c.G, c.B));
            brush.Freeze();
            return brush;
        }
        catch (FormatException) { return Brushes.Transparent; }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 4: Run — verify PASS**.

- [ ] **Step 5: Register in App.xaml** — accanto agli altri converter:
```xml
<conv:HabitColorTintConverter x:Key="HabitColorTint"/>
```
> Verifica il prefisso namespace dei converter già in `App.xaml` (`conv:` o simile) e riusalo.

- [ ] **Step 6: Build** · `dotnet build src/Recordum.WPF` → 0 errori.

- [ ] **Step 7: Commit** · `git commit -m "feat(wpf): HabitColorTintConverter for calendar heat tint"`

---

## Task 4: Marker DTOs + `CalendarDayCellViewModel` + `CalendarNavigatorViewModel`

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarDayCellViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/.../CalendarNavigatorViewModelTests.cs` (esistente; aggiornare)

- [ ] **Step 1: Failing test** — i marker presentation-ready arrivano alle celle:
```csharp
[StaFact]
public async Task ApplyMarkers_sets_tint_number_and_notecount()
{
    var clock = new FakeClock(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc));
    var vm = new CalendarNavigatorViewModel(clock)
    {
        MarkerProvider = (y, m, ct) => Task.FromResult(new CalendarMonthMarkers(new[]
        {
            new CalendarDayMarker(new DateOnly(2026,6,2), TintStep: 3, Number: "3", NoteCount: 0),
            new CalendarDayMarker(new DateOnly(2026,6,4), TintStep: 0, Number: null, NoteCount: 2),
        })),
    };
    await vm.LoadAsync(2026, 6, null);

    var c2 = vm.Cells.Single(c => c.Date == new DateOnly(2026,6,2));
    Assert.Equal(3, c2.TintStep);
    Assert.Equal("3", c2.Number);
    Assert.Equal(0, c2.NoteCount);
    var c4 = vm.Cells.Single(c => c.Date == new DateOnly(2026,6,4));
    Assert.Equal(0, c4.TintStep);
    Assert.Equal(2, c4.NoteCount);
}
```
> Adatta `FakeClock` al fake già usato nei test calendario.

- [ ] **Step 2: Run — verify FAIL** (i tipi `CalendarDayMarker`/`CalendarMonthMarkers` nuovi non compilano).

- [ ] **Step 3: Update DTOs** in `CalendarNavigatorViewModel.cs` — sostituire il record esistente:
```csharp
/// <summary>Presentation-ready marker per un giorno (il host calcola tint/numero/note).</summary>
public sealed record CalendarDayMarker(DateOnly Date, int TintStep, string? Number, int NoteCount);

public sealed record CalendarMonthMarkers(IReadOnlyList<CalendarDayMarker> Days);
```
Il delegate resta `public delegate Task<CalendarMonthMarkers> CalendarMarkerProvider(int year, int month, CancellationToken ct);`.

- [ ] **Step 4: Update `CalendarDayCellViewModel`** — rimpiazzare `_completed`/`_hasNote` con:
```csharp
[ObservableProperty] private bool _isSelected;
[ObservableProperty] private int _tintStep;
[ObservableProperty] private string? _number;
[ObservableProperty] private int _noteCount;
public string TintColorHex { get; set; } = "#808080";
public bool HasNote => NoteCount > 0;
public bool ShowNoteCount => NoteCount > 1;
public bool HasNumber => !string.IsNullOrEmpty(Number);
```
Aggiungere `OnNoteCountChanged` per notificare `HasNote`/`ShowNoteCount` (partial method), e `OnNumberChanged` per `HasNumber`.

- [ ] **Step 5: Update `ApplyMarkersAsync`** in `CalendarNavigatorViewModel.cs`:
```csharp
private async Task ApplyMarkersAsync(int year, int month, CancellationToken ct)
{
    if (MarkerProvider is null) return;
    var markers = await MarkerProvider(year, month, ct).ConfigureAwait(true);
    var byDate = markers.Days.ToDictionary(d => d.Date);
    foreach (var cell in Cells)
    {
        cell.TintColorHex = CompletedColorHex;
        if (byDate.TryGetValue(cell.Date, out var m))
        {
            cell.TintStep = m.TintStep;
            cell.Number = m.Number;
            cell.NoteCount = m.NoteCount;
        }
        else
        {
            cell.TintStep = 0; cell.Number = null; cell.NoteCount = 0;
        }
    }
}
```
> `CompletedColorHex`/`NoteColorHex` restano proprietà del VM (il colore nota lo usa la XAML via binding al VM, vedi Task 6).

- [ ] **Step 6: Run — verify PASS** · `--filter CalendarNavigator` → PASS. Sistema eventuali test esistenti che usavano `Completed`/`HasNote` booleani.

- [ ] **Step 7: Commit** · `git commit -m "feat(wpf): presentation-ready calendar day markers (tint/number/notecount)"`

---

## Task 5: Quick-fix A — range anni navigatore

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Calendar/CalendarNavigatorViewModel.cs:33`
- Test: stesso file di test del calendario.

- [ ] **Step 1: Failing test**
```csharp
[StaFact]
public void SelectableYears_span_at_least_15_years_back()
{
    var clock = new FakeClock(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
    var vm = new CalendarNavigatorViewModel(clock);
    Assert.Contains(2011, vm.SelectableYears);
    Assert.Contains(2026, vm.SelectableYears);
}
```

- [ ] **Step 2: Run — verify FAIL** (oggi parte da 2021).

- [ ] **Step 3: Implementation** — riga 33:
```csharp
SelectableYears = Enumerable.Range(_today.Year - 15, 17).ToList(); // 15 indietro, anno corrente, 1 avanti
```

- [ ] **Step 4: Run — verify PASS**.

- [ ] **Step 5: Commit** · `git commit -m "fix(wpf): widen calendar year dropdown to 15y back"`

---

## Task 6: Host — `LoadMonthMarkersAsync` compone e calcola

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (sostituire `LoadMonthMarkersAsync`, righe ~208-222)
- Test: `tests/Recordum.WPF.Tests/.../HabitScreenViewModelTests.cs` (esistente; aggiungere)

- [ ] **Step 1: Failing test** — verifica le regole TintStep/Number per tipo. Usa i fake completion/note service del test esistente; configura il nodo come Resistance con magnitudini [giorno A=3, giorno B=1] e nota count.
```csharp
[StaFact]
public async Task Markers_resistance_number_hidden_when_one_series()
{
    // Arrange: fake ICompletionService.GetDailyMagnitudeAsync → {A:3, B:1}; node HabitType.Resistance
    // fake INoteService.ListNoteCountsInMonthAsync → {B:2}
    // ... costruisci HabitScreenViewModel con i fake (vedi factory di test esistente) e LoadAsync(habitId)
    var markers = await InvokeLoadMonthMarkers(vm, 2026, 6); // helper che chiama Calendar.MarkerProvider

    var a = markers.Days.Single(d => d.Date == DayA);
    Assert.Equal("3", a.Number);          // >1 → mostrato
    Assert.True(a.TintStep >= 1);
    var b = markers.Days.Single(d => d.Date == DayB);
    Assert.Null(b.Number);                // ==1 resistance → nascosto
    Assert.Equal(2, b.NoteCount);
}
```
> Se i fake non espongono i nuovi metodi, estendili. `InvokeLoadMonthMarkers` può semplicemente chiamare `vm.Calendar` dopo `LoadAsync` oppure esporre il provider; scegli l'approccio meno invasivo (es. rendi `LoadMonthMarkersAsync` `internal` + `InternalsVisibleTo` se già usato, altrimenti testa via `Calendar.LoadAsync` e leggi `Cells`).

- [ ] **Step 2: Run — verify FAIL**.

- [ ] **Step 3: Implementation** — sostituire `LoadMonthMarkersAsync`:
```csharp
private async Task<CalendarMonthMarkers> LoadMonthMarkersAsync(int year, int month, CancellationToken ct)
{
    var node = Node;
    if (node is null) return new CalendarMonthMarkers([]);

    var mags = await _completions.GetDailyMagnitudeAsync(node.Id, year, month, ct).ConfigureAwait(true);
    var notes = await _noteService.ListNoteCountsInMonthAsync(node.Id, year, month, ct).ConfigureAwait(true);
    var noteByDate = notes.ToDictionary(n => n.Date, n => n.Count);

    var monthMax = mags.Count > 0 ? mags.Max(m => m.Magnitude) : 0m;
    var type = node.HabitType ?? HabitType.Action;

    var days = new List<CalendarDayMarker>();
    var allDates = mags.Select(m => m.Date).Union(noteByDate.Keys).Distinct();
    foreach (var date in allDates)
    {
        var mag = mags.FirstOrDefault(m => m.Date == date)?.Magnitude ?? 0m;
        var noteCount = noteByDate.TryGetValue(date, out var nc) ? nc : 0;
        days.Add(new CalendarDayMarker(date, TintStepFor(type, mag, monthMax), NumberFor(type, mag), noteCount));
    }
    return new CalendarMonthMarkers(days);
}

private static int TintStepFor(HabitType type, decimal mag, decimal monthMax)
{
    if (mag <= 0) return 0;
    if (type is HabitType.Action or HabitType.Journal) return 4; // binario: fatto = pieno
    if (monthMax <= 0) return 1;
    var step = (int)Math.Ceiling((double)(mag / monthMax) * 4);
    return Math.Clamp(step, 1, 4);
}

private static string? NumberFor(HabitType type, decimal mag)
{
    if (mag <= 0) return null;
    return type switch
    {
        HabitType.Counter or HabitType.State => FormatMag(mag),
        HabitType.Resistance => mag > 1 ? FormatMag(mag) : null,
        // Action/Journal: nascosto salvo registrazioni multiple
        _ => mag > 1 ? $"{FormatMag(mag)}×" : null,
    };
}

private static string FormatMag(decimal mag) =>
    mag == Math.Truncate(mag)
        ? ((long)mag).ToString(System.Globalization.CultureInfo.CurrentCulture)
        : mag.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);
```
> `mags.FirstOrDefault(...)?.Magnitude` richiede che `DailyMagnitude` sia una classe o usa pattern: poiché è un `record` (reference type), `FirstOrDefault` ritorna `null` se assente — OK. `Union` su `DateOnly` va bene.

- [ ] **Step 4: Run — verify PASS**.

- [ ] **Step 5: Commit** · `git commit -m "feat(wpf): host computes type-aware tint/number/notecount for calendar"`

---

## Task 7: `CalendarNavigator.xaml` — nuovo template cella

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml`

Solo XAML — verifica visiva, niente test.

- [ ] **Step 1: Sostituire il `<Grid>` interno della cella** (righe ~87-107) con:
```xml
<Grid>
    <!-- Sfondo tinto: colore abitudine (VM) × step cella -->
    <Border CornerRadius="6">
        <Border.Background>
            <MultiBinding Converter="{StaticResource HabitColorTint}">
                <Binding Path="TintColorHex"/>
                <Binding Path="TintStep"/>
            </MultiBinding>
        </Border.Background>
    </Border>
    <!-- Numero giorno, alto-sinistra discreto -->
    <TextBlock Text="{Binding DayNumber}"
               HorizontalAlignment="Left" VerticalAlignment="Top"
               Margin="5,3,0,0" FontSize="{StaticResource FontSize.Small}"/>
    <!-- Numero conteggio/quantità, centro-basso -->
    <TextBlock Text="{Binding Number}"
               HorizontalAlignment="Center" VerticalAlignment="Bottom"
               Margin="0,0,0,4" FontWeight="SemiBold"
               FontSize="{StaticResource FontSize.Small}"
               Visibility="{Binding HasNumber, Converter={StaticResource BoolToVisible}}"/>
    <!-- Angolino nota (colore per-abitudine dal VM padre) -->
    <Path HorizontalAlignment="Right" VerticalAlignment="Top"
          Data="M 0,0 L 13,0 L 13,13 Z"
          Visibility="{Binding HasNote, Converter={StaticResource BoolToVisible}}"
          Fill="{Binding DataContext.NoteColorHex,
                 RelativeSource={RelativeSource AncestorType=ItemsControl},
                 Converter={StaticResource HexToBrush}}"/>
    <TextBlock Text="{Binding NoteCount}"
               HorizontalAlignment="Right" VerticalAlignment="Top"
               Margin="0,0,1,0" FontSize="8" FontWeight="Bold"
               Foreground="#1c1d22"
               Visibility="{Binding ShowNoteCount, Converter={StaticResource BoolToVisible}}"/>
</Grid>
```
> Mantieni il `Button.Style` esistente (today/selected/future). Rimuovi i due vecchi `<Ellipse>` pallini. Il `Border` tint deve stare **sotto** il testo (primo figlio del Grid). Se il selected-bg accent copre il tint va bene (la selezione vince visivamente).

- [ ] **Step 2: Build** · chiudi l'app se aperta, `dotnet build src/Recordum.WPF` → 0 errori.

- [ ] **Step 3: Commit** · `git commit -m "feat(wpf): calendar cell heat tint + center count + note corner"`

---

## Task 8: Striscia dettaglio giorno (`DayNoteViewModel` + `DayNoteView.xaml`)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/DayNoteViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (passare la magnitudine del giorno al DayNote)
- Modify: `Strings.resx` (+ `.it.resx`)
- Test: `tests/Recordum.WPF.Tests/.../DayNoteViewModelTests.cs` (esistente; aggiungere)

> **Approccio**: la striscia mostra `✓ Fatto · <numero+unità>` (o `—`) + `<n> note`. Il `DayNote` non interroga i completamenti da sé (resterebbe accoppiato); il host gli passa un riepilogo già pronto quando carica il giorno.

- [ ] **Step 1: Failing test**
```csharp
[StaFact]
public async Task SummaryText_reflects_done_state()
{
    var vm = new DayNoteViewModel(fakeNotes, fakeClock);
    vm.SetContext(habitId);
    await vm.LoadDayAsync(new DateOnly(2026,6,2));
    vm.SetDaySummary(done: true, magnitudeLabel: "3 serie", noteCount: 1);
    Assert.Contains("3 serie", vm.SummaryText);
    Assert.Contains("1", vm.SummaryText);

    vm.SetDaySummary(done: false, magnitudeLabel: null, noteCount: 0);
    Assert.Equal(Strings.Abitudine_DaySummary_NotDone, vm.SummaryText);
}
```

- [ ] **Step 2: Run — verify FAIL**.

- [ ] **Step 3: Implementation** — in `DayNoteViewModel`:
```csharp
[ObservableProperty] private string _summaryText = string.Empty;

/// <summary>Il host fornisce il riepilogo del giorno (stato + magnitudine + n° note) già formattato lato dominio.</summary>
public void SetDaySummary(bool done, string? magnitudeLabel, int noteCount)
{
    if (!done)
    {
        SummaryText = Strings.Abitudine_DaySummary_NotDone;
        return;
    }
    var parts = new List<string> { Strings.Abitudine_DaySummary_Done };
    if (!string.IsNullOrEmpty(magnitudeLabel)) parts.Add(magnitudeLabel);
    if (noteCount > 0) parts.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Abitudine_DaySummary_Notes, noteCount));
    SummaryText = string.Join(" · ", parts);
}
```
Aggiungere `using Recordum.WPF.Resources;`.

- [ ] **Step 4: resx** — aggiungere in `Strings.resx` (EN) e `Strings.it.resx` (IT):
  - `Abitudine_DaySummary_Done` = `✓ Done` / `✓ Fatto`
  - `Abitudine_DaySummary_NotDone` = `— Not done yet` / `— Non ancora fatto`
  - `Abitudine_DaySummary_Notes` = `{0} notes` / `{0} note`
  - `Abitudine_QuickAdd_RecordingFor` (per Task 10) = `Recording for:` / `Registrando per:`

- [ ] **Step 5: Host wiring** — in `HabitScreenViewModel.OnCalendarDateSelected` e nel pre-load (`LoadAsync` riga ~142), dopo `DayNote.LoadDayAsync(date)`, calcolare e passare il riepilogo:
```csharp
private async Task PushDaySummaryAsync(DateOnly date, CancellationToken ct)
{
    var node = Node; if (node is null) return;
    var mags = await _completions.GetDailyMagnitudeAsync(node.Id, date.Year, date.Month, ct).ConfigureAwait(true);
    var mag = mags.FirstOrDefault(m => m.Date == date)?.Magnitude ?? 0m;
    var label = mag > 0 ? DayMagnitudeLabel(node.HabitType ?? HabitType.Action, mag) : null;
    var notes = await _noteService.ListByHabitAndDateAsync(node.Id, date, ct).ConfigureAwait(true);
    DayNote.SetDaySummary(mag > 0, label, notes.Count);
}
```
`DayMagnitudeLabel` produce es. `"3 serie"`, `"100 pagine"`, `"Umore 4"` — per ora una formattazione semplice riusando `NumberFor`-style + nome metrica primaria se disponibile (se troppo costoso, fallback al solo numero). Chiamare `await PushDaySummaryAsync(date, ct)` in `OnCalendarDateSelected` (dopo `LoadDayAsync`) e nel blocco di pre-selezione di `LoadAsync`.
> Mantieni semplice `DayMagnitudeLabel`: numero + eventuale unità/“serie”/“×”. Non serve perfezione, è una riga di riepilogo.

- [ ] **Step 6: `DayNoteView.xaml`** — aggiungere sopra l'editor:
```xml
<TextBlock Text="{Binding SummaryText}"
           Foreground="{DynamicResource Brush.TextMuted}"
           Margin="0,0,0,6"
           Visibility="{Binding HasDate, Converter={StaticResource BoolToVisible}}"/>
```

- [ ] **Step 7: Run — verify PASS** + `dotnet build src/Recordum.WPF`.

- [ ] **Step 8: Commit** · `git commit -m "feat(wpf): selected-day summary strip in Abitudine tab"`

---

## Task 9: Quick-fix B — DatePicker QuickAdd → label read-only

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (5 template QuickAdd: righe ~143, 167, 205, 250, 276)

Solo XAML — il calendario principale pilota già `CompletionDate`.

- [ ] **Step 1: In ognuno dei 5 template** sostituire `<DatePicker Grid.Column="1" SelectedDate="{Binding CompletionDate}"/>` con un'etichetta read-only:
```xml
<TextBlock Grid.Column="1" VerticalAlignment="Center"
           Foreground="{DynamicResource Brush.Text}"
           Text="{Binding CompletionDate, StringFormat=d}"/>
```
E il `TextBlock` di label a sinistra: cambiare il testo da `HabitScreen_QuickAdd_Date` a `Abitudine_QuickAdd_RecordingFor` (aggiunta in Task 8). Se preferisci tenere "Date", lascia la label invariata — l'importante è rimuovere il DatePicker.
> `StringFormat=d` usa il formato data corto della cultura corrente. Il `CompletionDate` è `DateTime`.

- [ ] **Step 2: Build** · `dotnet build src/Recordum.WPF` → 0 errori.

- [ ] **Step 3: Commit** · `git commit -m "fix(wpf): replace redundant QuickAdd DatePicker with read-only date label"`

---

## Task 10: Picker colore-nota in Configura

**Files:**
- Individuare la section view/VM dell'editor Configura che ospita Notes/Notifications (cerca in `src/Recordum.WPF/ViewModels/Editor/Sections/` e `Views/Editor/Sections/`).
- Modify: la section VM appropriata + la sua View.
- Modify: `Strings.resx` (+`.it.resx`).
- Test: test della section VM (se esistente).

> **Scope**: aggiungere un campo `NoteMarkerColor` (string hex) alla section, two-way bound, persistito via il percorso `ApplyAsync`→`IHabitConfigService.UpsertAsync` già esistente. Default `#e0b341`.

- [ ] **Step 1: Esplora** — `grep -r "NoteMarkerColor" src/Recordum.WPF` e individua la section editor giusta (probabile `NotificationsSectionViewModel` o una sezione "Notes/Aspetto"). Conferma come le altre proprietà della section vengono lette in `LoadAsync` e scritte in `ApplyAsync`.

- [ ] **Step 2: Failing test** — la section espone `NoteMarkerColor`, default ambra, e lo scrive in config:
```csharp
[Fact]
public async Task NoteMarkerColor_round_trips_through_config()
{
    // carica section da una config con NoteMarkerColor = "#aabbcc" → property == "#aabbcc"
    // cambia property → ApplyAsync → UpsertAsync riceve "#..."
}
```
> Modella sul test esistente di quella section. Se non c'è copertura, aggiungi il minimo.

- [ ] **Step 3: Implementation** — `[ObservableProperty] private string _noteMarkerColor = "#e0b341";` nella section VM; popolare in `LoadAsync` (`config.NoteMarkerColor ?? "#e0b341"`); scrivere in `ApplyAsync` (`config.NoteMarkerColor = NoteMarkerColor`).

- [ ] **Step 4: View** — color input. Riusa il pattern color già usato nel Level-1 dell'editor (color picker dell'abitudine) se esiste; altrimenti una `TextBox` hex + anteprima `Border` con `Background={Binding NoteMarkerColor, Converter={StaticResource HexToBrush}}`. Label `Abitudine_Config_NoteColor`.

- [ ] **Step 5: resx** — `Abitudine_Config_NoteColor` = `Note marker color` / `Colore marcatore note`.

- [ ] **Step 6: Reload calendario** — verificare che al rientro nella tab Abitudine il `Calendar.NoteColorHex` rifletta il nuovo valore. È già ricaricato in `HabitScreenViewModel.LoadAsync` (riga 133); se l'editor Configura è una finestra separata, al ritorno `LoadAsync` viene già richiamato — confermare in verifica visiva.

- [ ] **Step 7: Run tests + build**.

- [ ] **Step 8: Commit** · `git commit -m "feat(wpf): per-habit note marker color picker in Configure"`

---

## Verifica finale

- [ ] `dotnet test tests/Recordum.Data.Tests` → verde.
- [ ] `dotnet test tests/Recordum.WPF.Tests` → verde.
- [ ] `dotnet build src/Recordum.WPF` → 0 errori, 0 warning.
- [ ] **Verifica visiva utente** (skill `run`): tab Abitudine — celle tinte per intensità, "3" su un giorno resistance, "100" su counter, angolino nota nel colore scelto + conteggio se >1, striscia dettaglio del giorno selezionato, dropdown anni fino a <2021, QuickAdd senza DatePicker (label "Registrando per:"), picker colore-nota in Configura che cambia il colore dell'angolino.

## Self-review note (per chi esegue)
- I test usano `FakeClock`/`DbFixture`/`SeedHabitAsync`/`[StaFact]`: **adattali agli helper realmente presenti** nei progetti di test (i nomi qui sono indicativi del pattern, non garantiti).
- `DailyMagnitude` e `DayNoteCount` sono `record` (reference): `FirstOrDefault` ritorna `null` se assente — gestito con `?.Magnitude ?? 0m`.
- Nessuna migrazione: se `dotnet ef` viene invocato per errore, fermarsi (lo schema è già v2.8).
