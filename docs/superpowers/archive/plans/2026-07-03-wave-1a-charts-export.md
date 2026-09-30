# Onda 1a — Chart funzionanti + export — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chiudere F0-1…F0-5, F0-7, F0-9 (tracker `docs/phase-4-issues.md` §"Onda 0"): chart builder con anteprima viva, pipeline streak/andamento/orario del Dettaglio riparata, export period-aware per resistance + nome metrica, stamp schema backup reale.

**Architecture:** Spec autoritativa `docs/superpowers/archive/specs/2026-07-03-wave-1a-charts-export-design.md`. Il backend chart (query→`ChartRenderModel`→renderer LiveCharts) è sano: si completa solo la UI del builder e si estrae un overload in-memory per l'anteprima. I fix Data (aggregator, export, backup) sono chirurgici sui root cause già localizzati.

**Tech Stack:** .NET 10, WPF + CommunityToolkit.Mvvm, EF Core + SQLCipher, LiveCharts2 (plugin ChartEngine), QuestPDF (plugin ExportEngine), xUnit (+ `[WpfFact]` per i controlli WPF).

## Global Constraints (Environment facts — leggere PRIMA di iniziare)

- **VM Windows.** Build: `dotnet build <proj> -p:UseAppHost=false` ("Errori: 0" = successo). Test: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false` e `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false`. Baseline: Data 370 / WPF 683 verdi.
- **Branch**: creare `wave-1a-charts-export` da `main` (dopo merge della PR docs `plan/v1-redefinition`; se non ancora mergiata, branch da `plan/v1-redefinition`). MAI committare su `main` (hook la blocca). Commit message finali con: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` (adattare al modello reale).
- **Stringhe**: nuova stringa = 3 edit — `src/Recordum.WPF/Resources/Strings.resx` (EN) + `Strings.it.resx` (IT) + una riga `public static string X => Get("K");` in `Strings.Designer.cs`. **Un hook PreToolUse NEGA Edit/Write su `*.Designer.cs`** → editarlo SOLO via shell (python/sed heredoc da Bash). Attenzione a non introdurre BOM UTF-8.
- **IClock ovunque** (`UtcNow`/`UtcNowMs`/`LocalTimeZone`), mai `DateTime.Now/UtcNow`.
- `DispatcherTimer` solo se `Application.Current is not null` (guard per test headless).
- Se l'app è aperta (lock sui bin WPF), chiuderla prima di build/test.
- Fixture Data: `DbFixture` (`tests/Recordum.Data.Tests/Fixtures`) con `Factory`, `Clock` (`FakeClock(DateTimeOffset)`), `PluginContext`. Fake WPF esistenti: `FakeAbstinenceService`, `FakeChartService`, `FakeMetricDefinitionService`, `FakeHabitTreeQueryService` (`tests/Recordum.WPF.Tests/Fixtures`).
- Test controlli WPF: attributo `[WpfFact]` (vedi `tests/Recordum.WPF.Tests/Controls/TrendChartControlSmokeTests.cs`).

## Interface facts (stato attuale, verificato 2026-07-03)

- `ChartSeriesRowViewModel` (`src/Recordum.WPF/ViewModels/Charts/`): oggi solo `HabitNodeId`, `MetricDefinitionId`, `Label`, `Color` observable.
- `ChartBuilderViewModel(IChartService charts)`: `Name`, `SelectedKind` (ChartKind), `Range` ("90d"), `Bucket` ("daily"), `Series` (ObservableCollection), `LoadNew()`, `LoadAsync(chartId)`, `SaveAsync` (RelayCommand), evento `Saved`.
- `IChartQueryService.BuildModelAsync(Guid chartId, bool isDark, ct)` → `ChartRenderModel(Kind, Title, Series, XAxis, YAxis, Theme)`; impl `ChartQueryService(IDbContextFactory, IClock, ITrendQueryService, ICompletionService)`.
- Renderer: `IChartRenderer.CanRender(ChartKind)` / `CreateView(ChartRenderModel)`; lookup: `registry.GetExtensions(WellKnownExtensionPoints.ChartRenderer).OfType<IChartRenderer>()` (vedi `ChartsWorkspaceViewModel.RenderAsync`).
- `HomeTreeRow(Guid HabitNodeId, Guid? ParentId, string Name, string ColorHex, HabitType? HabitType, …)` da `IHabitTreeQueryService.GetHomeTreeAsync(ct)`.
- `IAbstinenceService.ListPeriodsAsync(Guid habitNodeId, DateOnly from, DateOnly to, ct)` → `IReadOnlyList<AbstinencePeriod>` (`StartedAtMs long`, `EndedAtMs long?`).
- Export: `ExportHabitModel(Guid, string Name, string HabitType, string? ColorHex, ExportSummary, IReadOnlyList<ExportCompletionRow> Completions, IReadOnlyList<ExportNoteRow> Notes, IReadOnlyList<ExportHeatmapCell> Heatmap)`; `ExportCompletionRow(DateTimeOffset CompletedAt, decimal? Value, string? Metric, bool Retroactive)`; `ExportSummary(int CurrentStreak, int LongestStreak, int TotalCompletions, decimal? GoalActual, decimal? GoalTarget)`.
- Enum defaults C#: `ScheduleType.Daily`, `StreakMode.Daily`, `StreakRecovery.None` sono i valori zero → `new HabitConfig()` è già la config di default desiderata.
- `TrendPoint(DateOnly Bucket, decimal Value, int Count)`.

---

### Task 1: F0-9 — stamp schema backup letto dal DB

**Files:**
- Create: `src/Recordum.Data/Backup/DbSchemaVersion.cs`
- Modify: `src/Recordum.Data/Backup/BackupService.cs:25,137-151` · `src/Recordum.Data/Backup/RestoreService.cs:157-165`
- Test: `tests/Recordum.Data.Tests/Backup/DbSchemaVersionTests.cs` (nuovo)

**Interfaces:** Produces: `internal static Task<string> DbSchemaVersion.ReadAsync(RecordumDbContext db, string fallback, CancellationToken ct)`.

- [ ] **Step 1: test rosso**

```csharp
using Recordum.Data.Backup;
using Recordum.Data.Tests.Fixtures;
using Recordum.Core.Models;
using Xunit;

namespace Recordum.Data.Tests.Backup;

public sealed class DbSchemaVersionTests
{
    [Fact]
    public async Task ReadAsync_returns_appsetting_value()
    {
        var fx = new DbFixture();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var row = await db.AppSettings.FindAsync("schema_version");
            if (row is null) db.AppSettings.Add(new AppSetting { Key = "schema_version", Value = "2.12" });
            else row.Value = "2.12";
            await db.SaveChangesAsync();
        }
        await using var db2 = await fx.Factory.CreateDbContextAsync();
        Assert.Equal("2.12", await DbSchemaVersion.ReadAsync(db2, "9.9", default));
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ReadAsync_missing_row_returns_fallback()
    {
        var fx = new DbFixture();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var row = await db.AppSettings.FindAsync("schema_version");
            if (row is not null) { db.AppSettings.Remove(row); await db.SaveChangesAsync(); }
        }
        await using var db2 = await fx.Factory.CreateDbContextAsync();
        Assert.Equal("2.7", await DbSchemaVersion.ReadAsync(db2, "2.7", default));
        await fx.DisposeAsync();
    }
}
```

Nota: se `AppSetting` ha chiave diversa da PK `Key`, adattare il `FindAsync` a `FirstOrDefaultAsync(a => a.Key == "schema_version")` (vedi come lo legge `RestoreService.GetCurrentSchemaVersionAsync`).

- [ ] **Step 2: run** `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter DbSchemaVersionTests` → FAIL (tipo mancante)
- [ ] **Step 3: implementazione**

```csharp
using Microsoft.EntityFrameworkCore;

namespace Recordum.Data.Backup;

/// <summary>Reads the real schema_version AppSetting; the fallback covers pre-seed DBs only.</summary>
internal static class DbSchemaVersion
{
    public static async Task<string> ReadAsync(RecordumDbContext db, string fallback, CancellationToken ct)
    {
        var v = await db.AppSettings.AsNoTracking()
            .Where(a => a.Key == "schema_version").Select(a => a.Value)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }
}
```

In `BackupService.BuildManifestAsync` (il `db` è già aperto lì): sostituire `SchemaVersion: CurrentSchemaVersion` con `SchemaVersion: await DbSchemaVersion.ReadAsync(db, CurrentSchemaVersion, ct).ConfigureAwait(false)`. Rinominare la costante in `FallbackSchemaVersion` (valore "2.7" invariato) e aggiornare il commento: è solo fallback. In `RestoreService.GetCurrentSchemaVersionAsync` sostituire il corpo con `return await DbSchemaVersion.ReadAsync(db, FallbackSchemaVersion, ct)` mantenendo il suo fallback "2.10" (aprire il context come fa oggi).

- [ ] **Step 4: run** stesso filtro → PASS; poi `--filter "FullyQualifiedName~Backup"` → nessuna regressione
- [ ] **Step 5: commit** `fix(data): backup manifest stamps real schema_version from AppSetting (F0-9)`

### Task 2: F0-7 — nome metrica nell'export

**Files:**
- Modify: `src/Recordum.Data/Services/Queries/ExportQueryService.cs:56-93` (metodo `BuildHabitAsync`)
- Test: `tests/Recordum.Data.Tests/Integration/ExportQueryServiceTests.cs` (esistente — riusare il builder del service già presente nel file)

**Interfaces:** Consumes: `IMetricDefinitionService.ListByHabitAsync(habitId, ct)` (già iniettato come `metrics`).

- [ ] **Step 1: test rosso** — aggiungere al file esistente (mimare setup/builder dei test già presenti):

```csharp
[Fact]
public async Task Export_completion_rows_carry_metric_name()
{
    // setup come i test esistenti: habit Counter + MetricDefinition "Pagine" (SortOrder 0)
    // + 1 Completion con CompletionValue { MetricDefinitionId = pagine.Id, ValueInteger = 22 }
    // request SingleHabit, Layout default
    var model = await BuildService(fx).BuildAsync(request);
    var row = Assert.Single(model.Habits[0].Completions);
    Assert.Equal("Pagine", row.Metric);
}
```

- [ ] **Step 2: run** `--filter Export_completion_rows_carry_metric_name` → FAIL (`row.Metric` è null)
- [ ] **Step 3: fix** — in `BuildHabitAsync`, prima del blocco `completionRows`:

```csharp
var metricNames = (await metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(false))
    .ToDictionary(m => m.Id, m => m.Name);
```

e nella costruzione della riga sostituire il terzo argomento `null` con:

```csharp
primaryValue is null ? null : metricNames.GetValueOrDefault(primaryValue.MetricDefinitionId)
```

- [ ] **Step 4: run** filtro + `FullyQualifiedName~ExportQueryService` → PASS
- [ ] **Step 5: commit** `fix(data): export rows carry the metric name (F0-7)`

### Task 3: F0-5 — export period-aware per resistance (contratti + query)

**Files:**
- Modify: `src/Recordum.Plugins.Contracts/Export/ExportModel.cs` · `src/Recordum.Data/Services/Queries/ExportQueryService.cs`
- Test: `tests/Recordum.Data.Tests/Integration/ExportQueryServiceTests.cs`

**Interfaces:** Produces (per Task 4): `ExportPeriodRow(DateTimeOffset StartedAt, DateTimeOffset? EndedAt, TimeSpan Duration, bool IsOpen)`; `ExportHabitModel.Periods: IReadOnlyList<ExportPeriodRow>` (init-property, default `[]`). Consumes: `IAbstinenceService.ListPeriodsAsync` (nuova dipendenza ctor di `ExportQueryService` — aggiornare anche il builder nei test).

- [ ] **Step 1: contratti** — in `ExportModel.cs` aggiungere:

```csharp
/// <summary>One abstinence attempt: start, optional end (open = still running), elapsed span.</summary>
public sealed record ExportPeriodRow(DateTimeOffset StartedAt, DateTimeOffset? EndedAt, TimeSpan Duration, bool IsOpen);
```

e su `ExportHabitModel` la init-property (non tocca i costruttori esistenti):

```csharp
public IReadOnlyList<ExportPeriodRow> Periods { get; init; } = [];
```

- [ ] **Step 2: test rosso**

```csharp
[Fact]
public async Task Export_resistance_habit_lists_periods_not_completions()
{
    // setup: habit Resistance; 2 righe AbstinencePeriod inserite via db:
    //   chiuso: StartedAtMs = T0, EndedAtMs = T0+3gg;  aperto: StartedAtMs = T0+4gg, EndedAtMs = null
    // FakeClock a T0+10gg. Request SingleHabit su [T0-1gg, oggi].
    var habit = model.Habits[0];
    Assert.Empty(habit.Completions);
    Assert.Equal(2, habit.Periods.Count);
    Assert.True(habit.Periods.Any(p => p.IsOpen && p.EndedAt is null));
    Assert.Equal(2, habit.Summary.TotalCompletions); // per resistance = numero tentativi
    Assert.NotEmpty(habit.Heatmap);                  // giorni puliti colorati
}
```

- [ ] **Step 3: run** → FAIL
- [ ] **Step 4: implementazione** — `ExportQueryService`: aggiungere `IAbstinenceService abstinence` al costruttore. In `BuildHabitAsync`, dopo il calcolo di `fromUtc/toUtc`:

```csharp
var isResistance = node.HabitType == HabitType.Resistance;
IReadOnlyList<ExportPeriodRow> periodRows = [];
if (isResistance)
{
    var periods = await abstinence.ListPeriodsAsync(node.Id, request.From, request.To, ct).ConfigureAwait(false);
    var now = clock.UtcNow;
    periodRows = periods
        .OrderByDescending(p => p.StartedAtMs)
        .Select(p =>
        {
            var start = DateTimeOffset.FromUnixTimeMilliseconds(p.StartedAtMs);
            DateTimeOffset? end = p.EndedAtMs is { } e ? DateTimeOffset.FromUnixTimeMilliseconds(e) : null;
            return new ExportPeriodRow(start, end, (end ?? now) - start, end is null);
        })
        .ToList();
}
```

`completionRows`: valutare solo `if (!isResistance && request.Layout.Completions)` (per resistance resta `[]`). Il return diventa:

```csharp
return new ExportHabitModel(node.Id, node.Name, node.HabitType?.ToString() ?? "", node.Color,
    summary, completionRows, noteRows, heatmap) { Periods = periodRows };
```

`BuildSummaryAsync`: aggiungere parametro `int? totalOverride = null` oppure (più semplice) dopo la chiamata, per resistance sostituire il summary: `if (isResistance) summary = summary with { TotalCompletions = periodRows.Count };`.

`BuildHeatmapAsync`: per resistance NON usare completions — nuovo ramo prima dell'attuale switch:

```csharp
if (node.HabitType == HabitType.Resistance)
{
    var periods = await abstinence.ListPeriodsAsync(node.Id, anchor, today, ct).ConfigureAwait(false);
    var clean = new HashSet<DateOnly>();
    foreach (var p in periods)
    {
        var s = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(p.StartedAtMs).UtcDateTime);
        var e = p.EndedAtMs is { } end
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(end).UtcDateTime)
            : today;
        for (var d = s < anchor ? anchor : s; d <= (e > today ? today : e); d = d.AddDays(1)) clean.Add(d);
    }
    var cells = new List<ExportHeatmapCell>();
    for (int i = 0; i < HeatmapCols * HeatmapRows; i++)
    {
        var d = anchor.AddDays(i);
        if (d > today) continue;
        if (clean.Contains(d)) cells.Add(new ExportHeatmapCell(i / HeatmapRows, i % HeatmapRows, 4));
    }
    return cells;
}
```

Nel test builder: costruire `AbstinenceService` reale (ctor in `src/Recordum.Data/Services/AbstinenceService.cs` — usa `IDbContextFactory` come i sibling; mimare la costruzione dagli `AbstinenceServiceTests` esistenti).

- [ ] **Step 5: run** nuovo test + `FullyQualifiedName~ExportQueryService` (regressione) → PASS
- [ ] **Step 6: docs** — se `docs/plugin-api.md` documenta il formato export JSON, annotare che per habit Resistance `total_completions` = numero tentativi (periodi) e che esiste l'array `periods`. Se non lo documenta, saltare.
- [ ] **Step 7: commit** `feat(data): period-aware export for resistance habits (F0-5)`

### Task 4: F0-5 — tabella periodi nei 5 formatter

**Files:**
- Modify: `src/Recordum.Plugins.ExportEngine/Formatters/{Csv,Markdown,Html,Json,Pdf}ExportFormatter.cs`
- Test: `tests/*ExportEngine*` — individuare con `Glob tests/**/*Formatter*Tests.cs` e aggiungere un caso per formatter.

**Interfaces:** Consumes: `ExportHabitModel.Periods` (Task 3). Convenzioni output: date `yyyy-MM-dd HH:mm` invariant; durata `d.hh\:mm\:ss` invariant; esito `open` / `relapsed` (EN, machine-friendly).

- [ ] **Step 1: helper condiviso** — in `ExportFormatterBase.cs` aggiungere:

```csharp
protected static string FormatPeriodDuration(TimeSpan t)
    => t.ToString(@"d\.hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
```

- [ ] **Step 2: test rossi (uno per formatter, stesso modello di input)** — modello con un habit `HabitType="Resistance"`, `Periods` = 1 chiuso + 1 aperto, `Completions` vuoto. Asserzioni: CSV contiene la riga header `habit,started,ended,duration,outcome` e una riga con `,open`; Markdown contiene `| Started | Ended | Duration | Outcome |`; HTML contiene `<th>Started</th>`; JSON contiene `"periods"` con 2 elementi e `"open": true` su uno; PDF: `Write` non lancia e produce bytes non vuoti (smoke, come i test PDF esistenti).
- [ ] **Step 3: run** → FAIL
- [ ] **Step 4: implementazioni**

**CSV** (`CsvExportFormatter.Write`, dopo il loop completions): 

```csharp
var withPeriods = model.Habits.Where(h => h.Periods.Count > 0).ToList();
if (withPeriods.Count > 0)
{
    sb.Append("\r\nhabit,started,ended,duration,outcome\r\n");
    foreach (var habit in withPeriods)
        foreach (var p in habit.Periods)
            sb.Append(Q(habit.Name)).Append(',')
              .Append(p.StartedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(',')
              .Append(p.EndedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(FormatPeriodDuration(p.Duration)).Append(',')
              .Append(p.IsOpen ? "open" : "relapsed").Append("\r\n");
}
```

**Markdown**: dove oggi rende la tabella completions (`| Date | Value | Metric |`), se `habit.Periods.Count > 0` rendere invece:

```
| Started | Ended | Duration | Outcome |
|---|---|---|---|
| 2026-06-13 09:00 | 2026-07-01 19:00 | 18.10:00:00 | relapsed |
```

(stesse convenzioni di formato del CSV; riga aperta: Ended vuoto, Outcome `open`).

**HTML**: analogo — `<table>` con `<th>Started</th><th>Ended</th><th>Duration</th><th>Outcome</th>` al posto della tabella completions quando ci sono periodi (seguire l'HTML già emesso per le liste esistenti nel file).

**JSON**: nell'oggetto habit serializzato, accanto a `"completions"`, aggiungere `"periods"`: array di `{ "started_at": ISO8601, "ended_at": ISO8601|null, "duration_seconds": long, "open": bool }` — seguire il meccanismo di serializzazione già usato nel file per `completions` (stesso writer/opzioni).

**PDF** (`PdfExportFormatter`): dove costruisce la tabella completions, se `habit.Periods.Count > 0` costruire una tabella QuestPDF a 4 colonne con header Started/Ended/Duration/Outcome e le stesse stringhe formattate — clonare il pattern della tabella completions esistente nel file.

- [ ] **Step 5: run** tutti i test ExportEngine → PASS
- [ ] **Step 6: commit** `feat(export): render abstinence periods table in all five formats (F0-5)`

### Task 5: F0-2a — streak engine: fallback config di default

**Files:**
- Modify: `src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs:57-58`
- Test: `tests/Recordum.Data.Tests/**/SchedulingAwareStreakAggregatorTests*.cs` (file esistente degli streak test — individuarlo con Grep `class SchedulingAwareStreakAggregator` nei tests e riusarne il builder)

- [ ] **Step 1: test rosso** — habit Action creato SENZA riga `HabitConfig`, 3 completions live (IsRetroactive=false) in 3 giorni consecutivi fino a "oggi" (FakeClock): `GetHistoryAsync(habitId, oggi-10, oggi)` deve produrre un run con `Length == 3` e `IsCurrent == true`. Oggi produce lista vuota.
- [ ] **Step 2: run** → FAIL
- [ ] **Step 3: fix** — sostituire le righe 57-58:

```csharp
// F0-2a: a habit with no config row behaves as a default daily habit instead of having no streaks.
var cfg = await configs.GetAsync(habitNodeId, ct).ConfigureAwait(false)
          ?? new HabitConfig { HabitNodeId = habitNodeId };
```

(`new HabitConfig()` è già Daily/Daily/None per i default degli enum — vedi Interface facts. Nessuna scrittura su DB.)

- [ ] **Step 4: run** test nuovo + intera suite streak (`--filter FullyQualifiedName~Streak`) → PASS
- [ ] **Step 5: commit** `fix(data): streak engine treats missing config as default daily config (F0-2a)`

### Task 6: F0-2b — il builder crea la config alla creazione dell'habit

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs:262-266`
- Test: `tests/Recordum.WPF.Tests/**/HabitBuilderViewModelTests*.cs` (esistente)

**Root cause verificato**: `BuildConfig(Guid nodeId) => null` (con TODO) → `ComposedHabitService` salta l'insert config (`if (request.Config is not null)`), quindi ogni habit creata dal builder non ha config.

- [ ] **Step 1: test rosso** — nel test esistente del Launch (cercare il test che chiama `LaunchCommand` e asserisce su `CreatedHabitNodeId`/richiesta composta): asserire che la `ComposedHabitRequest` catturata dal fake/spy abbia `Config is not null` con `Config.HabitNodeId == Node.Id`.
- [ ] **Step 2: run** → FAIL
- [ ] **Step 3: fix**:

```csharp
// Default config so streak/scheduling engines work out of the box; the editor refines it later.
// (Timer-mode mapping from RecordingMode is still future work.)
private static HabitConfig BuildConfig(Guid nodeId) =>
    new() { Id = Guid.NewGuid(), HabitNodeId = nodeId };
```

(rimuovere i `#pragma warning disable/restore CA1822` ora inutili; il chiamante non cambia).

- [ ] **Step 4: run** suite HabitBuilder → PASS
- [ ] **Step 5: commit** `fix(wpf): builder-created habits get a default HabitConfig row (F0-2b)`

### Task 7: F0-3 — rebuild reattivo di Trend e Hourly

**Files:**
- Modify: `src/Recordum.WPF/Controls/TrendChartControl.xaml.cs` · `src/Recordum.WPF/Controls/HourlyHistogramControl.xaml.cs`
- Test: `tests/Recordum.WPF.Tests/Controls/TrendChartControlSmokeTests.cs` · `HourlyHistogramControlSmokeTests.cs` (esistenti, aggiungere casi)

- [ ] **Step 1: test rosso** (`[WpfFact]`, nel file smoke esistente):

```csharp
[WpfFact]
public void Late_series_fill_triggers_rebuild()
{
    var vm = /* costruzione TrendChartViewModel come nei test esistenti del file */;
    var control = new TrendChartControl { DataContext = vm };
    control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
    // a VM vuoto: empty visibile
    vm.Series.Add(new TrendPoint(new DateOnly(2026, 7, 1), 5m, 1));
    vm.MaxValue = 5m; // simula il completamento del LoadAsync
    var host = (ContentControl)control.FindName("ChartHost");
    Assert.NotNull(host.Content); // oggi FAIL: nessun rebuild
}
```

- [ ] **Step 2: run** → FAIL
- [ ] **Step 3: fix** — `TrendChartControl`:

```csharp
private TrendChartViewModel? _vm;

public TrendChartControl()
{
    InitializeComponent();
    DataContextChanged += (_, _) => { Rehook(); Rebuild(); };
    Loaded += (_, _) => Rebuild();
    Unloaded += (_, _) => Unhook();
}

private void Rehook()
{
    Unhook();
    _vm = DataContext as TrendChartViewModel;
    if (_vm is null) return;
    _vm.Series.CollectionChanged += OnVmChanged;
    _vm.PropertyChanged += OnVmPropertyChanged;
}

private void Unhook()
{
    if (_vm is null) return;
    _vm.Series.CollectionChanged -= OnVmChanged;
    _vm.PropertyChanged -= OnVmPropertyChanged;
    _vm = null;
}

private void OnVmChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild();
private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
{
    if (e.PropertyName is nameof(TrendChartViewModel.MaxValue) or nameof(TrendChartViewModel.ChartKind)) Rebuild();
}
```

(il `Rebuild()` esistente resta invariato). **Stesso pattern** su `HourlyHistogramControl` (VM: `HourlyHistogramViewModel`; ascoltare la property dei bucket/`MaxCount` — verificare i nomi nel VM e usare quelli).

- [ ] **Step 4: run** entrambi gli smoke test file → PASS
- [ ] **Step 5: commit** `fix(wpf): trend and hourly charts rebuild when async data arrives (F0-3)`

### Task 8: F0-4 — empty-state della distribuzione oraria

**Files:**
- Modify: `src/Recordum.WPF/Controls/HourlyHistogramControl.xaml` + `.xaml.cs` · `Strings.resx` / `Strings.it.resx` / `Strings.Designer.cs` (via shell!)

- [ ] **Step 1: stringhe** — chiave `Detail.Hourly.Empty`: EN `No live records in this period` · IT `Nessuna registrazione dal vivo nel periodo`. Accessor: `public static string Detail_Hourly_Empty => Get("Detail.Hourly.Empty");` (Designer SOLO via Bash/python).
- [ ] **Step 2: test rosso** (`[WpfFact]` nel file smoke): VM con bucket tutti zero → l'elemento `EmptyText` del controllo è `Visible` e ha testo non vuoto; con dati → `Collapsed`.
- [ ] **Step 3: XAML** — aggiungere accanto a `ChartHost` (stesso Grid):

```xml
<TextBlock x:Name="EmptyText" Visibility="Collapsed"
           HorizontalAlignment="Center" VerticalAlignment="Center"
           Foreground="{DynamicResource Brush.Subtle}" />
```

e in `Rebuild()`: ramo vuoto → `EmptyText.Text = Strings.Detail_Hourly_Empty; EmptyText.Visibility = Visibility.Visible;` ramo dati → `EmptyText.Visibility = Visibility.Collapsed;` (mimare `TrendChartControl`).
- [ ] **Step 4: run + build** entrambi i progetti → PASS / Errori: 0
- [ ] **Step 5: commit** `feat(wpf): hourly distribution empty-state (F0-4)`

### Task 9: seeder demo credibile

**Files:**
- Modify: `src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs`

- [ ] **Step 1** — in `Draft(...)` sostituire `IsRetroactive = true` con `IsRetroactive = _rng.NextDouble() < 0.10` (deterministico col seed fisso 42).
- [ ] **Step 2** — in `SeedAsync`, dopo il blocco `// ---- configs ----` esistente, garantire una config a TUTTE le habit seedate:

```csharp
foreach (var id in new[] { acqua, mood, lettura, diario, nienteFumo })
    await configs.UpsertAsync(id, _ => { }, ct).ConfigureAwait(false);
```

(Camminata/Meditazione/ALetto/Allenamento la ricevono già dagli upsert esistenti.)
- [ ] **Step 3: build** WPF → Errori: 0. Verifica manuale (fine piano, Task 14): reset stato + `--seed-demo` → streak/andamento/orario popolati. Nessun test automatico: il seeder è dev-tool con 12 dipendenze, verificato a schermo.
- [ ] **Step 4: commit** `fix(wpf): demo seeder emits live completions and configs for all habits`

### Task 10: overload in-memory di IChartQueryService (base dell'anteprima)

**Files:**
- Modify: `src/Recordum.Data/Services/Queries/IChartQueryService.cs` · `ChartQueryService.cs`
- Test: `tests/Recordum.Data.Tests/Integration/ChartQueryServiceTests.cs`

**Interfaces:** Produces: `ValueTask<ChartRenderModel> BuildModelAsync(Chart chart, IReadOnlyList<ChartSeries> series, bool isDark, CancellationToken ct = default)` — nessun accesso alla tabella `chart` (le serie risolvono comunque i dati di completions dal DB).

- [ ] **Step 1: test rosso** — come `BuildModel_action_habit_no_metric_uses_count_per_bucket` (stesso file), ma SENZA `chartSvc.CreateAsync`: costruire `new Chart { Name = "Preview", ChartType = "bar", ConfigJson = ... }` non persistito + lista `ChartSeries` in memoria e chiamare il nuovo overload; asserire gli stessi punti.
- [ ] **Step 2: run** → FAIL (overload inesistente)
- [ ] **Step 3: refactor** — in `ChartQueryService` estrarre il corpo attuale dopo il caricamento in:

```csharp
public async ValueTask<ChartRenderModel> BuildModelAsync(
    Chart chart, IReadOnlyList<ChartSeries> series, bool isDark, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(chart);
    ArgumentNullException.ThrowIfNull(series);
    var theme = new ChartThemeModel(isDark, isDark ? DarkText : LightText, isDark ? DarkGrid : LightGrid);
    var kind = ChartKindParser.Parse(chart.ChartType);
    var config = ChartConfig.Parse(chart.ConfigJson);
    // … (range, granularity, loop serie, assi — il codice esistente, con `series` al posto di `chart.Series`)
}
```

L'overload `BuildModelAsync(Guid chartId, …)` carica il chart (`Include(c => c.Series)`) e delega: `return await BuildModelAsync(chart, chart.Series.OrderBy(s => s.SortOrder).ToList(), isDark, ct)`. Firma aggiunta anche a `IChartQueryService` con xml-doc "Preview: builds the model from unsaved state".

- [ ] **Step 4: run** nuovo test + tutti i `ChartQueryServiceTests` → PASS
- [ ] **Step 5: commit** `refactor(data): IChartQueryService in-memory overload for live preview`

### Task 11: builder VM — picker, validazione, errori

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Charts/ChartSeriesRowViewModel.cs` · `ChartBuilderViewModel.cs` · `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (se il builder è registrato con ctor esplicito)
- Test: `tests/Recordum.WPF.Tests/ViewModels/Charts/ChartBuilderViewModelTests.cs` (creare se assente; fake esistenti: `FakeChartService`, `FakeHabitTreeQueryService`, `FakeMetricDefinitionService`)

**Interfaces:** Produces (per Task 12/13):
- `ChartSeriesRowViewModel`: `AvailableHabits: IReadOnlyList<HomeTreeRow>` (ctor), `SelectedHabit: HomeTreeRow?` (obs), `Metrics: ObservableCollection<MetricOption>`, `SelectedMetric: MetricOption?` (obs), `Label`, `Color` (obs), evento `Changed` (`EventHandler`), `ToChartSeries(int sortOrder): ChartSeries?` (null se `SelectedHabit` null).
- `MetricOption(Guid? Id, string Name)` record (Id null = "Conteggio").
- `ChartBuilderViewModel`: ctor `(IChartService charts, IHabitTreeQueryService tree, IMetricDefinitionService metrics)`; `ErrorMessage: string?` (obs); `CanSave: bool`; evento `SeriesChanged` interno per il preview.

- [ ] **Step 0: stringhe necessarie a questo task** (EN / IT, Designer via shell — vedi Global Constraints):
  - `Charts.Builder.CountMetric`: `Count` / `Conteggio`
  - `Charts.Builder.SaveError`: `Could not save the chart. Check the series and try again.` / `Impossibile salvare il grafico. Controlla le serie e riprova.`

- [ ] **Step 1: test rossi**

```csharp
[Fact] // riga: selezione habit popola metriche + auto label/colore
public async Task Selecting_habit_fills_metrics_label_and_color() { /* row.SelectedHabit = lettura(colore #8a64e0, metrica Pagine) →
    row.Metrics = [Conteggio, Pagine], row.SelectedMetric = Pagine, row.Label = "Lettura — Pagine", row.Color = "#8a64e0" */ }

[Fact] // label toccata a mano non viene più sovrascritta
public async Task Hand_edited_label_is_preserved() { /* row.Label = "mio"; row.SelectedHabit = altro; Assert row.Label == "mio" */ }

[Fact] // CanSave: nome + ≥1 serie valida
public void CanSave_requires_name_and_valid_series() { }

[Fact] // il salvataggio scarta le righe senza habit e NON lancia su errore del service
public async Task Save_skips_invalid_rows_and_reports_errors() { /* FakeChartService.CreateAsync throws →
    vm.ErrorMessage != null, nessuna eccezione propagata */ }
```

- [ ] **Step 2: run** → FAIL
- [ ] **Step 3: implementazione**

`ChartSeriesRowViewModel` (riscrittura):

```csharp
public sealed record MetricOption(Guid? Id, string Name);

public sealed partial class ChartSeriesRowViewModel : ObservableObject
{
    private readonly Func<Guid, CancellationToken, Task<IReadOnlyList<MetricDefinition>>> _loadMetrics;
    private bool _labelTouched;

    public ChartSeriesRowViewModel(
        IReadOnlyList<HomeTreeRow> availableHabits,
        Func<Guid, CancellationToken, Task<IReadOnlyList<MetricDefinition>>> loadMetrics)
    { AvailableHabits = availableHabits; _loadMetrics = loadMetrics; }

    public IReadOnlyList<HomeTreeRow> AvailableHabits { get; }
    public ObservableCollection<MetricOption> Metrics { get; } = [];
    public event EventHandler? Changed;

    [ObservableProperty] private HomeTreeRow? _selectedHabit;
    [ObservableProperty] private MetricOption? _selectedMetric;
    [ObservableProperty] private string? _label;
    [ObservableProperty] private string? _color;

    partial void OnSelectedHabitChanged(HomeTreeRow? value) => _ = OnHabitChangedAsync(value);
    partial void OnSelectedMetricChanged(MetricOption? value) { AutoLabel(); Changed?.Invoke(this, EventArgs.Empty); }
    partial void OnColorChanged(string? value) => Changed?.Invoke(this, EventArgs.Empty);

    public void MarkLabelTouched() => _labelTouched = true; // chiamato dal setter UI (vedi XAML: TextChanged con IsKeyboardFocused)

    private async Task OnHabitChangedAsync(HomeTreeRow? habit)
    {
        Metrics.Clear();
        Metrics.Add(new MetricOption(null, Resources.Strings.Charts_Builder_CountMetric));
        if (habit is not null)
        {
            foreach (var m in await _loadMetrics(habit.HabitNodeId, default).ConfigureAwait(true))
                Metrics.Add(new MetricOption(m.Id, m.Name));
            SelectedMetric = Metrics.Count > 1 ? Metrics[1] : Metrics[0];
            if (string.IsNullOrWhiteSpace(Color)) Color = habit.ColorHex;
        }
        AutoLabel();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AutoLabel()
    {
        if (_labelTouched || SelectedHabit is null) return;
        Label = SelectedMetric?.Id is null ? SelectedHabit.Name : $"{SelectedHabit.Name} — {SelectedMetric.Name}";
    }

    public ChartSeries? ToChartSeries(int sortOrder) => SelectedHabit is null ? null : new ChartSeries
    {
        Id = Guid.NewGuid(),
        HabitNodeId = SelectedHabit.HabitNodeId,
        MetricDefinitionId = SelectedMetric?.Id,
        Label = Label,
        Color = Color,
        SortOrder = sortOrder,
    };
}
```

`ChartBuilderViewModel`: nuovo ctor `(IChartService charts, IHabitTreeQueryService tree, IMetricDefinitionService metrics)`; `AvailableHabits` caricata in `EnsureHabitsAsync()` (chiamata da `LoadNew`/`LoadAsync`, filtro `r.HabitType is not null`); `AddSeries()` crea la riga passando `AvailableHabits` e `(id, ct) => metrics.ListByHabitAsync(id, ct)` e si aggancia a `row.Changed` → `SaveCommand.NotifyCanExecuteChanged(); RaisePreviewRequested();`. `SaveAsync` diventa:

```csharp
[RelayCommand(CanExecute = nameof(CanSave))]
private async Task SaveAsync(CancellationToken ct)
{
    ErrorMessage = null;
    var rows = Series.Select((r, i) => r.ToChartSeries(i)).Where(s => s is not null).Cast<ChartSeries>().ToList();
    try
    {
        // ramo update/create identico all'attuale, con `rows`
        Saved?.Invoke(this, id);
    }
    catch (Exception)
    {
        ErrorMessage = Resources.Strings.Charts_Builder_SaveError;
    }
}

public bool CanSave => !string.IsNullOrWhiteSpace(Name) && Series.Any(r => r.SelectedHabit is not null);
```

(`OnNameChanged` → `SaveCommand.NotifyCanExecuteChanged()`. `LoadAsync(chartId)` mappa le serie esistenti ricostruendo `SelectedHabit`/`SelectedMetric` dagli id persistiti.) Aggiornare la registrazione DI in `ServiceRegistrar` se necessario e i siti di costruzione nei test esistenti.

- [ ] **Step 4: run** suite Charts WPF → PASS
- [ ] **Step 5: commit** `feat(wpf): chart builder rows with habit/metric pickers, validation, safe save (F0-1)`

### Task 12: anteprima viva

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Charts/ChartBuilderViewModel.cs` · `ChartsWorkspaceViewModel.cs`
- Create: `src/Recordum.WPF/ViewModels/Charts/ChartRendererLocator.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/Charts/ChartBuilderPreviewTests.cs` (nuovo)

**Interfaces:** Produces: `ChartBuilderViewModel.PreviewView: object?` (obs) e `PreviewEmpty: bool` (obs); `static object? ChartRendererLocator.CreateView(IExtensionPointRegistry registry, ChartRenderModel model)`.

- [ ] **Step 1: test rosso** — builder con `FakeChartQueryService` (fake nuovo: implementa entrambi gli overload, ritorna un `ChartRenderModel` fisso) + registry fake con renderer fake che ritorna `new object()`: dopo `AddSeries` + selezione habit, `await vm.RefreshPreviewAsync()` → `PreviewView` non null; senza righe valide → `PreviewView` null e `PreviewEmpty` true.
- [ ] **Step 2: run** → FAIL
- [ ] **Step 3: implementazione**

`ChartRendererLocator` (deduplica il lookup di `ChartsWorkspaceViewModel.RenderAsync`, che va aggiornato a usarlo):

```csharp
internal static class ChartRendererLocator
{
    public static object? CreateView(IExtensionPointRegistry registry, ChartRenderModel model) =>
        registry.GetExtensions(WellKnownExtensionPoints.ChartRenderer)
            .OfType<IChartRenderer>()
            .FirstOrDefault(r => r.CanRender(model.Kind))
            ?.CreateView(model);
}
```

`ChartBuilderViewModel`: dipendenze aggiuntive `IChartQueryService query, IExtensionPointRegistry registry`; proprietà `IsDarkTheme` settata dal workspace. Debounce: `DispatcherTimer` da 300ms creato solo se `Application.Current is not null` (guard!); `RaisePreviewRequested()` (chiamata da `row.Changed`, `OnSelectedKindChanged`, `OnRangeChanged`) fa restart del timer; il tick chiama `RefreshPreviewAsync()`. Nei test (niente Application) chiamare direttamente `RefreshPreviewAsync()`:

```csharp
public async Task RefreshPreviewAsync(CancellationToken ct = default)
{
    var rows = Series.Select((r, i) => r.ToChartSeries(i)).Where(s => s is not null).Cast<ChartSeries>().ToList();
    if (rows.Count == 0) { PreviewView = null; PreviewEmpty = true; return; }
    PreviewEmpty = false;
    var chart = new Chart { Name = Name, ChartType = ChartKindParser.ToToken(SelectedKind),
        ConfigJson = new ChartConfig(Range: Range, Bucket: Bucket).ToJson() };
    try
    {
        var model = await _query.BuildModelAsync(chart, rows, IsDarkTheme, ct).ConfigureAwait(true);
        PreviewView = ChartRendererLocator.CreateView(_registry, model);
    }
    catch (Exception) { PreviewView = null; } // l'anteprima non deve mai far crashare il builder
}
```

`ChartsWorkspaceViewModel`: propaga `IsDarkTheme` al builder quando apre (`New`/`Edit`).

- [ ] **Step 4: run** → PASS
- [ ] **Step 5: commit** `feat(wpf): live chart preview in the builder (F0-1)`

### Task 13: ChartsView.xaml + stringhe

**Files:**
- Modify: `src/Recordum.WPF/Views/ChartsView.xaml` · `Strings.resx` / `Strings.it.resx` / `Strings.Designer.cs` (shell!)

- [ ] **Step 1: stringhe nuove** (EN / IT — `CountMetric` e `SaveError` sono già state aggiunte nel Task 11 Step 0):
  - `Charts.Empty`: `No charts yet — create one` / `Nessun grafico — creane uno`
  - `Charts.Builder.PickHabit`: `Choose a habit…` / `Scegli un'abitudine…`
  - `Charts.Builder.PreviewEmpty`: `Pick a habit to preview` / `Scegli un'abitudine per l'anteprima`
  - `Charts.Builder.Range`: `Period` / `Periodo` · `Charts.Range.30d`: `30 days`/`30 giorni` · `Charts.Range.90d`: `90 days`/`90 giorni` · `Charts.Range.1y`: `1 year`/`1 anno`
- [ ] **Step 2: layout builder** — il `DataTemplate` del builder diventa un Grid a 2 colonne (`*` form, `*` anteprima, min 320 ciascuna):
  - Colonna 0: Nome (TextBox), Tipo (ComboBox esistente), Periodo (ComboBox 3 voci → mappa su `Range` "30d"/"90d"/"1y"), bottone Aggiungi serie, `ItemsControl` righe serie, banner errore (`TextBlock Foreground={DynamicResource Brush.Danger}` bound `ErrorMessage`, visibile se non null), Salva.
  - Riga serie: ComboBox habit (`ItemsSource=AvailableHabits`, item template: `Border 10x10 CornerRadius=5 Background=ColorHex` + `TextBlock Name`; placeholder `Charts.Builder.PickHabit`), ComboBox metriche (`DisplayMemberPath=Name`), TextBox label (su `GotKeyboardFocus`+`TextChanged` dell'utente chiama `MarkLabelTouched` — usare `Interaction`/code-behind come fa il resto del file), swatch colore: riusare `ColorSwatchPicker` (uso di riferimento in `src/Recordum.WPF/Views/HabitEditor/*.xaml` — cercare `ColorSwatchPicker` con Grep e clonare il binding), bottone rimuovi con icona Lucide X (clonare il bottone-icona della TabStrip in `WorkspaceView.xaml` invece del testo "✕").
  - Colonna 1: `ContentControl Content={Binding PreviewView}` + `TextBlock` `Charts.Builder.PreviewEmpty` visibile quando `PreviewEmpty`.
  - Lista master: empty-state `Charts.Empty` quando `Charts.Count == 0` (pattern `EmptyState.xaml`).
- [ ] **Step 3: build** entrambi i progetti → Errori: 0. Smoke E2E esistente `ChartsWorkspaceE2ETests` verde.
- [ ] **Step 4: commit** `feat(wpf): chart builder two-column layout with live preview and validation (F0-1)`

### Task 14: chiusura — suite complete, verifica visiva, tracker

- [ ] **Step 1**: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false` e `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false` → tutte verdi (baseline 370/683 + i nuovi).
- [ ] **Step 2**: reset stato demo (chiedere conferma all'autore prima di cancellare `%LocalAppData%\Recordum`!) + `dotnet run --project src/Recordum.WPF --no-build -p:UseAppHost=false -- --seed-demo`.
- [ ] **Step 3: verifica visiva con l'autore** (skill `run` già attiva): (a) Dettaglio dei 5 tipi: streak > 0, andamento disegnato, orario popolato; (b) Charts: crea grafico 2 serie con anteprima → salva → riapri; salvataggio con service rotto NON crasha; (c) Esporta Lettura → `metric` = "Pagine"; Esporta Niente fumo → tabella periodi nei 5 formati; (d) backup nuovo → anteprima restore mostra "Schema: 2.12".
- [ ] **Step 4**: aggiornare `docs/phase-4-issues.md` (F0-1…5, F0-7, F0-9 → `fixed`/`verified-fixed` a seconda dell'esito) e la tabella onde in `docs/superpowers/specs/2026-07-03-v1-redefinition-plan-design.md` §10 (Onda 1a → completata).
- [ ] **Step 5**: merge secondo `superpowers:finishing-a-development-branch` (PR verso `main`).
