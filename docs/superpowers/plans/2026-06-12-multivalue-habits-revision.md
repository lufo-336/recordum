# Multivalue Habits Revision (R-multiValue) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Counter = occorrenze a tap (+1 nudo, righe-serie con campi), serie come proprietà del Counter (Resistance torna astinenza, reclass dati v2.10), quantità con unità opzionale, cella durata mm:ss con cronometro persistito, seeder multi-metrica.

**Architecture:** Spec autoritativa `docs/superpowers/specs/2026-06-12-multivalue-habits-revision-design.md`. Grammatica pura in Core (`HabitCompositionRules`); quick-add WPF MVVM (CommunityToolkit); nessun cambio schema — solo migration EF di UPDATE; crono su `active_timer` esistente.

**Tech Stack:** .NET 10, WPF + CommunityToolkit.Mvvm, EF Core + SQLCipher, xUnit.

**Convenzioni trasversali:** stringhe utente in `Strings.resx` (EN) + `Strings.it.resx` + proprietà in `Strings.Designer.cs` (mantenuto a mano, copia il pattern delle voci adiacenti). Mai `DateTime.Now` — sempre `IClock`. Test: `dotnet test tests/<proj> --nologo -v q`.

---

### Task 1: Grammatica Core — `HabitComposition` + `HabitCompositionRules`

**Files:**
- Modify: `src/Recordum.Core/Composition/HabitComposition.cs`
- Modify: `src/Recordum.Core/Composition/HabitCompositionRules.cs`
- Test: `tests/Recordum.Core.Tests/Composition/HabitCompositionRulesTests.cs` (riscrivi la classe)

- [ ] **Step 1: Riscrivi i test** (sostituisci il contenuto della classe test; mantieni namespace/using esistenti)

```csharp
private static HabitComposition C(bool counts = false, bool abst = false, params FieldNature[] fields) =>
    new(fields, counts, abst, RecordingMode.Manual);

[Fact] public void Text_wins() => Assert.Equal(HabitType.Journal, HabitCompositionRules.ResolveType(C(fields: [FieldNature.Text, FieldNature.Time])));
[Fact] public void Abstinence_resolves_resistance() => Assert.Equal(HabitType.Resistance, HabitCompositionRules.ResolveType(C(abst: true, fields: [FieldNature.Quantity, FieldNature.Scale])));
[Fact] public void Scale_only_is_state() => Assert.Equal(HabitType.State, HabitCompositionRules.ResolveType(C(fields: [FieldNature.Scale])));
[Fact] public void Counts_occurrences_is_counter() => Assert.Equal(HabitType.Counter, HabitCompositionRules.ResolveType(C(counts: true)));
[Fact] public void Measured_field_is_counter() => Assert.Equal(HabitType.Counter, HabitCompositionRules.ResolveType(C(fields: [FieldNature.Time])));
[Fact] public void Scale_plus_measured_is_counter() => Assert.Equal(HabitType.Counter, HabitCompositionRules.ResolveType(C(fields: [FieldNature.Scale, FieldNature.Quantity])));
[Fact] public void Empty_is_action() => Assert.Equal(HabitType.Action, HabitCompositionRules.ResolveType(C()));

[Fact] public void Count_nature_no_longer_addable() => Assert.False(HabitCompositionRules.CanAddField(C(), FieldNature.Count));
[Fact] public void Fields_blocked_when_counting() => Assert.All(
    new[] { FieldNature.Quantity, FieldNature.Time, FieldNature.Scale, FieldNature.Text },
    n => Assert.False(HabitCompositionRules.CanAddField(C(counts: true), n)));
[Fact] public void Abstinence_allows_measured_and_scale() => Assert.All(
    new[] { FieldNature.Quantity, FieldNature.Time, FieldNature.Scale },
    n => Assert.True(HabitCompositionRules.CanAddField(C(abst: true), n)));
[Fact] public void Abstinence_blocks_text() => Assert.False(HabitCompositionRules.CanAddField(C(abst: true), FieldNature.Text));
[Fact] public void Journal_time_relaxation_survives() => Assert.True(HabitCompositionRules.CanAddField(C(fields: [FieldNature.Text]), FieldNature.Time));

[Fact] public void Counter_block_requires_empty_canvas() { Assert.True(HabitCompositionRules.CanEnableCounter(C())); Assert.False(HabitCompositionRules.CanEnableCounter(C(fields: [FieldNature.Quantity]))); Assert.False(HabitCompositionRules.CanEnableCounter(C(abst: true))); }
[Fact] public void Abstinence_blocked_by_text_and_counter() { Assert.True(HabitCompositionRules.CanEnableAbstinence(C(fields: [FieldNature.Scale]))); Assert.False(HabitCompositionRules.CanEnableAbstinence(C(fields: [FieldNature.Text]))); Assert.False(HabitCompositionRules.CanEnableAbstinence(C(counts: true))); }
```

- [ ] **Step 2: Verifica che falliscano** — `dotnet test tests/Recordum.Core.Tests --nologo -v q` → errori di compilazione (firma `HabitComposition` cambiata): ok, è il fail atteso.

- [ ] **Step 3: Implementa**

`HabitComposition.cs`:
```csharp
/// <summary>Snapshot immutabile dei mattoni correnti, input puro della grammatica.</summary>
public sealed record HabitComposition(
    IReadOnlyList<FieldNature> Fields,
    bool CountsOccurrences,
    bool Abstinence,
    RecordingMode Mode)
{
    public static HabitComposition Empty { get; } =
        new(Array.Empty<FieldNature>(), CountsOccurrences: false, Abstinence: false, RecordingMode.Manual);
}
```

`HabitCompositionRules.cs` (sostituisci `ResolveType`, `CanAddField`; sostituisci `CanEnableSeries` con i due nuovi; `CanSetMode` invariato ma `hasMeasured` ora è `Quantity or Time` — il `Count` field non esiste più, e il counter nudo resta `Manual`):
```csharp
public static HabitType ResolveType(HabitComposition c)
{
    ArgumentNullException.ThrowIfNull(c);
    if (c.Fields.Contains(FieldNature.Text)) return HabitType.Journal;            // #1
    if (c.Abstinence) return HabitType.Resistance;                                 // #2
    bool hasMeasured = c.Fields.Any(f => f is FieldNature.Quantity or FieldNature.Time);
    bool hasScale = c.Fields.Contains(FieldNature.Scale);
    if (hasScale && !hasMeasured && !c.CountsOccurrences) return HabitType.State;  // #3
    if (c.CountsOccurrences || hasMeasured) return HabitType.Counter;              // #4
    return HabitType.Action;                                                       // #5
}

public static bool CanAddField(HabitComposition c, FieldNature nature)
{
    ArgumentNullException.ThrowIfNull(c);
    if (c.CountsOccurrences) return false; // il contatore nudo non ha campi (spec §3.3 nota 2)
    bool hasText = c.Fields.Contains(FieldNature.Text);
    switch (nature)
    {
        case FieldNature.Text:
            return !hasText && !c.Abstinence && c.Fields.All(f => f == FieldNature.Time);
        case FieldNature.Quantity:
        case FieldNature.Scale:
            return !hasText;
        case FieldNature.Time:
            if (hasText) return !c.Fields.Contains(FieldNature.Time);
            return true;
        case FieldNature.Count: // non è più un campo: mattone dedicato CountsOccurrences
        default:
            return false;
    }
}

/// <summary>Il mattone contatore vale solo a tela vuota: con campi o astinenza il conteggio è implicito.</summary>
public static bool CanEnableCounter(HabitComposition c)
{
    ArgumentNullException.ThrowIfNull(c);
    return c.Fields.Count == 0 && !c.Abstinence;
}

public static bool CanEnableAbstinence(HabitComposition c)
{
    ArgumentNullException.ThrowIfNull(c);
    return !c.Fields.Contains(FieldNature.Text) && !c.CountsOccurrences;
}
```
E aggiorna `CanSetMode` (il campo Count non esiste più; il counter nudo resta manuale):
```csharp
public static bool CanSetMode(HabitComposition c, RecordingMode mode)
{
    ArgumentNullException.ThrowIfNull(c);
    if (mode == RecordingMode.Manual) return true;
    return c.Fields.Any(f => f is FieldNature.Quantity or FieldNature.Time);
}
```

- [ ] **Step 4: Verifica verde** — `dotnet test tests/Recordum.Core.Tests --nologo -v q` → PASS. La build di WPF/Data fallirà finché il Task 2 non adatta i chiamanti: compila solo Core+test (`dotnet build src/Recordum.Core; dotnet test tests/Recordum.Core.Tests`). NON committare ancora se la soluzione non compila: il commit arriva a fine Task 2.

---

### Task 2: Builder VM — mattoni contatore/astinenza, via serie, unità opzionale

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/FieldBlockViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/RecordingPreviewViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/` (adatta i test esistenti su SeriesEnabled/SeriesCount, aggiungi i nuovi)

- [ ] **Step 1: Test nuovi** (nella classe di test esistente del builder; rimuovi/adatta quelli su `SeriesEnabled`/`SeriesCount`/`ToggleSeries`/`CanEnableSeries`)

```csharp
[Fact]
public void Counter_block_derives_counter_and_locks_palette()
{
    var vm = CreateVm(); // helper esistente della classe
    vm.CountsOccurrences = true;
    Assert.Equal(HabitType.Counter, vm.DerivedType);
    Assert.False(vm.CanAddField(FieldNature.Quantity));
    Assert.True(vm.Preview.IsTapCounter);
}

[Fact]
public void Abstinence_derives_resistance()
{
    var vm = CreateVm();
    vm.Abstinence = true;
    vm.AddFieldCommand.Execute(FieldNature.Quantity);
    Assert.Equal(HabitType.Resistance, vm.DerivedType);
}

[Fact]
public void Quantity_without_unit_is_integer_and_launchable()
{
    var vm = CreateVm();
    vm.Name = "Lettura";
    vm.AddFieldCommand.Execute(FieldNature.Quantity);
    Assert.True(vm.CanLaunch); // unità opzionale (spec §3.1)
    Assert.Equal(MetricType.Integer, vm.Fields[0].MetricType);
    vm.Fields[0].UnitOfMeasureId = Guid.NewGuid();
    Assert.Equal(MetricType.Decimal, vm.Fields[0].MetricType);
}

[Fact]
public void Counter_with_fields_preview_shows_add_series()
{
    var vm = CreateVm();
    vm.AddFieldCommand.Execute(FieldNature.Quantity);
    Assert.True(vm.Preview.HasAddSeries);
    Assert.Single(vm.Preview.Rows);
}
```

- [ ] **Step 2: Run** → fail di compilazione (proprietà mancanti).

- [ ] **Step 3: Implementa**

`FieldBlockViewModel.cs` — `MetricType` dipende dall'unità e notifica al cambio:
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(MetricType))]
private Guid? _unitOfMeasureId;     // Quantity (opzionale)

public MetricType MetricType => Nature switch
{
    FieldNature.Quantity => UnitOfMeasureId is null ? MetricType.Integer : MetricType.Decimal,
    FieldNature.Time => MetricType.Seconds,
    FieldNature.Scale => MetricType.Integer,
    FieldNature.Text => MetricType.Text,
    _ => MetricType.Integer,
};
```

`HabitBuilderViewModel.cs`:
- Sostituisci `_seriesEnabled`/`_seriesCount` con:
```csharp
[ObservableProperty] private bool _countsOccurrences;
[ObservableProperty] private bool _abstinence;
partial void OnCountsOccurrencesChanged(bool value) => Recompute();
partial void OnAbstinenceChanged(bool value) => Recompute();
public bool CanEnableCounter => HabitCompositionRules.CanEnableCounter(Snapshot());
public bool CanEnableAbstinence => HabitCompositionRules.CanEnableAbstinence(Snapshot());
```
- Elimina `ToggleSeriesCommand`, `CanEnableSeries`, `OnSeriesCountChanged`, `OnSeriesEnabledChanged`.
- `Snapshot()`: `new(Fields.Select(f => f.Nature).ToList(), CountsOccurrences, Abstinence, Mode);`
- `Recompute()`:
```csharp
private void Recompute()
{
    TemplateSaved = false;
    DerivedType = HabitCompositionRules.ResolveType(Snapshot());
    bool hasCells = Fields.Any(f => f.Nature != FieldNature.Text);
    Preview.Rebuild(Fields, CountsOccurrences, DerivedType == HabitType.Counter && hasCells);
    OnPropertyChanged(nameof(CanEnableCounter));
    OnPropertyChanged(nameof(CanEnableAbstinence));
    RaiseLaunchState();
}
```
- `CanLaunch` (unità quantità non più richiesta):
```csharp
public bool CanLaunch =>
    !string.IsNullOrWhiteSpace(Name)
    && Fields.All(f => f.Nature != FieldNature.Scale || f.ScaleTemplateId is not null);
```
- Payload: in `TemplatePayload` sostituisci `public int? SeriesCount` con `public bool? CountsOccurrences { get; set; }`; in `BuildTemplatePayloadJson` sostituisci `SeriesCount = ...` con `CountsOccurrences = CountsOccurrences ? true : null,` (i payload vecchi con `series_count` sono già ignorati dal reader: nessun cambio in `HabitTemplateService`).
- `DefaultName`: elimina il ramo `FieldNature.Count`.
- `OnFieldChanged`: aggiungi `nameof(FieldBlockViewModel.UnitOfMeasureId)` già presente — verifica che chiami anche `Recompute()` quando cambia l'unità (il MetricType del campo cambia ma non serve ricalcolo tipo: basta `RaiseLaunchState`, che è il comportamento attuale — lascialo).

`RecordingPreviewViewModel.cs`:
```csharp
public sealed partial class RecordingPreviewViewModel : ObservableObject
{
    public ObservableCollection<PreviewRow> Rows { get; } = new();
    [ObservableProperty] private bool _hasTextField;
    [ObservableProperty] private bool _isTapCounter;
    [ObservableProperty] private bool _hasAddSeries;

    public void Rebuild(IReadOnlyList<FieldBlockViewModel> fields, bool countsOccurrences, bool hasAddSeries)
    {
        Rows.Clear();
        IsTapCounter = countsOccurrences;
        HasAddSeries = hasAddSeries;
        HasTextField = fields.Any(f => f.Nature == Core.Enums.FieldNature.Text);
        if (countsOccurrences) return; // anteprima = mock bottone +1, niente righe
        var cellNames = fields.Where(f => f.Nature != Core.Enums.FieldNature.Text)
                              .Select(f => f.Name).ToList();
        Rows.Add(new PreviewRow(string.Empty, cellNames));
    }
}
```

- [ ] **Step 4: Adatta i test esistenti rotti** (compilazione: ogni uso di `SeriesEnabled`→`CountsOccurrences` o eliminazione; `Rebuild` nuova firma).
- [ ] **Step 5: Run** — `dotnet test tests/Recordum.Core.Tests tests/Recordum.WPF.Tests --nologo -v q`. La build WPF fallirà sul XAML del builder (binding a SeriesEnabled): se succede, fai il fix XAML minimo del Task 3 Step 1 ora e tieni i due commit separati solo se la build lo consente; altrimenti **unifica Task 2+3 in un commit**.
- [ ] **Step 6: Commit** — `git commit -m "feat(core,wpf): grammar v2 — counter tap block, abstinence block, series removed"`

---

### Task 3: Builder XAML — palette nuova + resx

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml`
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`

- [ ] **Step 1: resx** — aggiungi (EN primario / IT):

| Key | EN | IT |
|---|---|---|
| `HabitBuilder.CounterBlock` | `Counter (+1)` | `Contatore (+1)` |
| `HabitBuilder.CounterBlock.Tip` | `This habit counts occurrences: record with a single tap.` | `Questa abitudine conta occorrenze: si registra con un tap.` |
| `HabitBuilder.AbstinenceBlock` | `Abstinence` | `Astinenza` |
| `HabitBuilder.AbstinenceBlock.Tip` | `Resist something: record lapses/episodes; the streak measures the time since the last one.` | `Resistere a qualcosa: registri le ricadute/episodi; la streak misura il tempo dall'ultima.` |
| `HabitBuilder.Preview.Tap` | `+1` | `+1` |
| `HabitBuilder.Preview.AddSeries` | `+ Add series` | `+ Aggiungi serie` |

Rimuovi/lascia orfane le chiavi `HabitBuilder.Series`, `HabitBuilder.SeriesCount`, `HabitBuilder.SeriesRow` SOLO se non più referenziate dopo lo Step 2 (`HabitBuilder.SeriesRow` non serve più: la preview non etichetta righe). Aggiorna `Strings.Designer.cs` con le proprietà corrispondenti (copia il pattern delle voci `HabitBuilder_*` adiacenti).

- [ ] **Step 2: XAML** — in `HabitBuilderView.xaml`:
  - Rimuovi il CheckBox "Serie" (bind `SeriesEnabled`) e il controllo numerico `SeriesCount`.
  - Rimuovi il bottone palette del mattone "Conteggio" (`AddFieldCommand` con `FieldNature.Count`).
  - Aggiungi nella zona palette due CheckBox tematizzati (stile implicito flat già a tema):
```xml
<CheckBox Content="{x:Static res:Strings.HabitBuilder_CounterBlock}"
          ToolTip="{x:Static res:Strings.HabitBuilder_CounterBlock_Tip}"
          IsChecked="{Binding CountsOccurrences}"
          IsEnabled="{Binding CanEnableCounter}"/>
<CheckBox Content="{x:Static res:Strings.HabitBuilder_AbstinenceBlock}"
          ToolTip="{x:Static res:Strings.HabitBuilder_AbstinenceBlock_Tip}"
          IsChecked="{Binding Abstinence}"
          IsEnabled="{Binding CanEnableAbstinence}"/>
```
  - Nell'area anteprima: quando `Preview.IsTapCounter` mostra un mock del bottone +1 (Border circolare 56px con TextBlock `+1`, accent); quando `Preview.HasAddSeries` mostra sotto le righe un TextBlock ghost `HabitBuilder_Preview_AddSeries`. Usa `BooleanToVisibilityConverter` già in risorse.
- [ ] **Step 3: Build + suite WPF** — `dotnet build src/Recordum.WPF; dotnet test tests/Recordum.WPF.Tests --nologo -v q` → verdi.
- [ ] **Step 4: Commit** — `git commit -m "feat(wpf): builder palette — counter/abstinence blocks, series toggle removed"`

---

### Task 4: `DurationFormat` — parse/format durate

**Files:**
- Create: `src/Recordum.WPF/Formatting/DurationFormat.cs`
- Test: `tests/Recordum.WPF.Tests/Formatting/DurationFormatTests.cs` (create)

- [ ] **Step 1: Test**

```csharp
using Recordum.WPF.Formatting;
using Xunit;

namespace Recordum.WPF.Tests.Formatting;

public class DurationFormatTests
{
    [Theory]
    [InlineData("45", 45)]          // solo cifre: ultime 2 = secondi
    [InlineData("130", 90)]         // "1:30"
    [InlineData("0:45", 45)]
    [InlineData("12:05", 725)]
    [InlineData("1:02:05", 3725)]
    [InlineData(" 1:02:05 ", 3725)]
    public void Parses(string text, int expected)
    { Assert.True(DurationFormat.TryParse(text, out var s)); Assert.Equal(expected, s); }

    [Theory]
    [InlineData("")] [InlineData("  ")] [InlineData("abc")] [InlineData("1:99")] [InlineData("1:2:3:4")] [InlineData("-10")]
    public void Rejects(string text) => Assert.False(DurationFormat.TryParse(text, out _));

    [Theory]
    [InlineData(45, "0:45")]
    [InlineData(725, "12:05")]
    [InlineData(3725, "1:02:05")]
    [InlineData(90061, "1g 1:01:01")]
    [InlineData(31536000 + 3541 * 86400 + 60, "1a 3541g")] // oltre l'anno: solo anni+giorni... vedi nota
    public void Formats(int seconds, string expected) => Assert.Equal(expected, DurationFormat.Format(seconds));

    [Theory]
    [InlineData(0, "0g")] [InlineData(41, "41g")] [InlineData(365, "1a 0g")] [InlineData(771, "2a 41g")]
    public void FormatsDays(int days, string expected) => Assert.Equal(expected, DurationFormat.FormatDays(days));
}
```
**Nota sul caso anni in `Format`**: usa un caso semplice e corretto — sostituisci la InlineData "oltre l'anno" con `[InlineData(31536000 + 86400 * 41 + 60, "1a 41g")]` (oltre l'anno si mostrano solo anni+giorni; anno = 365 giorni).

- [ ] **Step 2: Run** → FAIL (classe inesistente).
- [ ] **Step 3: Implementa**

```csharp
using System.Globalization;

namespace Recordum.WPF.Formatting;

/// <summary>Parse/format di durate per la cella tempo (spec multivalue §4.1, scelta A: mask mm:ss).
/// Solo cifre = mm:ss compattato ("130" → 1:30). Format auto-scala: mm:ss → h:mm:ss → "Ng h:mm:ss" → "Na Ng".</summary>
public static class DurationFormat
{
    public static bool TryParse(string? text, out int seconds)
    {
        seconds = 0;
        var t = text?.Trim();
        if (string.IsNullOrEmpty(t)) return false;
        if (t.All(char.IsAsciiDigit))
        {
            // cifre nude: le ultime due sono i secondi ("130" → 1:30, "45" → 0:45)
            int ss = int.Parse(t.Length <= 2 ? t : t[^2..], CultureInfo.InvariantCulture);
            int mm = t.Length <= 2 ? 0 : int.Parse(t[..^2], CultureInfo.InvariantCulture);
            if (ss > 59) return false;
            seconds = mm * 60 + ss;
            return true;
        }
        var parts = t.Split(':');
        if (parts.Length is < 2 or > 3) return false;
        var nums = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out nums[i])) return false;
        if (parts.Length == 2)
        {
            if (nums[1] > 59) return false;
            seconds = nums[0] * 60 + nums[1];
        }
        else
        {
            if (nums[1] > 59 || nums[2] > 59) return false;
            seconds = nums[0] * 3600 + nums[1] * 60 + nums[2];
        }
        return true;
    }

    public static string Format(int seconds)
    {
        if (seconds < 0) seconds = 0;
        const int Day = 86400, Year = 365 * Day;
        if (seconds >= Year) return FormatDays(seconds / Day);
        if (seconds >= Day)
        {
            int d = seconds / Day, rest = seconds % Day;
            return string.Create(CultureInfo.InvariantCulture,
                $"{d}g {rest / 3600}:{rest % 3600 / 60:00}:{rest % 60:00}");
        }
        if (seconds >= 3600)
            return string.Create(CultureInfo.InvariantCulture,
                $"{seconds / 3600}:{seconds % 3600 / 60:00}:{seconds % 60:00}");
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    }

    /// <summary>"41g", "2a 41g" — usato anche dalla streak Resistance in formato tempo-trascorso.</summary>
    public static string FormatDays(int days)
    {
        if (days < 0) days = 0;
        return days < 365
            ? string.Create(CultureInfo.InvariantCulture, $"{days}g")
            : string.Create(CultureInfo.InvariantCulture, $"{days / 365}a {days % 365}g");
    }
}
```
> Suffissi "g"/"a" lato display: se preferisci localizzarli, mettili in resx (`Duration.DaysSuffix`, `Duration.YearsSuffix`) — scelta consentita, aggiorna i test di conseguenza.

- [ ] **Step 4: Run** → PASS. **Step 5: Commit** — `git commit -m "feat(wpf): DurationFormat — mm:ss mask parse + auto-scaling display"`

---

### Task 5: Cella durata in `SeriesCellViewModel`

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelTests.cs` (aggiungi)

- [ ] **Step 1: Test**

```csharp
private static MetricDefinition SecondsMetric() =>
    new() { Id = Guid.NewGuid(), Name = "Tempo", Type = MetricType.Seconds };

[Fact]
public void Seconds_cell_parses_duration_into_value_seconds()
{
    var cell = new SeriesCellViewModel(SecondsMetric()) { DurationText = "1:02:05" };
    var v = cell.BuildValue(0);
    Assert.NotNull(v);
    Assert.Equal(3725, v!.ValueSeconds);
    Assert.Null(v.ValueInteger);
}

[Fact]
public void Seconds_cell_invalid_text_is_flagged_and_yields_null()
{
    var cell = new SeriesCellViewModel(SecondsMetric()) { DurationText = "abc" };
    Assert.True(cell.IsDurationInvalid);
    Assert.Null(cell.BuildValue(0));
}

[Fact]
public void SetDurationSeconds_round_trips_display()
{
    var cell = new SeriesCellViewModel(SecondsMetric());
    cell.SetDurationSeconds(3725);
    Assert.Equal("1:02:05", cell.DurationText);
}
```

- [ ] **Step 2: Run** → FAIL. **Step 3: Implementa** in `SeriesCellViewModel`:

```csharp
using Recordum.WPF.Formatting; // in testa

/// <summary>True per la cella durata (Measured + Seconds): usa DurationText/mask invece di IntegerValue.</summary>
public bool IsDuration => Kind == MetricInputKind.Measured && Metric.Type == MetricType.Seconds;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsDurationInvalid))]
private string? _durationText;

public bool IsDurationInvalid =>
    !string.IsNullOrWhiteSpace(DurationText) && !DurationFormat.TryParse(DurationText, out _);

public void SetDurationSeconds(int seconds) => DurationText = DurationFormat.Format(seconds);
```
E in `BuildValue`, ramo Measured, sostituisci il case Seconds:
```csharp
case MetricType.Integer:
    if (IntegerValue is null) return null;
    v.ValueInteger = IntegerValue;
    return v;
case MetricType.Seconds:
    if (!DurationFormat.TryParse(DurationText, out var secs)) return null;
    v.ValueSeconds = secs;
    return v;
```
(I lettori coalescono già `ValueDecimal ?? ValueInteger ?? ValueSeconds`: i vecchi dati Seconds scritti in `ValueInteger` restano leggibili.)

- [ ] **Step 4: Run** → PASS (eventuali test esistenti che scrivevano `IntegerValue` su metriche Seconds: aggiornali a `DurationText`). **Step 5: Commit** — `git commit -m "feat(wpf): duration cell — mm:ss mask writes ValueSeconds"`

---

### Task 6: Righe valide + `CounterSeriesQuickAddViewModel`

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesRowViewModel.cs`
- Rename: `ResistanceQuickAddViewModel.cs` → `CounterSeriesQuickAddViewModel.cs` (classe e file; `git mv`)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelTests.cs`

- [ ] **Step 1: Test**

```csharp
[Fact]
public void Row_is_valid_when_any_cell_has_a_value()
{
    var row = new SeriesRowViewModel(new[] { IntMetric("Ripetizioni"), SecondsMetric() }); // IntMetric: helper analogo a SecondsMetric, Type = MetricType.Integer
    Assert.False(row.IsValid);
    row.Cells[0].IntegerValue = 14;
    Assert.True(row.IsValid);
}

[Fact]
public async Task Save_skips_empty_rows_and_reindexes_series()
{
    var (vm, completions) = CreateCounterSeriesVm(); // helper della classe: fake ICompletionService che cattura draft+values
    await vm.LoadMetricsAsync();
    vm.AddSeriesCommand.Execute(null);
    vm.AddSeriesCommand.Execute(null);                 // 3 righe
    vm.Series[0].Cells[0].IntegerValue = 14;
    vm.Series[2].Cells[0].IntegerValue = 11;           // la riga 1 resta vuota
    await vm.SaveAsync();
    var indexes = completions.LastValues!.Select(v => v.SeriesIndex).Distinct().OrderBy(i => i).ToList();
    Assert.Equal(new[] { 0, 1 }, indexes);             // reindicizzate senza buchi
}

[Fact]
public async Task Missing_field_in_valid_row_produces_no_value()
{
    var (vm, completions) = CreateCounterSeriesVm();
    await vm.LoadMetricsAsync();
    vm.Series[0].Cells[0].IntegerValue = 14;           // Ripetizioni sì, Tempo no
    await vm.SaveAsync();
    Assert.Single(completions.LastValues!);            // un solo CompletionValue (spec D6)
}
```
Adatta gli helper/fake esistenti della classe test (c'è già un fake `ICompletionService` per i test Resistance: estendilo con `LastValues`).

- [ ] **Step 2: Run** → FAIL. **Step 3: Implementa**

`SeriesRowViewModel.cs` — validità osservabile:
```csharp
public sealed partial class SeriesRowViewModel : ObservableObject
{
    public IReadOnlyList<SeriesCellViewModel> Cells { get; }

    public SeriesRowViewModel(IEnumerable<MetricDefinition> metrics)
        : this(metrics.Select(m => new SeriesCellViewModel(m))) { }

    public SeriesRowViewModel(IEnumerable<SeriesCellViewModel> cells)
    {
        Cells = cells.ToList();
        foreach (var c in Cells)
            c.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsValid));
    }

    /// <summary>Serie valida = almeno un campo compilato (spec D6). Il ✓/○ in UI e il conteggio
    /// "Registra N serie" derivano da qui.</summary>
    public bool IsValid => Cells.Any(c => c.BuildValue(0) is not null);
}
```

`CounterSeriesQuickAddViewModel` (ex Resistance, rinominata):
- `SaveAsync` filtra e reindicizza:
```csharp
var validRows = Series.Where(r => r.IsValid).ToList();
var values = new List<CompletionValue>();
for (int i = 0; i < validRows.Count; i++)
    foreach (var cell in validRows[i].Cells)
        if (cell.BuildValue(i) is { } v) values.Add(v);
if (values.Count == 0) { ErrorMessage = Strings.QuickAdd_Error_ValueRequired; return; }
```
- Aggiungi il conteggio per il bottone:
```csharp
public int ValidSeriesCount => Series.Count(r => r.IsValid);
```
notificato sia su `Series.CollectionChanged` sia su ogni `row.PropertyChanged` (aggancia in `NewRow()`/`LoadMetricsAsync`/`AddSeries` quando aggiungi righe: `row.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ValidSeriesCount));`).
- Rinomina ovunque (`QuickAddTemplateSelector`, `HabitScreenViewModel`, test): il routing per tipo arriva al Task 7 — in questo task fai solo rename meccanico per compilare.

- [ ] **Step 4: Run suite WPF** → PASS. **Step 5: Commit** — `git commit -m "feat(wpf): valid-row series — implicit check, reindexing, missing values"`

---

### Task 7: `CounterTapQuickAddViewModel` + routing per tipo

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/CounterTapQuickAddViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs:401-420` (BuildQuickAddAsync)
- Modify: `src/Recordum.WPF/Selectors/QuickAddTemplateSelector.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelTests.cs`

- [ ] **Step 1: Test**

```csharp
[Fact]
public async Task Tap_records_bare_completion_and_increments_day_count()
{
    var (vm, completions) = CreateCounterTapVm(); // fake completions che cattura le RecordAsync e risponde a ListByHabitAsync
    await vm.LoadDayAsync();
    await vm.TapCommand.ExecuteAsync(null);
    Assert.Equal(1, vm.DayCount);
    Assert.Null(completions.LastValues); // nessun CompletionValue (occorrenza nuda)
}

[Fact]
public async Task UndoLast_deletes_newest_completion_of_the_day()
{
    var (vm, completions) = CreateCounterTapVm();
    await vm.TapCommand.ExecuteAsync(null);
    await vm.TapCommand.ExecuteAsync(null);
    await vm.UndoLastCommand.ExecuteAsync(null);
    Assert.Equal(1, vm.DayCount);
    Assert.Equal(completions.RecordedIds[1], completions.DeletedIds.Single()); // l'ultima registrata
}
```

- [ ] **Step 2: Run** → FAIL. **Step 3: Implementa**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Quick-add del counter NUDO (zero metriche): ogni tap = un'occorrenza registrata subito;
/// il conteggio del giorno è il numero di completion. ⌫ annulla l'ultima del giorno. Spec multivalue §4.1.</summary>
public sealed partial class CounterTapQuickAddViewModel(ICompletionService completions, IClock clock)
    : QuickAddViewModelBase(completions, clock)
{
    [ObservableProperty] private int _dayCount;

    /// <summary>Conta le occorrenze del giorno selezionato (CompletionDate, anche retroattivo).</summary>
    public async Task LoadDayAsync(CancellationToken ct = default)
    {
        if (Node is null) { DayCount = 0; return; }
        var (from, to) = DayWindowUtc();
        var list = await Completions.ListByHabitAsync(Node.Id, from, to, ct: ct).ConfigureAwait(true);
        DayCount = list.Count;
    }

    [RelayCommand]
    private Task Tap(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var (ts, ms, retro) = ResolveCompletionTimestamp();
            var id = await Completions.RecordAsync(new Completion
            {
                HabitNodeId = Node.Id, CompletedAt = ts, CompletedAtMs = ms, IsRetroactive = retro,
            }, ct: ct).ConfigureAwait(true);
            DayCount++;
            UndoLastCommand.NotifyCanExecuteChanged();
            OnSaved(id);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsSaving = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoLast(CancellationToken ct)
    {
        if (Node is null) return;
        var (from, to) = DayWindowUtc();
        var list = await Completions.ListByHabitAsync(Node.Id, from, to, ct: ct).ConfigureAwait(true);
        var newest = list.OrderByDescending(c => c.CompletedAtMs).FirstOrDefault();
        if (newest is null) return;
        await Completions.DeleteAsync(newest.Id, ct).ConfigureAwait(true);
        DayCount = Math.Max(0, DayCount - 1);
        UndoLastCommand.NotifyCanExecuteChanged();
    }

    private bool CanUndo() => DayCount > 0;

    private (DateTimeOffset From, DateTimeOffset To) DayWindowUtc()
    {
        var day = DateTime.SpecifyKind(CompletionDate.Date, DateTimeKind.Utc);
        return (new DateTimeOffset(day), new DateTimeOffset(day.AddDays(1)));
    }
}
```
> Nota: `OnSaved` fa già ricaricare CompletionList/calendario via eventi esistenti, quindi il tap aggiorna tutte le superfici. Verifica che `partial void OnCompletionDateChanged` non esista in base — se serve ricaricare il conteggio al cambio data, gestiscilo in `HabitScreenViewModel` (che già reimposta `CompletionDate`): chiama `LoadDayAsync` dopo averla impostata.

`HabitScreenViewModel.BuildQuickAddAsync` — routing nuovo:
```csharp
private async Task BuildQuickAddAsync(HabitNode node, CancellationToken ct)
{
    QuickAddViewModelBase? vm = node.HabitType switch
    {
        HabitType.Action => new ActionQuickAddViewModel(_completions, _clock),
        HabitType.Counter => await BuildCounterQuickAddAsync(ct).ConfigureAwait(true),
        HabitType.Resistance => new MeasuredQuickAddViewModel(_completions, _metrics, _refData, _clock),
        HabitType.Journal => new JournalQuickAddViewModel(_completions, _clock),
        HabitType.State => new MeasuredQuickAddViewModel(_completions, _metrics, _refData, _clock),
        _ => null,
    };
    if (vm is null) { QuickAdd = null; return; }
    vm.Node = node;
    switch (vm)
    {
        case MeasuredQuickAddViewModel mq: await mq.LoadMetricsAsync(ct).ConfigureAwait(true); break;
        case CounterSeriesQuickAddViewModel cs: await cs.LoadMetricsAsync(ct).ConfigureAwait(true); break;
        case CounterTapQuickAddViewModel tap: await tap.LoadDayAsync(ct).ConfigureAwait(true); break;
    }
    QuickAdd = vm;

    async Task<QuickAddViewModelBase> BuildCounterQuickAddAsync(CancellationToken c)
    {
        var defs = await _metrics.ListByHabitAsync(node.Id, c).ConfigureAwait(true);
        return defs.Count == 0
            ? new CounterTapQuickAddViewModel(_completions, _clock)
            : new CounterSeriesQuickAddViewModel(_completions, _metrics, _refData, _clock);
    }
}
```
Attenzione all'ordine: `vm.Node = node` deve restare PRIMA delle `Load*` (com'è oggi). Sposta l'assegnazione `Node` prima dello switch come nel codice sopra.

`QuickAddTemplateSelector`:
```csharp
public DataTemplate? Action { get; set; }
public DataTemplate? Measured { get; set; }
public DataTemplate? CounterSeries { get; set; }
public DataTemplate? CounterTap { get; set; }
public DataTemplate? Journal { get; set; }

public override DataTemplate? SelectTemplate(object? item, DependencyObject container) => item switch
{
    ActionQuickAddViewModel => Action,
    MeasuredQuickAddViewModel => Measured,
    CounterSeriesQuickAddViewModel => CounterSeries,
    CounterTapQuickAddViewModel => CounterTap,
    JournalQuickAddViewModel => Journal,
    _ => null,
};
```

- [ ] **Step 4: Run suite WPF** → la XAML (`HabitScreen.xaml:510`) passa ancora `Resistance=` al selector: rinomina la property nel markup in `CounterSeries=` e aggiungi `CounterTap={StaticResource CounterTapQuickAddTemplate}` (il template arriva al Task 8 — per compilare ora, punta temporaneamente `CounterTap` allo stesso `ActionQuickAddTemplate`? NO: lascia la property non assegnata, il selector ritorna null e il pannello resta vuoto finché il Task 8 non aggiunge il template — accettabile tra un commit e l'altro ma NON a fine task: esegui Task 8 prima di chiudere la sessione). Run → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(wpf): counter tap quick-add (+1/undo) + per-shape routing"`

---

### Task 8: XAML quick-add — template tap, serie con ✓/○, durata

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (righe ~121, ~293, ~320, ~510)
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`

- [ ] **Step 1: resx**

| Key | EN | IT |
|---|---|---|
| `HabitScreen.QuickAdd.Tap` | `+1` | `+1` |
| `HabitScreen.QuickAdd.TodayCount` | `{0} today` | `{0} oggi` |
| `HabitScreen.QuickAdd.UndoLast` | `Undo last` | `Annulla ultima` |
| `HabitScreen.QuickAdd.SaveSeries` | `Record {0} series` | `Registra {0} serie` |
| `HabitScreen.QuickAdd.ChronoStart` | `Start stopwatch` | `Avvia cronometro` |
| `HabitScreen.QuickAdd.ChronoStop` | `Stop and fill in the time` | `Ferma e compila il tempo` |

- [ ] **Step 2: Template tap** (dopo `ActionQuickAddTemplate`):
```xml
<DataTemplate x:Key="CounterTapQuickAddTemplate" DataType="{x:Type qa:CounterTapQuickAddViewModel}">
    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
        <Button Command="{Binding TapCommand}" Width="64" Height="64"
                Content="{x:Static res:Strings.HabitScreen_QuickAdd_Tap}"
                FontSize="22" FontWeight="SemiBold">
            <Button.Resources>
                <Style TargetType="Border"><Setter Property="CornerRadius" Value="32"/></Style>
            </Button.Resources>
        </Button>
        <StackPanel Margin="14,0,0,0" VerticalAlignment="Center">
            <TextBlock FontSize="20" FontWeight="Bold">
                <TextBlock.Text>
                    <Binding Path="DayCount" StringFormat="{x:Static res:Strings.HabitScreen_QuickAdd_TodayCount}"/>
                </TextBlock.Text>
            </TextBlock>
            <Button Command="{Binding UndoLastCommand}" Style="{StaticResource Recordum.LinkButton}"
                    Content="{x:Static res:Strings.HabitScreen_QuickAdd_UndoLast}"/>
        </StackPanel>
    </StackPanel>
</DataTemplate>
```
> Se `Recordum.LinkButton` non esiste, usa lo stile ghost/flat già usato dai bottoni secondari del quick-add esistente (guarda `ResistanceQuickAddTemplate` per il pattern reale e riusa quello).

- [ ] **Step 3: Template serie** — rinomina `ResistanceQuickAddTemplate` → `CounterSeriesQuickAddTemplate` (`DataType` → `CounterSeriesQuickAddViewModel`). Dentro l'`ItemsControl` delle righe aggiungi in testa alla riga l'indicatore di validità:
```xml
<TextBlock Width="18" TextAlignment="Center" VerticalAlignment="Center"
           Text="{Binding IsValid, Converter={StaticResource BoolToCheckMark}}"/>
```
con converter nuovo in `App.xaml`/converters (riusa il pattern di `InverseBooleanConverter`):
```csharp
// src/Recordum.WPF/Converters/BoolToCheckMarkConverter.cs — "✓" se true, "○" se false
public sealed class BoolToCheckMarkConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "✓" : "○";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```
(registra `<conv:BoolToCheckMarkConverter x:Key="BoolToCheckMark"/>` accanto agli altri converter in `App.xaml`; colora il ✓ con il brush di successo a tema se disponibile, altrimenti foreground normale e opacità 0.5 per "○" via Style/DataTrigger su `IsValid`). Il bottone Salva diventa:
```xml
<Button Command="{Binding SaveCommand}">
    <Button.Content>
        <Binding Path="ValidSeriesCount" StringFormat="{x:Static res:Strings.HabitScreen_QuickAdd_SaveSeries}"/>
    </Button.Content>
</Button>
```
- [ ] **Step 4: Cella durata** — nel template celle (usato sia da Measured sia da CounterSeries: cerca il `DataTemplate` per `SeriesCellViewModel` o il blocco kind-aware dentro i due template), aggiungi il ramo durata: quando `IsDuration` mostra `TextBox Text="{Binding DurationText, UpdateSourceTrigger=PropertyChanged}"` con `ToolTip` dell'esempio "12:30" e bordo rosso quando `IsDurationInvalid` (DataTrigger → `BorderBrush` errore a tema), al posto del TextBox numerico `IntegerValue`. Visibilità: i rami esistenti scelgono già per Kind — aggiungi il DataTrigger su `IsDuration` PRIMA del ramo Measured-Integer.
- [ ] **Step 5: Selector** a riga ~510: `CounterSeries="{StaticResource CounterSeriesQuickAddTemplate}" CounterTap="{StaticResource CounterTapQuickAddTemplate}"` (via `Resistance=`).
- [ ] **Step 6: Build + suite + verifica visiva** — `dotnet build`; suite WPF verde; lancio rapido con skill `run` su una habit counter nuda e una con campi.
- [ ] **Step 7: Commit** — `git commit -m "feat(wpf): quick-add XAML — tap counter, series validity marks, duration cell"`

---

### Task 9: `CounterQuickActionHandler` — +1 nudo / navigazione

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Quick/CounterQuickActionHandler.cs`
- Test: cerca i test esistenti del handler (`grep -r "CounterQuickActionHandler" tests/`) e adattali

- [ ] **Step 1: Verifica semantica del `false`** — leggi il chiamante (grep `QuickActionHandlerSelector` / `ExecuteAsync` in `HomeViewModel` o nei behavior): `OpenScreenQuickActionHandler` ritorna `false` per dire "non gestito → apri la schermata". Conferma che il fallback navighi; se il chiamante non naviga sul `false`, aggiungi la navigazione lì (stesso percorso usato da Resistance/Journal/State oggi).
- [ ] **Step 2: Test**
```csharp
[Fact]
public async Task Bare_counter_records_occurrence_without_values()
{
    // fake metrics → lista vuota; fake completions cattura la chiamata
    var ok = await handler.ExecuteAsync(habitId);
    Assert.True(ok);
    Assert.Null(completions.LastValues);
}

[Fact]
public async Task Counter_with_metrics_defers_to_screen()
{
    // fake metrics → 1 metrica
    Assert.False(await handler.ExecuteAsync(habitId));
    Assert.Empty(completions.RecordedIds);
}
```
- [ ] **Step 3: Implementa**
```csharp
public async ValueTask<bool> ExecuteAsync(Guid habitNodeId, CancellationToken ct = default)
{
    var list = await metrics.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(false);
    if (list.Count > 0) return false; // con campi il +1 cieco non ha senso: apri la schermata

    await completions.RecordAsync(new Completion
    {
        HabitNodeId = habitNodeId,
        CompletedAt = clock.UtcNow.UtcDateTime,
        CompletedAtMs = clock.UtcNowMs,
    }, ct: ct).ConfigureAwait(false);
    return true;
}
```
- [ ] **Step 4: Run suite WPF** → PASS. **Step 5: Commit** — `git commit -m "feat(wpf): home +1 quick action — bare occurrence or open screen"`

---

### Task 10: Crono-cella — `StopWithoutRecordingAsync` + ⏱ nei quick-add

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/IActiveTimerService.cs`
- Modify: `src/Recordum.Data/Services/ActiveTimerService.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/CounterSeriesQuickAddViewModel.cs` e `MeasuredQuickAddViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (bottone ⏱ nella cella durata)
- Test: `tests/Recordum.Data.Tests/...ActiveTimerServiceTests.cs` + QuickAdd tests WPF

- [ ] **Step 1: Test Data**
```csharp
[Fact]
public async Task StopWithoutRecording_returns_elapsed_and_removes_timer_without_completion()
{
    // pattern dei test esistenti di ActiveTimerService: clock fake, StartAsync, avanza il clock di 95s
    var elapsed = await svc.StopWithoutRecordingAsync(habitId);
    Assert.Equal(95, elapsed);
    Assert.Null(await svc.GetActiveAsync(habitId));
    Assert.Empty(db.Completions); // nessuna completion registrata (a differenza di StopAsync)
}
```
- [ ] **Step 2: Interfaccia + implementazione**
```csharp
// IActiveTimerService
/// <summary>Ferma e rimuove il timer SENZA registrare una Completion; ritorna i secondi trascorsi
/// (al netto delle pause). Usato dal crono-cella che riversa il tempo in un campo durata.</summary>
ValueTask<int> StopWithoutRecordingAsync(Guid habitNodeId, CancellationToken ct = default);
```
Implementazione in `ActiveTimerService`: replica il calcolo elapsed di `StopAsync` (startedAtMs/accumulatedPausedMs/pausedAtMs — riusa l'helper privato se c'è, altrimenti estrailo) e cancella la riga, senza creare la `Completion`. Run test Data → PASS.
- [ ] **Step 3: Test WPF**
```csharp
[Fact]
public async Task Chrono_toggle_fills_the_duration_cell()
{
    var (vm, timers) = CreateCounterSeriesVmWithTimers(); // fake IActiveTimerService: Start registra, StopWithoutRecording → 95
    await vm.LoadMetricsAsync();
    var cell = vm.Series[0].Cells.First(c => c.IsDuration);
    await vm.ToggleChronoCommand.ExecuteAsync(cell);  // start
    Assert.True(vm.ChronoRunning);
    await vm.ToggleChronoCommand.ExecuteAsync(cell);  // stop
    Assert.Equal("1:35", cell.DurationText);
    Assert.False(vm.ChronoRunning);
}
```
- [ ] **Step 4: Implementa nel VM** (identico in `CounterSeriesQuickAddViewModel` e `MeasuredQuickAddViewModel`; aggiungi `IActiveTimerService timers` come 5° parametro ctor e aggiorna i call-site in `HabitScreenViewModel` + test):
```csharp
[ObservableProperty] private bool _chronoRunning;
private SeriesCellViewModel? _chronoCell;

[RelayCommand]
private async Task ToggleChrono(SeriesCellViewModel? cell, CancellationToken ct)
{
    if (Node is null || cell is null || !cell.IsDuration) return;
    if (!ChronoRunning)
    {
        // Un solo timer attivo per habit (UNIQUE su active_timer): se il timer integrato della
        // habit sta già correndo, il crono-cella non parte (spec §8).
        if (await _timers.GetActiveAsync(Node.Id, ct).ConfigureAwait(true) is not null) return;
        await _timers.StartAsync(Node.Id, TimerMode.Stopwatch, ct).ConfigureAwait(true);
        _chronoCell = cell;
        ChronoRunning = true;
    }
    else
    {
        var secs = await _timers.StopWithoutRecordingAsync(Node.Id, ct).ConfigureAwait(true);
        (_chronoCell ?? cell).SetDurationSeconds(secs);
        _chronoCell = null;
        ChronoRunning = false;
    }
}
```
- [ ] **Step 5: XAML** — accanto al TextBox durata (Task 8 Step 4) un ToggleButton-icona ⏱ (`Icon.Timer` Lucide se esiste in `Icons.xaml`, altrimenti aggiungilo col path Lucide "timer"): `Command="{Binding DataContext.ToggleChronoCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" CommandParameter="{Binding}"`, ToolTip Start/Stop via DataTrigger su `ChronoRunning`. Nota recovery: il crono compare già nella `TimerStatusBar` (lista `active_timer`); dopo un crash lo stop dalla status bar segue il percorso M3.5 esistente (registra una completion con il tempo) — comportamento accettato dalla spec §4.1, nessun lavoro aggiuntivo.
- [ ] **Step 6: Run entrambe le suite** → PASS. **Step 7: Commit** — `git commit -m "feat(wpf,data): per-cell stopwatch on active_timer fills the duration cell"`

---

### Task 11: Migrazione dati v2.10 — reclass resistance→counter

**Files:**
- Create: migration `CounterReclassV210` in `src/Recordum.Data/Migrations/`
- Test: `tests/Recordum.Data.Tests/` (segui il pattern dei test migrazione esistenti, es. quelli di StreakBrokenAtV26/BackupLogV27)

- [ ] **Step 1: Genera la migration vuota**
```bash
dotnet ef migrations add CounterReclassV210 --project src/Recordum.Data --startup-project src/Recordum.WPF
```
(nessun cambio modello → `Up` vuoto; verifica che il Designer non contenga delta).
- [ ] **Step 2: Scrivi `Up`/`Down`**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Spec multivalue §5: le ex-"serie" create dal builder (resistance con metriche misurate)
    // diventano counter; le resistance d'astinenza (zero metriche misurate) restano.
    migrationBuilder.Sql("""
        UPDATE habit_node SET habit_type = 'Counter'
        WHERE habit_type = 'Resistance'
          AND id IN (SELECT habit_node_id FROM metric_definition
                     WHERE scale_template_id IS NULL
                       AND labels_json IS NULL
                       AND type IN ('Integer','Decimal','Seconds'));
        """);
    migrationBuilder.Sql("UPDATE app_setting SET value = '2.10' WHERE key = 'schema_version';");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Il reclass non è reversibile senza perdita (non sappiamo più quali counter fossero resistance).
    migrationBuilder.Sql("UPDATE app_setting SET value = '2.9' WHERE key = 'schema_version';");
}
```
Verifica le literal del CHECK (`'Counter'`,`'Resistance'` PascalCase — confermate in `20260514101635_InitialSchema.cs:99`) e che `metric_definition.type` usi le stesse literal PascalCase (`grep "type IN" src/Recordum.Data/Migrations/20260514101635_InitialSchema.cs`).
- [ ] **Step 3: Test**
```csharp
[Fact]
public async Task Reclass_converts_series_resistance_and_keeps_abstinence()
{
    // pattern dei test migrazione esistenti: DB seminato pre-migrazione
    // 1) resistance "Flessioni" + metrica Integer "Ripetizioni" (scale_template_id NULL, labels NULL)
    // 2) resistance "Niente fumo" senza metriche
    // 3) resistance "Voglie" + SOLA metrica Integer con LabelsJson (scala locale) → resta resistance
    // applica la migration → assert: 1 = Counter; 2,3 = Resistance; schema_version = "2.10"
}
```
(Scrivi il corpo seguendo l'helper di setup dei test migrazione esistenti in Data.Tests — stesso fixture SQLite; il test 3 copre il filtro `labels_json IS NULL`.)
- [ ] **Step 4: Run suite Data** → PASS; ispeziona il SQL con `dotnet ef migrations script --project src/Recordum.Data --startup-project src/Recordum.WPF` e confrontalo con la spec §5 prima di applicarlo al DB locale.
- [ ] **Step 5: Commit** — `git commit -m "feat(data): v2.10 data migration — series resistance reclassified as counter"`

---

### Task 12: Magnitudine = occorrenze

**Files:**
- Modify: `src/Recordum.Data/Services/CompletionService.cs:204-213` (GetDailyMagnitudeAsync)
- Test: test esistenti di `GetDailyMagnitudeAsync` in Data.Tests (aggiorna le attese) + nuovo caso

- [ ] **Step 1: Test nuovo**
```csharp
[Fact]
public async Task Counter_magnitude_counts_occurrences_not_values()
{
    // giorno con: 1 completion nuda + 1 completion con 3 SeriesIndex (0,1,2) → magnitudine 4
    var day = ...;
    var result = await svc.GetDailyMagnitudeAsync(habitId, day.Year, day.Month);
    Assert.Equal(4m, result.Single(r => r.Date == day).Magnitude);
}
```
- [ ] **Step 2: Implementa** — sostituisci i rami Counter e Resistance:
```csharp
HabitType.Resistance or HabitType.Counter =>
    g.Sum(c => Math.Max(1, c.Values.Select(v => v.SeriesIndex).Distinct().Count())),
```
e rimuovi la query `primaryMetricId` se non più usata da altri rami. (Il vecchio ramo Resistance non contava le completion senza valori — `Max(1, …)` corregge anche gli episodi nudi.)
- [ ] **Step 3: Aggiorna i test esistenti** che assumevano "counter = somma della prima metrica" (le attese cambiano in numero di occorrenze).
- [ ] **Step 4: Run suite Data** → PASS. **Step 5: Commit** — `git commit -m "feat(data): daily magnitude counts occurrences for counter and resistance"`

---

### Task 13: Streak Resistance in tempo trascorso

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs`
- Modify: la view Detail che mostra `CurrentLength` (`grep -rn "CurrentLength" src/Recordum.WPF/Views/`)
- Test: test esistenti di `StreakSummaryViewModel` (aggiungi un caso)

- [ ] **Step 1: Test**
```csharp
[Fact]
public async Task Resistance_current_streak_renders_elapsed_format()
{
    // aggregator fake → streak corrente di 771 giorni su un nodo Resistance
    await vm.LoadAsync(resistanceNode);
    Assert.Equal("2a 41g", vm.CurrentDisplay);
}

[Fact]
public async Task Non_resistance_current_streak_stays_numeric()
{
    await vm.LoadAsync(counterNode); // streak 12
    Assert.Equal("12", vm.CurrentDisplay);
}
```
- [ ] **Step 2: Implementa**
```csharp
using Recordum.WPF.Formatting; // in testa

[ObservableProperty] private string _currentDisplay = "0";
```
e in coda a `LoadAsync`:
```csharp
CurrentDisplay = node.HabitType == HabitType.Resistance
    ? DurationFormat.FormatDays(CurrentLength)
    : CurrentLength.ToString(CultureInfo.InvariantCulture);
```
Nella view sostituisci il binding del valore "streak corrente" da `CurrentLength` a `CurrentDisplay` (solo il numero grande; le altre statistiche restano numeriche).
- [ ] **Step 3: Run suite WPF** → PASS. **Step 4: Commit** — `git commit -m "feat(wpf): resistance current streak shown as elapsed time (2a 41g)"`

---

### Task 14: Seeder natura-aware

**Files:**
- Modify: `src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs:168-209` (SeedHabitAsync) + helper `ValueAsync`

- [ ] **Step 1: Riscrivi `SeedHabitAsync`**
```csharp
/// <summary>Populates an EXISTING habit (by name, case-insensitive) with ~90 days of plausible
/// completions, filling ALL its metrics nature-aware (spec multivalue §6): Integer/Decimal =
/// quantità con trend, Seconds = durate attività, scala = pesata. Counter con campi → 1–4 serie/giorno
/// con ~10% di campi lasciati vuoti; counter nudo → N occorrenze nude. No marker: run ripetuti sommano.</summary>
public async Task SeedHabitAsync(string name, CancellationToken ct = default)
{
    var rows = await tree.GetHomeTreeAsync(ct).ConfigureAwait(false);
    var row = rows.FirstOrDefault(r =>
        string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase) && r.HabitType is not null)
        ?? throw new InvalidOperationException($"Nessuna habit attiva chiamata '{name}'.");
    var type = row.HabitType!.Value;
    var defs = (await metrics.ListByHabitAsync(row.HabitNodeId, ct).ConfigureAwait(false))
        .OrderBy(m => m.SortOrder).ToList();

    var today = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow.UtcDateTime, clock.LocalTimeZone));
    var start = today.AddDays(-90);

    for (var day = start; day <= today; day = day.AddDays(1))
    {
        double progress = (day.DayNumber - start.DayNumber) / 90.0;
        switch (type)
        {
            case HabitType.Action when Chance(0.72):
                await ActionAsync(row.HabitNodeId, day, 7, 21, ct).ConfigureAwait(false);
                break;
            case HabitType.Journal when Chance(0.40):
                await completions.RecordAsync(
                    Draft(row.HabitNodeId, day, 7, 21, notesBody: JournalEntries[_rng.Next(JournalEntries.Length)]),
                    ct: ct).ConfigureAwait(false);
                break;
            case HabitType.Counter when defs.Count == 0 && Chance(0.85):
                for (int i = _rng.Next(1, 6); i > 0; i--)
                    await completions.RecordAsync(Draft(row.HabitNodeId, day, 7, 21), ct: ct).ConfigureAwait(false);
                break;
            case HabitType.Counter when defs.Count > 0 && Chance(0.72):
                await SeriesDayAsync(row.HabitNodeId, defs, _rng.Next(1, 5), progress, day, ct).ConfigureAwait(false);
                break;
            case HabitType.Resistance when Chance(0.12): // episodi/ricadute, rari
                await SeriesDayAsync(row.HabitNodeId, defs, 1, progress, day, ct).ConfigureAwait(false);
                break;
            case HabitType.State when defs.Count > 0 && Chance(0.85):
                await ValueAsync(row.HabitNodeId, defs[0].Id, WeightedMood(), day, 20, 22, ct).ConfigureAwait(false);
                break;
        }
    }
}

/// <summary>Una completion con N righe-serie, ogni cella popolata per natura; ~10% dei campi
/// (mai tutti in una riga) lasciati vuoti per esercitare il "mancante" (spec D6).</summary>
private async Task SeriesDayAsync(Guid habitId, IReadOnlyList<MetricDefinition> defs, int series,
    double progress, DateOnly day, CancellationToken ct)
{
    if (defs.Count == 0)
    {
        await completions.RecordAsync(Draft(habitId, day, 17, 21), ct: ct).ConfigureAwait(false);
        return;
    }
    var values = new List<CompletionValue>();
    for (int s = 0; s < series; s++)
    {
        var written = new List<CompletionValue>();
        for (int m = 0; m < defs.Count; m++)
        {
            bool skip = defs.Count > 1 && m > 0 && Chance(0.10);
            if (skip) continue;
            written.Add(MakeValue(defs[m], s, progress));
        }
        if (written.Count == 0) written.Add(MakeValue(defs[0], s, progress)); // riga mai tutta vuota
        values.AddRange(written);
    }
    await completions.RecordAsync(Draft(habitId, day, 17, 21), values, ct: ct).ConfigureAwait(false);
}

private CompletionValue MakeValue(MetricDefinition def, int seriesIndex, double progress)
{
    var v = new CompletionValue { MetricDefinitionId = def.Id, SeriesIndex = seriesIndex };
    bool isScale = def.ScaleTemplateId is not null || def.LabelsJson is not null;
    if (isScale) v.ValueInteger = WeightedMood() - 1;                       // indice 0-based
    else switch (def.Type)
    {
        case MetricType.Seconds: v.ValueSeconds = 20 + _rng.Next(0, 100) + (int)(progress * 30); break;
        case MetricType.Decimal: v.ValueDecimal = Math.Round((decimal)(0.5 + _rng.NextDouble() * 2.5 + progress), 2); break;
        default: v.ValueInteger = (int)(12 + progress * 20) + _rng.Next(0, 14); break;
    }
    return v;
}
```
Elimina il vecchio corpo (creazione metrica "Ripetizioni" inclusa: una habit senza metriche è un counter nudo legittimo, non va arricchita).
- [ ] **Step 2: Build + verifica manuale** — `dotnet build`; poi su una copia dei dati: `dotnet run --project src/Recordum.WPF -- --seed-habit flessioni` e controlla in Cronologia che ogni serie abbia ripetizioni+tempo con qualche buco.
- [ ] **Step 3: Run suite WPF completa** → verde. **Step 4: Commit** — `git commit -m "feat(wpf): --seed-habit fills every metric nature-aware with multi-series days"`

---

### Task 15: Chiusura — tracker, suite complete, verifica visiva

- [ ] **Step 1:** Suite complete: `dotnet test --nologo -v q` (tutti i progetti) → verdi (flaky noto `CompletionRecorded_event_reloads_the_list` ok in seconda run).
- [ ] **Step 2:** Aggiorna `docs/phase-4-issues.md`: sezione nuova in testa — R-multiValue `fixed` (riassunto a una riga per: grammatica v2, quick-add tre forme, crono-cella, reclass v2.10, magnitudine occorrenze, streak elapsed, seeder; SHA dei commit), verifica visiva utente da fare. Aggiorna `CLAUDE.md` §7 riga Fase 4 se tocchi i conteggi suite.
- [ ] **Step 3:** Commit — `git commit -m "docs: phase-4 tracker — R-multiValue executed"`
- [ ] **Step 4:** Verifica visiva con skill `run`: counter nudo (+1/⌫), flessioni multi-serie (✓/○, mancanti, Registra N serie, ⏱), Resistance astinenza (streak "Xg"), builder (palette nuova, anteprima tap), entrambi i temi.
