# Unità temporale obbligatoria su metrica durata — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Una metrica di tipo durata (`MetricType.Seconds`) non può più nascere senza un'unità temporale, e una registrazione singola si legge sempre come orologio mentre gli aggregati si leggono nell'unità.

**Architecture:** L'invariante vive in `Recordum.Core` (`SecondsDisplay`) come elenco pubblico dei codici temporali, e viene applicato ai due bordi UI che creano metriche: il gate di lancio del builder e la sezione Metriche di Configura. Nessun cambio di schema, nessuna migration, nessuna validazione nel service — le durate senza unità già in archivio restano legali e modificabili.

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions, `[WpfFact]` per i test che toccano tipi WPF.

**Spec:** `docs/superpowers/specs/2026-08-07-duration-unit-required-design.md`

## Global Constraints

- **Nessun cambio di schema.** `metric_definition.unit_of_measure_id` resta nullable. Nessuna migration, nessun bump di `schema_version`.
- **Nessuna stringa nuova.** Le due modifiche di copy riusano la chiave `HabitBuilder.LaunchHint` già esistente in entrambe le `.resx`. **Non toccare `Strings.Designer.cs`** — l'hook `guard-generated.js` blocca Edit/Write su quel file.
- **Nessun backfill.** Nessun task deve scrivere un'unità su una metrica esistente al caricamento. La coercizione avviene solo su un cambio di tipo esplicito dell'utente.
- **English-first** nelle stringhe utente (`Strings.resx` primario, `Strings.it.resx` secondario). Commenti in codice: inglese, come il resto dei file toccati.
- **Un test per comportamento**, TDD: test rosso verificato prima dell'implementazione.
- Comandi di verifica:
  - `dotnet test tests/Recordum.Core.Tests/Recordum.Core.Tests.csproj`
  - `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj`
  - filtro singolo: `dotnet test <csproj> --filter "FullyQualifiedName~<NomeTest>"`

---

## File Structure

**Modificati:**

| file | responsabilità dopo la modifica |
|---|---|
| `src/Recordum.Core/Formatting/SecondsDisplay.cs` | unica definizione di "unità temporale": l'elenco dei codici + le conversioni che ci si appoggiano |
| `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs` | consuma l'elenco da Core; il gate di lancio richiede l'unità sui campi Tempo |
| `src/Recordum.WPF/Resources/Strings.resx` · `Strings.it.resx` | il suggerimento di lancio nomina Tempo invece di Quantità |
| `src/Recordum.WPF/ViewModels/HabitEditor/Sections/MetricsSectionViewModel.cs` | ogni riga possiede la propria lista di unità, filtrata sul proprio tipo; il passaggio a durata coercizza l'unità |
| `src/Recordum.WPF/Views/HabitEditor/Sections/MetricsSectionView.xaml` | la tendina unità lega la lista della riga, non più quella condivisa della sezione |
| `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs` | una durata si legge sempre come orologio |

**Deliberatamente NON toccati** (se ti viene voglia di modificarli, fermati — è fuori piano):

- `ExportQueryService` e i formatter di `ExportEngine`: nell'export anche la riga singola resta numero + unità, perché una colonna `.csv` deve poter essere sommata in un foglio di calcolo e `7:30:00` non è un numero. È l'eccezione dichiarata alla regola "singolo = orologio" (spec §6.1). Nessuna modifica.
- I cinque siti che aggregano (`HabitTreeQueryService`, `CounterTodayContent`, `HabitScreenViewModel:1047`, `GoalProgressCardViewModel`, `ExportQueryService.BuildSummaryAsync`): chiamano già `SecondsDisplay.ConvertTotal` e restano com'erano.
- `SeriesCellViewModel`: il selettore h/min/sec della cella quick-add è l'unità con cui *digiti*, non quella della metrica. Resta com'è, ripiego su minuti incluso.
- Schema, migration, `MetricDefinitionService`: nessuna validazione lato dati (spec §3, alternative B e C scartate).

**Eliminati:**

| file | perché |
|---|---|
| `src/Recordum.WPF/Formatting/DurationUnitDisplay.cs` | unico chiamante rimosso dal Task 4 |
| `tests/Recordum.WPF.Tests/Formatting/DurationUnitDisplayTests.cs` | prova la classe eliminata |

---

### Task 1: L'elenco dei codici temporali vive in Core

**Files:**
- Modify: `src/Recordum.Core/Formatting/SecondsDisplay.cs`
- Test: `tests/Recordum.Core.Tests/Formatting/SecondsDisplayTests.cs`

**Interfaces:**
- Consumes: niente (primo task).
- Produces: `Recordum.Core.Formatting.SecondsDisplay.TimeUnitCodes` di tipo `IReadOnlyList<string>`, contenente esattamente `"hours"`, `"minutes"`, `"seconds"` in minuscolo. `IsTimeUnit(string? unitCode)` resta `static bool` con la stessa firma e la stessa insensibilità alle maiuscole di oggi.

- [ ] **Step 1: Write the failing test**

In fondo a `tests/Recordum.Core.Tests/Formatting/SecondsDisplayTests.cs`, prima della graffa di chiusura della classe:

```csharp
    [Fact]
    public void TimeUnitCodes_lists_the_three_units_a_duration_can_be_expressed_in()
        => SecondsDisplay.TimeUnitCodes.Should().Equal("hours", "minutes", "seconds");

    [Fact]
    public void Every_listed_code_is_a_time_unit_and_converts()
    {
        // Anti-divergence guard: the list the UI filters its pickers with and the predicate the
        // formatters branch on must stay the same set. Before this, the codes lived duplicated as a
        // private array in HabitBuilderViewModel while IsTimeUnit derived the notion another way.
        foreach (var code in SecondsDisplay.TimeUnitCodes)
        {
            SecondsDisplay.IsTimeUnit(code).Should().BeTrue($"'{code}' is listed as a time unit");
            SecondsDisplay.ConvertToUnit(1, code).Should().NotBeNull($"'{code}' must be convertible");
        }
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Recordum.Core.Tests/Recordum.Core.Tests.csproj --filter "FullyQualifiedName~TimeUnitCodes_lists_the_three_units"`

Expected: FAIL in compilazione — `'SecondsDisplay' does not contain a definition for 'TimeUnitCodes'`.

- [ ] **Step 3: Write minimal implementation**

In `src/Recordum.Core/Formatting/SecondsDisplay.cs`, aggiungi l'elenco in cima al corpo della classe (prima di `ConvertToUnit`):

```csharp
    /// <summary>The unit codes a duration can be expressed in, lowercase as the unit table stores them.
    /// Single source of truth: the UI filters its unit pickers with this list and <see cref="IsTimeUnit"/>
    /// answers from it, so the two can never disagree about what counts as a time unit.</summary>
    public static readonly IReadOnlyList<string> TimeUnitCodes = ["hours", "minutes", "seconds"];
```

e riscrivi `IsTimeUnit` sopra l'elenco, sostituendo il corpo attuale:

```csharp
    /// <summary>True when the code is a unit a duration can be expressed in. Callers use it to tell
    /// "this total was converted" from "this total is still raw seconds" — the latter covers both a
    /// non-time unit and a legacy duration metric with no unit chosen at all.</summary>
    public static bool IsTimeUnit(string? unitCode) =>
        unitCode is not null && TimeUnitCodes.Contains(unitCode, StringComparer.OrdinalIgnoreCase);
```

Aggiungi `using System.Linq;` in cima al file se non c'è già (serve a `Contains` con comparer).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Recordum.Core.Tests/Recordum.Core.Tests.csproj`

Expected: PASS, tutti. In particolare devono restare verdi i test preesistenti `IsTimeUnit_is_true_for_the_units_a_duration_can_convert_into` (che passa `"MINUTES"` maiuscolo — è il motivo di `OrdinalIgnoreCase`) e `IsTimeUnit_is_false_for_everything_else`.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.Core/Formatting/SecondsDisplay.cs tests/Recordum.Core.Tests/Formatting/SecondsDisplayTests.cs
git commit -m "refactor(core): one list defines what a time unit is"
```

---

### Task 2: Il builder non lancia con un campo Tempo senza unità

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs:57-72` (commento + array privato + `InitializeAsync`) e `:203-205` (`CanLaunch`)
- Modify: `src/Recordum.WPF/Resources/Strings.resx` (chiave `HabitBuilder.LaunchHint`)
- Modify: `src/Recordum.WPF/Resources/Strings.it.resx` (stessa chiave)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs`

**Interfaces:**
- Consumes: `SecondsDisplay.IsTimeUnit(string?)` dal Task 1 (il predicato, non l'elenco: il filtro parte dalle `UnitOfMeasure` caricate e ne interroga il `Code`).
- Produces: niente di nuovo verso gli altri task. `CanLaunch` resta `public bool`.

**Contesto che serve sapere:** il VM sottoscrive già `PropertyChanged` di ogni campo e rilancia lo stato del comando quando cambia `UnitOfMeasureId` (`AddField` → `OnFieldChanged`, riga ~137). Quindi **non** serve aggiungere alcuna notifica: basta la clausola in `CanLaunch`.

- [ ] **Step 1: Write the failing test**

In `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs`, in fondo alla classe:

```csharp
    [WpfFact]
    public void A_time_field_without_a_unit_blocks_launch()
    {
        // A duration with no unit has nothing to render its totals in: the day total, the goal and
        // the export all fall back to raw seconds. The unit is required at creation, mirroring the
        // rule a Scale field already enforces.
        var sut = NewSut();
        sut.Name = "Meditazione";
        sut.AddFieldCommand.Execute(FieldNature.Time);

        sut.CanLaunch.Should().BeFalse();
    }

    [WpfFact]
    public async Task Picking_a_time_unit_re_enables_the_launch_command()
    {
        var sut = NewSut();
        await sut.InitializeAsync();
        sut.Name = "Meditazione";
        sut.AddFieldCommand.Execute(FieldNature.Time);
        sut.CanLaunch.Should().BeFalse();
        var fired = false;
        sut.LaunchCommand.CanExecuteChanged += (_, _) => fired = true;

        sut.Fields[0].UnitOfMeasureId = sut.AvailableTimeUnits.Single(u => u.Code == "minutes").Id;

        fired.Should().BeTrue();
        sut.CanLaunch.Should().BeTrue();
        sut.LaunchCommand.CanExecute(null).Should().BeTrue();
    }

    [WpfFact]
    public void A_quantity_field_without_a_unit_still_launches()
    {
        // The new rule is scoped to Time: a quantity is a plain number and reads fine without a unit.
        var sut = NewSut();
        sut.Name = "Lettura";
        sut.AddFieldCommand.Execute(FieldNature.Quantity);

        sut.CanLaunch.Should().BeTrue();
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~A_time_field_without_a_unit_blocks_launch|FullyQualifiedName~Picking_a_time_unit_re_enables"`

Expected: FAIL. `A_time_field_without_a_unit_blocks_launch` fallisce con "Expected sut.CanLaunch to be false, but found True". `A_quantity_field_without_a_unit_still_launches` passa già oggi — è il guard-rail che dimostra che il Task non allarga la regola oltre Tempo.

- [ ] **Step 3: Write minimal implementation**

**3a.** In `HabitBuilderViewModel.cs`, sostituisci il commento e l'array privato (righe ~57-62):

```csharp
    /// <summary>Units offered for a Time field (hours / minutes / seconds). Picking one is required:
    /// a duration with no unit has nothing to express its totals, its goal or its export rows in.
    /// The list comes from Core so it cannot drift from what the formatters treat as a time unit.</summary>
    public ObservableCollection<UnitOfMeasure> AvailableTimeUnits { get; } = new();
```

(cioè: rimuovi la riga `private static readonly string[] TimeUnitCodes = { "hours", "minutes", "seconds" };` e riscrivi il commento XML sopra `AvailableTimeUnits`).

**3b.** In `InitializeAsync`, cambia la riga di filtro:

```csharp
        foreach (var u in AvailableUnits.Where(u => Recordum.Core.Formatting.SecondsDisplay.IsTimeUnit(u.Code)))
            AvailableTimeUnits.Add(u);
```

**3c.** Sostituisci `CanLaunch` (righe ~202-205):

```csharp
    /// <summary>Launch enabled only with a valid name, a scale on every Scale field and a time unit on
    /// every Time field. A Quantity field's unit stays optional — a bare number reads fine — but a
    /// duration without a unit has no unit to render its totals in.</summary>
    public bool CanLaunch =>
        !string.IsNullOrWhiteSpace(Name)
        && Fields.All(f => f.Nature != FieldNature.Scale || f.ScaleTemplateId is not null)
        && Fields.All(f => f.Nature != FieldNature.Time || f.UnitOfMeasureId is not null);
```

**3d.** In `src/Recordum.WPF/Resources/Strings.resx`, sostituisci il valore della chiave `HabitBuilder.LaunchHint`:

```xml
  <data name="HabitBuilder.LaunchHint" xml:space="preserve">
    <value>Name the habit, pick a scale for every Scale field and a unit for every Time field to launch.</value>
  </data>
```

**3e.** In `src/Recordum.WPF/Resources/Strings.it.resx`, stessa chiave:

```xml
  <data name="HabitBuilder.LaunchHint" xml:space="preserve">
    <value>Dai un nome, scegli una scala per ogni campo Scala e un'unità per ogni campo Tempo per lanciare.</value>
  </data>
```

**Nota:** la chiave esiste già in entrambi i file, quindi cambia solo il testo dentro `<value>`. `Strings.Designer.cs` **non** va toccato.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~HabitBuilderViewModelTests"`

Expected: PASS, tutti. Controlla in particolare che resti verde `Launch_persists_picked_time_unit_on_the_metric` (sceglie l'unità prima di lanciare, quindi la nuova clausola lo lascia passare) e che nessun altro test lanci con un campo Tempo senza unità.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs
git commit -m "feat(builder): a Time field must declare its unit before launch"
```

---

### Task 3: Configura assegna e filtra l'unità di una durata

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/Sections/MetricsSectionViewModel.cs` (entrambe le classi del file)
- Modify: `src/Recordum.WPF/Views/HabitEditor/Sections/MetricsSectionView.xaml:23-27`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/MetricsSectionViewModelTests.cs`

**Interfaces:**
- Consumes: `SecondsDisplay.IsTimeUnit(string?)` dal Task 1 (stesso motivo del Task 2: si filtrano oggetti `UnitOfMeasure`, non stringhe).
- Produces: `MetricRowViewModel` guadagna `public ObservableCollection<UnitOfMeasure> Units { get; }` (la lista che la tendina della riga lega) e il costruttore diventa `MetricRowViewModel(MetricDefinition metric, IMetricDefinitionService metrics, IReadOnlyList<UnitOfMeasure> allUnits)`. `MetricsSectionViewModel.AvailableUnits` resta pubblica (nessun ComboBox la lega più, ma un test esistente la verifica).

**Contesto che serve sapere:** `AvailableUnits` è oggi l'`ItemsSource` **condivisa** da tutte le righe. Svuotarla in `LoadAsync` fa coerciare a `null` il `SelectedValue` di ogni ComboBox viva e una riga ancora attaccata persisterebbe quel null: è il data-wipe del commit `4d01f9c`, per cui esiste il `Detach()` e il test `Reload_with_combobox_coercion_does_not_wipe_metric_units`. Dando a ogni riga la **propria** lista (uno snapshot immutabile, non la collection della sezione) il meccanismo non può ripetersi. Il `Detach()` resta comunque dov'è.

- [ ] **Step 1: Write the failing test**

In `tests/Recordum.WPF.Tests/ViewModels/HabitEditor/MetricsSectionViewModelTests.cs`, in fondo alla classe:

```csharp
    [Fact]
    public async Task Switching_a_metric_to_duration_assigns_minutes()
    {
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];

        row.Type = MetricType.Seconds;
        await Task.Delay(20);

        var minutes = _refData.Units.Single(u => u.Code == "minutes");
        row.UnitOfMeasureId.Should().Be(minutes.Id);
        _metrics.Store[row.Id].UnitOfMeasureId.Should().Be(minutes.Id);
        _metrics.Store[row.Id].Type.Should().Be(MetricType.Seconds);
    }

    [Fact]
    public async Task Switching_to_duration_discards_a_non_time_unit()
    {
        // A metric that was a quantity in litres keeps nothing meaningful when it becomes a duration.
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];
        row.UnitOfMeasureId = _refData.Units.Single(u => u.Code == "l").Id;
        await Task.Delay(20);

        row.Type = MetricType.Seconds;
        await Task.Delay(20);

        row.UnitOfMeasureId.Should().Be(_refData.Units.Single(u => u.Code == "minutes").Id);
    }

    [Fact]
    public async Task Switching_to_duration_keeps_a_time_unit_already_chosen()
    {
        // Overwriting "hours" with minutes would silently throw away a choice the user made.
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];
        var hours = _refData.Units.Single(u => u.Code == "hours");
        row.UnitOfMeasureId = hours.Id;
        await Task.Delay(20);

        row.Type = MetricType.Seconds;
        await Task.Delay(20);

        row.UnitOfMeasureId.Should().Be(hours.Id);
    }

    [Fact]
    public async Task A_duration_row_offers_only_time_units_and_a_quantity_row_offers_all()
    {
        var sut = CreateSut();
        await sut.AddMetricCommand.ExecuteAsync(null);
        var row = sut.Metrics[0];

        row.Units.Select(u => u.Code).Should().BeEquivalentTo(_refData.Units.Select(u => u.Code));

        row.Type = MetricType.Seconds;

        row.Units.Select(u => u.Code).Should().BeEquivalentTo(new[] { "hours", "minutes", "seconds" });
    }

    [Fact]
    public async Task A_legacy_duration_keeps_its_foreign_unit_in_the_list()
    {
        // A duration carrying "l" from before the rule: dropping it from the row's list would leave the
        // ComboBox with a SelectedValue that matches no item, WPF would coerce it to null and the
        // attached row would persist that null — the 4d01f9c data-wipe through another door. No backfill
        // happens on load either: the metric keeps its foreign unit until the user picks a valid one.
        var litres = _refData.Units.Single(u => u.Code == "l");
        var metricId = Guid.NewGuid();
        await _metrics.CreateAsync(new MetricDefinition
        {
            Id = metricId, HabitNodeId = _node.Id, Name = "Vecchia durata",
            Type = MetricType.Seconds, UnitOfMeasureId = litres.Id, SortOrder = 0,
        });

        var sut = CreateSut();
        await Task.Delay(20);
        var row = sut.Metrics.Single();

        row.Units.Should().Contain(u => u.Id == litres.Id);
        row.UnitOfMeasureId.Should().Be(litres.Id);
        _metrics.Store[metricId].UnitOfMeasureId.Should().Be(litres.Id);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~MetricsSectionViewModelTests"`

Expected: FAIL in compilazione — `'MetricRowViewModel' does not contain a definition for 'Units'`.

- [ ] **Step 3: Write minimal implementation**

**3a.** In `MetricsSectionViewModel.cs`, aggiungi gli using in cima al file:

```csharp
using Recordum.Core.Formatting;
```

**3b.** Nella classe `MetricsSectionViewModel`, passa uno snapshot delle unità a ogni riga. In `LoadAsync`, sostituisci il ciclo finale:

```csharp
        NodeId = node.Id;
        // A snapshot, not AvailableUnits itself: each row owns its list, so emptying the section's
        // collection on the next load can never coerce a live row's SelectedValue to null (4d01f9c).
        var units = AvailableUnits.ToList();
        foreach (var m in (await _metrics.ListByHabitAsync(node.Id)).OrderBy(m => m.SortOrder))
            Metrics.Add(new MetricRowViewModel(m, _metrics, units));
```

e in `AddMetricAsync` sostituisci l'ultima riga:

```csharp
        Metrics.Add(new MetricRowViewModel(draft, _metrics, AvailableUnits.ToList()));
```

Aggiungi `using System.Linq;` in cima al file se non è già presente.

**3c.** Sostituisci **interamente** la classe `MetricRowViewModel` in fondo al file con:

```csharp
public sealed partial class MetricRowViewModel : ObservableObject
{
    private readonly IMetricDefinitionService _metrics;
    private readonly IReadOnlyList<UnitOfMeasure> _allUnits;
    private bool _detached;
    private bool _suppressUnitPersist;

    public Guid Id { get; }

    /// <summary>The units this row's picker offers. Per-row, not shared with the section: a shared
    /// ItemsSource that gets emptied coerces every live ComboBox's SelectedValue to null, and an
    /// attached row persists that null — the 4d01f9c data-wipe. Filtered to the three time units on a
    /// duration, plus whatever foreign unit a legacy duration already carries (dropping it would
    /// trigger the very same coercion).</summary>
    public ObservableCollection<UnitOfMeasure> Units { get; } = new();

    [ObservableProperty] private string _name;
    [ObservableProperty] private MetricType _type;
    [ObservableProperty] private Guid? _unitOfMeasureId;
    [ObservableProperty] private int _sortOrder;

    public MetricRowViewModel(MetricDefinition metric, IMetricDefinitionService metrics, IReadOnlyList<UnitOfMeasure> allUnits)
    {
        ArgumentNullException.ThrowIfNull(metric);
        _metrics = metrics;
        _allUnits = allUnits;
        Id = metric.Id;
        _name = metric.Name;
        _type = metric.Type;
        _unitOfMeasureId = metric.UnitOfMeasureId;
        _sortOrder = metric.SortOrder;
        // Construction never coerces: an existing unit-less duration stays unit-less until the user
        // acts on it. There is no backfill, by design.
        RebuildUnits();
    }

    /// <summary>Stops persisting changes. Called on the old rows before a reload, so view-side
    /// side effects (ComboBox SelectedValue coercion when ItemsSource empties) can't write back.</summary>
    internal void Detach() => _detached = true;

    partial void OnNameChanged(string value) { if (!_detached) _ = _metrics.UpdateAsync(Id, m => m.Name = value).AsTask(); }
    partial void OnSortOrderChanged(int value) { if (!_detached) _ = _metrics.UpdateAsync(Id, m => m.SortOrder = value).AsTask(); }

    partial void OnUnitOfMeasureIdChanged(Guid? value)
    {
        if (_detached || _suppressUnitPersist) return;
        _ = _metrics.UpdateAsync(Id, m => m.UnitOfMeasureId = value).AsTask();
    }

    partial void OnTypeChanged(MetricType value)
    {
        // A duration must carry a time unit: keep one already chosen, otherwise assign minutes and
        // drop a unit that made sense for the old type (litres on what is now a duration).
        var unit = value == MetricType.Seconds ? CoerceToTimeUnit(UnitOfMeasureId) : UnitOfMeasureId;
        if (unit != UnitOfMeasureId)
        {
            // Suppress the unit's own write: type and unit go out as ONE update below, so two
            // concurrent read-modify-write calls can't race and lose one of the two changes.
            _suppressUnitPersist = true;
            UnitOfMeasureId = unit;
            _suppressUnitPersist = false;
        }
        RebuildUnits();
        if (_detached) return;
        _ = _metrics.UpdateAsync(Id, m => { m.Type = value; m.UnitOfMeasureId = unit; }).AsTask();
    }

    private Guid? CoerceToTimeUnit(Guid? current)
    {
        var chosen = _allUnits.FirstOrDefault(u => u.Id == current);
        if (chosen is not null && SecondsDisplay.IsTimeUnit(chosen.Code)) return current;
        return _allUnits.FirstOrDefault(u => string.Equals(u.Code, "minutes", StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private void RebuildUnits()
    {
        Units.Clear();
        if (Type != MetricType.Seconds)
        {
            foreach (var u in _allUnits) Units.Add(u);
            return;
        }
        foreach (var u in _allUnits.Where(u => SecondsDisplay.IsTimeUnit(u.Code))) Units.Add(u);
        // Keep a legacy duration's foreign unit selectable so the binding still finds its item.
        if (_allUnits.FirstOrDefault(u => u.Id == UnitOfMeasureId) is { } current && !Units.Contains(current))
            Units.Add(current);
    }
}
```

**Attenzione:** `Units.Clear()` in `RebuildUnits` è sicuro — è la lista di *questa sola* riga, e viene subito ripopolata includendo l'unità corrente, quindi il `SelectedValue` ritrova sempre il proprio elemento.

**3d.** In `src/Recordum.WPF/Views/HabitEditor/Sections/MetricsSectionView.xaml`, sostituisci l'`ItemsSource` della terza ComboBox (righe 23-27):

```xml
                        <ComboBox Grid.Column="2" Margin="0,0,4,0"
                                  ItemsSource="{Binding Units}"
                                  DisplayMemberPath="Name"
                                  SelectedValuePath="Id"
                                  SelectedValue="{Binding UnitOfMeasureId}"/>
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~MetricsSectionViewModelTests"`

Expected: PASS, tutti. Deve restare verde anche `Reload_with_combobox_coercion_does_not_wipe_metric_units`, che ora è protetto due volte (dal `Detach()` e dal fatto che le righe non condividono più l'`ItemsSource`).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitEditor/Sections/MetricsSectionViewModel.cs src/Recordum.WPF/Views/HabitEditor/Sections/MetricsSectionView.xaml tests/Recordum.WPF.Tests/ViewModels/HabitEditor/MetricsSectionViewModelTests.cs
git commit -m "feat(editor): a duration metric picks its unit from the time units only"
```

---

### Task 4: Una registrazione singola si legge come orologio

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs:155`
- Modify: `src/Recordum.Core/Formatting/SecondsDisplay.cs:1-8` (commento che cita la classe eliminata)
- Delete: `src/Recordum.WPF/Formatting/DurationUnitDisplay.cs`
- Delete: `tests/Recordum.WPF.Tests/Formatting/DurationUnitDisplayTests.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CompletionListTests.cs` (riscrittura di un test esistente)

**Interfaces:**
- Consumes: niente dai task precedenti — questo task è indipendente e potrebbe girare per primo.
- Produces: niente.

**Contesto che serve sapere:** `DurationUnitDisplay` ha **un solo chiamante**, la riga 155 di `CompletionListViewModel`. Rimosso quello, la classe è morta e va eliminata insieme al suo file di test. Verificalo prima di cancellare con `grep -rn "DurationUnitDisplay" --include=*.cs src/ tests/`.

- [ ] **Step 1: Rewrite the existing test so it fails**

In `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CompletionListTests.cs` esiste già il test `LoadDayAsync_renders_a_duration_field_in_the_metrics_own_unit`, che asserisce la resa **vecchia** ("7,5 ore"). **Sostituiscilo interamente** (nome, commento e assert) con:

```csharp
    [WpfFact]
    public async Task LoadDayAsync_reads_a_duration_as_a_clock_even_when_the_metric_has_a_unit()
    {
        // A single recording reads as a duration, not as a decimal: 27000s is "7:30:00" whatever unit
        // the metric declares. The unit governs AGGREGATES — day total, goal, export — where a number
        // is what you compare against a target. Before this, a metric with a unit read "7,5 ore" here
        // and one without read "7:30:00": two layouts for the same fact.
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var habit = Guid.NewGuid();
        var hoursUnitId = rd.Units.Single(u => u.Code == "hours").Id;
        var sleepId = Guid.NewGuid();
        await ms.CreateAsync(new MetricDefinition { Id = sleepId, HabitNodeId = habit, Name = "Slept", Type = MetricType.Seconds, UnitOfMeasureId = hoursUnitId, SortOrder = 0 });

        var day = new DateOnly(2026, 6, 1);
        var onDay = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = onDay,
            CompletedAtMs = new DateTimeOffset(onDay).ToUnixTimeMilliseconds(),
            Values = new List<CompletionValue> { new() { MetricDefinitionId = sleepId, SeriesIndex = 0, ValueSeconds = 27000 } },
        };

        var sut = new CompletionListViewModel(cs, ms, rd, new FakeDiaryService());
        await sut.LoadDayAsync(habit, day);

        sut.Rows[0].FieldDisplays.Should().ContainSingle().Which.Should().Be("Slept: 7:30:00");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~LoadDayAsync_reads_a_duration_as_a_clock"`

Expected: FAIL con "Expected ... to be \"Slept: 7:30:00\", but found \"Slept: 7,5 ore\"" (o "7.5 hours" a seconda della cultura corrente). È il test discriminante: se passa subito, l'implementazione è già cambiata e qualcosa non torna.

- [ ] **Step 3: Write minimal implementation**

**3a.** In `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs`, dentro `FormatValue`, sostituisci il blocco della durata (commento incluso):

```csharp
        // Duration metrics store ValueSeconds only; without this branch a series with just a time
        // (e.g. 1:30) rendered as a blank field in Cronologia/Records despite being saved (CR13).
        // A single recording always reads as a clock — the metric's unit governs aggregates (day
        // total, goal, export), where the value is a number you compare against a target.
        if (v.ValueSeconds is { } secs) return DurationFormat.Format(secs);
```

**3b.** Elimina i due file:

```bash
git rm src/Recordum.WPF/Formatting/DurationUnitDisplay.cs tests/Recordum.WPF.Tests/Formatting/DurationUnitDisplayTests.cs
```

**3c.** In `src/Recordum.Core/Formatting/SecondsDisplay.cs`, il commento di classe cita la classe appena eliminata. Sostituiscilo con:

```csharp
/// <summary>Converts a stored value_seconds into the unit chosen on the metric (U8): hours, minutes,
/// or seconds. Numeric-only (no localized suffix — that belongs to the presentation layer). Used by
/// the aggregates: the Home row total, the "Today" total, the day strip, the goal's actual and the
/// export projection. A single recording is not an aggregate — it reads as a clock via
/// DurationFormat, so it does not come through here.</summary>
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj`

Expected: PASS, tutti. Deve restare verde `LoadDayAsync_renders_a_duration_field_value` (durata senza unità → "Time: 1:30"), che ora percorre esattamente lo stesso ramo del test nuovo.

Verifica che non resti alcun riferimento:

Run: `grep -rn "DurationUnitDisplay" --include=*.cs src/ tests/`
Expected: nessun risultato.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(records): a single duration always reads as a clock"
```

---

### Task 5: Verifica finale

**Files:** nessuno (solo verifica).

**Interfaces:**
- Consumes: tutti i task precedenti.
- Produces: niente.

- [ ] **Step 1: Run the whole suite**

```bash
dotnet test tests/Recordum.Core.Tests/Recordum.Core.Tests.csproj
dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj
dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj
```

Expected: tutte verdi. Riferimento prima di questo piano: **Core 111 / Data 430 / WPF 840**. Attesa del delta: Core `+2`; Data invariato (nessun task tocca quel progetto); WPF `+3` dal Task 2, `+5` dal Task 3, meno i casi di `DurationUnitDisplayTests` rimossi dal Task 4 — conta i casi `[InlineData]` effettivi prima di cancellare il file, non i metodi, e verifica che il totale finale quadri con quel conto. Il Task 4 riscrive un test esistente, quindi non sposta il totale.

- [ ] **Step 2: Confirm no schema or generated file was touched**

```bash
git diff --name-only main...HEAD | grep -E "Migrations/|ModelSnapshot|Designer\.cs"
```

Expected: nessun risultato. Se `Strings.Designer.cs` compare, qualcosa è andato storto: il piano non aggiunge chiavi nuove.

- [ ] **Step 3: Report**

Riassumi all'autore: cosa è cambiato a schermo (builder che blocca, Configura che assegna minuti, Cronologia a orologio), cosa resta da verificare a mano, e i conteggi finali della suite. Non dichiarare completo nulla che non abbia un test verde alle spalle.

---

## Verifica a schermo (dopo il piano, con l'autore)

Non fanno parte dei task — servono all'autore per il pass visivo:

1. Nuova abitudine → aggiungi un campo **Tempo** → "Crea" resta disabilitato e il suggerimento nomina Tempo; scegli minuti → si sblocca.
2. Configura › Metriche su un counter → porta una metrica a **Tempo** → l'unità diventa minuti da sola e la tendina offre solo ore/minuti/secondi.
3. Una registrazione di 7h30m in Cronologia legge `7:30:00`; il totale "Oggi" e il goal leggono nell'unità.
4. Le durate legacy senza unità dell'archivio: sistemale dalla tendina di Configura una per una.
