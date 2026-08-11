# Onda 2.5 — Builder superficie di composizione completa: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Il builder "Crea nuova abitudine" sa comporre tutto ciò che i template ufficiali contengono (programma, streak, goal, sessione+segmenti, serie nominate, unità/scale custom staged), mostra un'anteprima a 3 piani coi componenti reali (E7), e un guard round-trip template↔builder automatizza la parte meccanica di E16.

**Architecture:** I mattoni nuovi sono brick-VM che mutano il draft di composizione (non chiamano servizi — host-agnostici, spec §1). Il Lancia diventa un'unica `ComposedHabitRequest` estesa materializzata in una transazione (chiude W24-R1 by design). Il payload template si estende a specchio del lettore `HabitTemplateService.InstantiateAsync`, che guadagna risolvi-o-crea per unità/scale incorporate. Nessuna migrazione schema.

**Tech Stack:** C#/.NET 10, WPF + CommunityToolkit.Mvvm, EF Core + SQLite (SQLCipher), xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-08-11-wave-2-5-builder-design.md` (leggerla prima di ogni task).

## Global Constraints

- **Branch**: `wave-2-5-builder` (esiste già, la spec è il primo commit). Mai commit su `main`.
- **Build/test**: aggiungere sempre `-p:UseAppHost=false` (lock DriveFS). Suite WPF: se il run parallelo si appende (W24-E1), serializzare: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false -- xUnit.ParallelizeTestCollections=false`.
- **Stringhe utente**: una stringa nuova = **3 edit**: `src/Recordum.WPF/Resources/Strings.resx` (EN) + `Strings.it.resx` (IT) + accessor manuale in `Strings.Designer.cs` (`public static string X => Get("X");`). `Strings.Designer.cs` è bloccato dall'hook per Edit/Write: **editare via shell** (Bash: `python`/`sed`/heredoc). Verificare con un build subito dopo.
- **Mai `Clear()` su una collection che è `ItemsSource` di un controllo bindato two-way** senza sopprimere la persistenza per tutta l'operazione e ri-affermare il valore dopo (data-wipe `4d01f9c`/`1d96967`). I picker unità/scala con voci staged **aggiungono** (`Add`), non ricostruiscono.
- **`ContentControl` con `ContentTemplate`/`ContentTemplateSelector` statico**: sempre selector o guard, mai il solo binding null (C1, Onda 2.4). `ContentPresenter.ChooseTemplate()` applica il template anche con `Content` null.
- **`AsyncRelayCommand.ExecuteAsync` non consulta `CanExecute`**: ogni guard va ripetuto **dentro** il metodo (lezione R7).
- **Mai `DateTime.UtcNow/Now`**: solo `IClock`.
- **Niente migrazioni EF in quest'onda.** Se un task sembra richiederne una, fermarsi: è fuori spec.
- **Convenzioni test**: xUnit + FluentAssertions; Data usa `DbFixture` (`tests/Recordum.Data.Tests`); i test WPF su view/STA seguono i pattern esistenti (es. `HabitScreenDiarioColumnViewTests`).
- Commit frequenti, messaggi `feat(builder):` / `test(...)` / `docs:`, footer `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

## File Structure (mappa complessiva)

```
src/Recordum.Core/
  Composition/HabitComposition.cs          [M] via RecordingMode dal record
  Composition/HabitCompositionRules.cs     [M] via CanSetMode
  Formatting/ReferenceCodeSlug.cs          [C] slug codici unità/scale custom
  Abstractions/Services/ComposedHabitRequest.cs [M] request estesa
src/Recordum.Data/
  Services/ComposedHabitService.cs         [M] materializzazione completa 1-tx
  Services/HabitUiPrefsJson.cs             [C] serializer condiviso ui_prefs
  Services/HabitUiPrefsService.cs          [M] usa il serializer condiviso
  Services/HabitTemplateService.cs         [M] payload esteso + risolvi-o-crea
src/Recordum.WPF/ViewModels/HabitBuilder/
  HabitBuilderViewModel.cs                 [M] host: bricks, request, payload
  ScheduleBrickViewModel.cs                [C] mattone Programma
  GoalBrickViewModel.cs                    [C] mattone Obiettivo
  SessionBrickViewModel.cs                 [C] mattone Sessione
  StagedReferenceData.cs                   [C] unità/scale staged + micro-form VM
  RecordingPreviewViewModel.cs             [D] sostituito dall'anteprima 3 piani
  Preview/SamplePreviewFactory.cs          [C] modelli d'esempio deterministici
  Preview/SampleServices.cs                [C] stub servizi per i VM reali
src/Recordum.WPF/Views/HabitBuilder/
  HabitBuilderView.xaml(.cs)               [M] layout 2 colonne + zona avanzata
  BuilderPreviewPane.xaml(.cs)             [C] i 3 riquadri anteprima
tests/: Recordum.Core.Tests, Recordum.Data.Tests, Recordum.WPF.Tests (dettagli per task)
```

---

### Task 1: Core — grammatica senza `RecordingMode` + `ReferenceCodeSlug`

**Files:**
- Modify: `src/Recordum.Core/Composition/HabitComposition.cs`
- Modify: `src/Recordum.Core/Composition/HabitCompositionRules.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs` (call site `Snapshot()`, proprietà `_mode`)
- Create: `src/Recordum.Core/Formatting/ReferenceCodeSlug.cs`
- Test: `tests/Recordum.Core.Tests/Composition/HabitCompositionRulesTests.cs` (esistente, da aggiornare), `tests/Recordum.Core.Tests/Formatting/ReferenceCodeSlugTests.cs` (nuovo)

**Interfaces:**
- Consumes: niente (primo task).
- Produces: `HabitComposition(IReadOnlyList<FieldNature> Fields, bool CountsOccurrences, bool Abstinence)` (senza `Mode`); `ReferenceCodeSlug.From(string name, IReadOnlyCollection<string> takenCodes) → string`. I task 2–10 usano queste firme.

- [ ] **Step 1: censire i chiamanti di `RecordingMode`**

Run: `grep -rn "RecordingMode" src tests --include=*.cs`
Attesi: `HabitComposition`, `HabitCompositionRules.CanSetMode`, `HabitBuilderViewModel` (`_mode`, `Snapshot`, `OnModeChanged`), eventuali test Core. Se emergono altri consumer di produzione (quick-add, editor), NON cancellare l'enum: rimuoverlo solo dalla grammatica e annotare nel commit. Se i soli chiamanti sono grammatica+builder+test, l'enum `RecordingMode.cs` si cancella.

- [ ] **Step 2: aggiornare i test della grammatica (rossi)**

In `HabitCompositionRulesTests.cs`: togliere l'argomento `RecordingMode` da ogni costruzione di `HabitComposition`, cancellare i test di `CanSetMode`. Aggiungere il test-guardia che il record non ha più la proprietà:

```csharp
[Fact]
public void Composition_has_no_recording_mode()
{
    typeof(HabitComposition).GetProperty("Mode").Should().BeNull(
        "il modo di registrazione è uscito dalla grammatica (Onda 2.5, decisione 4: il mattone Sessione lo sostituisce)");
}
```

- [ ] **Step 3: run per vedere il rosso** — `dotnet test tests/Recordum.Core.Tests -p:UseAppHost=false --filter "FullyQualifiedName~HabitCompositionRules"` → FAIL (compile error sui costruttori).

- [ ] **Step 4: implementare** — nuovo record:

```csharp
public sealed record HabitComposition(
    IReadOnlyList<FieldNature> Fields,
    bool CountsOccurrences,
    bool Abstinence)
{
    public static HabitComposition Empty { get; } =
        new(Array.Empty<FieldNature>(), CountsOccurrences: false, Abstinence: false);
}
```

Cancellare `CanSetMode` dalle rules (ResolveType/CanAddField/CanEnableCounter/CanEnableAbstinence invariati). In `HabitBuilderViewModel`: rimuovere `[ObservableProperty] private RecordingMode _mode`, `OnModeChanged`, l'argomento in `Snapshot()`; il commento della classe non deve più menzionare "modo".

- [ ] **Step 5: test `ReferenceCodeSlug` (rosso)**

```csharp
public class ReferenceCodeSlugTests
{
    [Theory]
    [InlineData("Litri d'acqua", "litri-d-acqua")]
    [InlineData("  Café  crème ", "caf-cr-me")]   // non-ASCII fuori: [a-z0-9-] soltanto
    [InlineData("!!!", "custom")]                  // tutto illegale → fallback
    public void From_slugifies(string name, string expected) =>
        ReferenceCodeSlug.From(name, []).Should().Be(expected);

    [Fact]
    public void From_suffixes_on_collision_case_insensitive()
    {
        ReferenceCodeSlug.From("Litri", ["litri"]).Should().Be("litri-2");
        ReferenceCodeSlug.From("Litri", ["LITRI", "litri-2"]).Should().Be("litri-3");
    }
}
```

- [ ] **Step 6: implementare `ReferenceCodeSlug`**

```csharp
namespace Recordum.Core.Formatting;

/// <summary>Genera il Code di un'unità/scala custom dal nome (Onda 2.5). Il codice è la chiave di
/// join dei payload template (risolvi-o-crea): minuscolo, [a-z0-9-], mai vuoto, unico rispetto a
/// <paramref name="takenCodes"/> (confronto case-insensitive) con suffisso -2, -3, …</summary>
public static class ReferenceCodeSlug
{
    public static string From(string name, IReadOnlyCollection<string> takenCodes)
    {
        ArgumentNullException.ThrowIfNull(takenCodes);
        var chars = (name ?? string.Empty).Trim().ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-');
        var slug = string.Join(null, chars);
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        if (slug.Length == 0) slug = "custom";
        var taken = new HashSet<string>(takenCodes, StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(slug)) return slug;
        for (var i = 2; ; i++)
            if (!taken.Contains($"{slug}-{i}")) return $"{slug}-{i}";
    }
}
```

- [ ] **Step 7: suite Core + WPF compile** — `dotnet test tests/Recordum.Core.Tests -p:UseAppHost=false` PASS; `dotnet build src/Recordum.WPF -p:UseAppHost=false` verde.

- [ ] **Step 8: Commit** — `git add -A && git commit -m "feat(core): grammatica senza RecordingMode + ReferenceCodeSlug (2.5 T1)"`

---

### Task 2: Core/Data — `ComposedHabitRequest` v2 + materializzazione atomica completa

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/ComposedHabitRequest.cs`
- Create: `src/Recordum.Data/Services/HabitUiPrefsJson.cs`
- Modify: `src/Recordum.Data/Services/HabitUiPrefsService.cs` (usa il serializer condiviso)
- Modify: `src/Recordum.Data/Services/ComposedHabitService.cs`
- Test: `tests/Recordum.Data.Tests/Integration/ComposedHabitServiceTests.cs` (esistente o nuovo, pattern `DbFixture`)

**Interfaces:**
- Consumes: nulla di nuovo (Task 1 non tocca queste firme).
- Produces (per Task 3–7 e 10):

```csharp
public sealed record ComposedHabitRequest(
    HabitNode Node,
    HabitConfig? Config,
    IReadOnlyList<MetricDefinition> Metrics,
    IReadOnlyList<SegmentDefinition>? Segments = null,
    Goal? Goal = null,
    IReadOnlyList<UnitOfMeasure>? NewUnits = null,
    IReadOnlyList<ScaleTemplate>? NewScales = null,
    HabitUiPrefs? UiPrefs = null);
```

e `HabitUiPrefsJson.Serialize(HabitUiPrefs)` / `HabitUiPrefsJson.Deserialize(string?)` (static, in `Recordum.Data.Services`, con le **stesse** `JsonSerializerOptions` oggi private in `HabitUiPrefsService` — spostarle, non duplicarle).

**Semantica**: i mattoni staged arrivano con `Guid` **pre-assegnati** dal builder; le `MetricDefinition` li referenziano direttamente via `UnitOfMeasureId`/`ScaleTemplateId` — il servizio li `Add`a prima delle metriche, nessuna risoluzione per codice in questo percorso (quella vive solo in `InstantiateAsync`, Task 8). `UiPrefs` si serializza su `request.Node.UiPrefsJson` **prima** dell'`Add` — dentro la transazione, niente scrittura post-hoc (chiude W24-R1). Il `Goal` arriva già con `Name` = nome habit (CR9: mai literal) e `MetricDefinitionId` della prima metrica (o null per tap counter). Per una metrica su scala staged, il builder snapshotta `LabelsJson` della scala sulla metrica (mirror di `HabitTemplateService.MaterializeMetricsSegmentsGoalAsync`).

- [ ] **Step 1: test (rossi)**

```csharp
[Fact]
public async Task CreateComposedHabitAsync_materializes_everything_in_one_transaction()
{
    await using var fx = new DbFixture();
    var svc = new ComposedHabitService(fx.Factory);
    var nodeId = Guid.NewGuid();
    var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Code = "tazze", Name = "Tazze", IsOfficial = false };
    var scale = new ScaleTemplate { Id = Guid.NewGuid(), Code = "energia-3", Name = "Energia",
        MinValue = 1, MaxValue = 3, LabelsJson = """["Bassa","Media","Alta"]""", IsOfficial = false };
    var metric = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = nodeId, Name = "Tazze",
        Type = MetricType.Decimal, UnitOfMeasureId = unit.Id, SortOrder = 0 };
    var request = new ComposedHabitRequest(
        Node: new HabitNode { Id = nodeId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "Tè", Color = "#123456" },
        Config: new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = nodeId, Repeatable = true,
            ScheduleType = ScheduleType.SpecificDays, ScheduleDaysJson = """["Mon","Fri"]""",
            StreakMode = StreakMode.Weekly, StreakRecovery = StreakRecovery.None,
            SeriesLabelsJson = """["Mattina","Sera"]""" },
        Metrics: [metric],
        Segments: [new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = nodeId, Name = "Work", IsRest = false, SortOrder = 0 }],
        Goal: new Goal { Id = Guid.NewGuid(), HabitNodeId = nodeId, Name = "Tè",
            MetricDefinitionId = metric.Id, TargetValue = 3, PeriodType = GoalPeriodType.Open },
        NewUnits: [unit],
        NewScales: [scale],
        UiPrefs: new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });

    var id = await svc.CreateComposedHabitAsync(request);

    await using var db = await fx.Factory.CreateDbContextAsync();
    (await db.UnitsOfMeasure.AnyAsync(u => u.Code == "tazze")).Should().BeTrue();
    (await db.ScaleTemplates.AnyAsync(s => s.Code == "energia-3")).Should().BeTrue();
    (await db.SegmentDefinitions.CountAsync(s => s.HabitNodeId == id)).Should().Be(1);
    (await db.Goals.SingleAsync(g => g.HabitNodeId == id)).TargetValue.Should().Be(3);
    var node = await db.HabitNodes.SingleAsync(n => n.Id == id);
    HabitUiPrefsJson.Deserialize(node.UiPrefsJson).JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
}

[Fact]
public async Task CreateComposedHabitAsync_with_minimal_request_behaves_as_before()
{
    // Compat: i 3 argomenti storici bastano (default null sui nuovi) — il call site legacy compila e crea solo nodo+config+metriche.
    await using var fx = new DbFixture();
    var svc = new ComposedHabitService(fx.Factory);
    var nodeId = Guid.NewGuid();
    var id = await svc.CreateComposedHabitAsync(new ComposedHabitRequest(
        new HabitNode { Id = nodeId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "A", Color = "#000000" },
        new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = nodeId }, []));
    await using var db = await fx.Factory.CreateDbContextAsync();
    (await db.HabitNodes.AnyAsync(n => n.Id == id)).Should().BeTrue();
    (await db.Goals.AnyAsync(g => g.HabitNodeId == id)).Should().BeFalse();
}
```

(Adattare namespace/using al file esistente; se `ComposedHabitServiceTests.cs` non esiste, crearlo accanto a `HabitTemplateServiceTests.cs` con lo stesso pattern `DbFixture`.)

- [ ] **Step 2: run rosso** — `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter "FullyQualifiedName~ComposedHabitService"` → FAIL (record a 3 parametri, `HabitUiPrefsJson` inesistente).

- [ ] **Step 3: implementare**

`HabitUiPrefsJson.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

/// <summary>Contratto JSON di ui_prefs_json (camelCase, enum come stringhe, null omessi —
/// docs/ARCHITECTURE.md). Unico punto: HabitUiPrefsService e ComposedHabitService DEVONO
/// condividere queste options, o il round-trip diverge.</summary>
internal static class HabitUiPrefsJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static string Serialize(HabitUiPrefs prefs) => JsonSerializer.Serialize(prefs, Options);

    internal static HabitUiPrefs Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new HabitUiPrefs()
            : JsonSerializer.Deserialize<HabitUiPrefs>(json, Options) ?? new HabitUiPrefs();
}
```

In `HabitUiPrefsService`: cancellare le options private e ogni `JsonSerializer.(De)Serialize` diretto → chiamare `HabitUiPrefsJson`. (Se il test lo vuole visibile: `internal` va bene se `Recordum.Data` ha `InternalsVisibleTo` per i test — verificarlo; altrimenti renderlo `public static`.)

`ComposedHabitService.CreateComposedHabitAsync`:

```csharp
await using var db = await dbFactory.CreateDbContextAsync(ct);
await using var tx = await db.Database.BeginTransactionAsync(ct);

foreach (var u in request.NewUnits ?? []) db.UnitsOfMeasure.Add(u);
foreach (var s in request.NewScales ?? []) db.ScaleTemplates.Add(s);
if (request.UiPrefs is { } prefs) request.Node.UiPrefsJson = HabitUiPrefsJson.Serialize(prefs);
db.HabitNodes.Add(request.Node);
if (request.Config is not null) db.HabitConfigs.Add(request.Config);
foreach (var m in request.Metrics) db.MetricDefinitions.Add(m);
foreach (var seg in request.Segments ?? []) db.SegmentDefinitions.Add(seg);
if (request.Goal is not null) db.Goals.Add(request.Goal);

await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
return request.Node.Id;
```

- [ ] **Step 4: run verde** — stesso filtro, PASS; poi l'intera `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false` (nessuna regressione: `HabitUiPrefsServiceTests` deve restare verde col serializer condiviso).

- [ ] **Step 5: Commit** — `git commit -am "feat(data): ComposedHabitRequest v2 — materializzazione atomica completa, ui_prefs nella transazione (2.5 T2)"`

---

### Task 3: WPF — mattone Programma

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/ScheduleBrickViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs` (proprietà `Schedule`, `BuildConfig`)
- Modify: `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml` (zona "Composizione avanzata" sotto la field list, sopra l'anteprima)
- Modify (via shell per il Designer): `Strings.resx` / `Strings.it.resx` / `Strings.Designer.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/ScheduleBrickViewModelTests.cs` (nuovo) + `HabitBuilderViewModelTests.cs` (esistente)

**Interfaces:**
- Consumes: `HabitConfig` (proprietà `ScheduleType`, `ScheduleDaysJson`, `ScheduleWeeklyTarget`, `ScheduleEveryXDays`, `StreakMode`, `StreakRecovery`), enum `ScheduleType { Daily, SpecificDays, WeeklyX, EveryXDays, Monthly, Custom }`, `StreakMode { Daily, Weekly, Occurrences, Both }`, `StreakRecovery { None, GraceDay, Manual, Retroactive }`.
- Produces: `ScheduleBrickViewModel` con `ScheduleType ScheduleType`, `ObservableCollection<ScheduleDayToggle> Days` (7 voci, `DayKey` ∈ Mon…Sun, `IsSelected`), `int? WeeklyTarget`, `int? EveryXDays`, `StreakMode StreakMode`, `StreakRecovery StreakRecovery`, `bool IsExpanded`, `string Summary`, `event EventHandler? Changed`, `void ApplyTo(HabitConfig config)`. Il Task 8 (payload) e il Task 10 (fixture) leggono queste proprietà.

**Scope del mattone**: `Daily` (default), `SpecificDays` (7 toggle → `ScheduleDaysJson` = `["Mon","Fri"]`, **stesso formato di `SchedulingSectionViewModel`**: array JSON di `DayKey`, null se nessun giorno), `WeeklyX` (target numerico), `EveryXDays` (numerico). `Monthly`/`Custom` restano a Configura (nessun seed li usa; documentato nel riassunto del mattone). Default streak: `Daily` + `GraceDay` (la forma dei seed).

- [ ] **Step 1: test (rossi)**

```csharp
public class ScheduleBrickViewModelTests
{
    [Fact]
    public void Defaults_match_seed_shape()
    {
        var vm = new ScheduleBrickViewModel();
        vm.ScheduleType.Should().Be(ScheduleType.Daily);
        vm.StreakMode.Should().Be(StreakMode.Daily);
        vm.StreakRecovery.Should().Be(StreakRecovery.GraceDay);
        vm.IsExpanded.Should().BeFalse();
        vm.Days.Should().HaveCount(7);
    }

    [Fact]
    public void ApplyTo_writes_specific_days_in_scheduling_section_format()
    {
        var vm = new ScheduleBrickViewModel { ScheduleType = ScheduleType.SpecificDays };
        vm.Days.Single(d => d.DayKey == "Mon").IsSelected = true;
        vm.Days.Single(d => d.DayKey == "Fri").IsSelected = true;
        var cfg = new HabitConfig();
        vm.ApplyTo(cfg);
        cfg.ScheduleType.Should().Be(ScheduleType.SpecificDays);
        cfg.ScheduleDaysJson.Should().Be("""["Mon","Fri"]""");
    }

    [Fact]
    public void ApplyTo_clears_fields_of_other_schedule_types()
    {
        var vm = new ScheduleBrickViewModel { ScheduleType = ScheduleType.WeeklyX, WeeklyTarget = 3 };
        var cfg = new HabitConfig { ScheduleDaysJson = "[\"Mon\"]", ScheduleEveryXDays = 2 };
        vm.ApplyTo(cfg);
        cfg.ScheduleWeeklyTarget.Should().Be(3);
        cfg.ScheduleDaysJson.Should().BeNull();
        cfg.ScheduleEveryXDays.Should().BeNull();
    }

    [Fact]
    public void Any_change_raises_Changed_and_updates_Summary()
    {
        var vm = new ScheduleBrickViewModel();
        var raised = 0;
        vm.Changed += (_, _) => raised++;
        var before = vm.Summary;
        vm.StreakRecovery = StreakRecovery.None;
        raised.Should().BeGreaterThan(0);
        vm.Summary.Should().NotBe(before);
    }
}
```

- [ ] **Step 2: run rosso** — `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter "FullyQualifiedName~ScheduleBrick"` → FAIL.

- [ ] **Step 3: implementare il VM**

```csharp
public sealed partial class ScheduleDayToggle : ObservableObject
{
    public required string DayKey { get; init; }          // "Mon".."Sun" — contratto ScheduleDaysJson
    public required string Label { get; init; }           // localizzata (Strings.Day_Mon…)
    [ObservableProperty] private bool _isSelected;
}

/// <summary>Mattone Programma (Onda 2.5 §2.1): schedule + streak + recovery del draft. Vista sul
/// draft, non chiama servizi (spec §1: host-agnostico). Collassato mostra Summary.</summary>
public sealed partial class ScheduleBrickViewModel : ObservableObject
{
    [ObservableProperty] private ScheduleType _scheduleType = ScheduleType.Daily;
    [ObservableProperty] private int? _weeklyTarget;
    [ObservableProperty] private int? _everyXDays;
    [ObservableProperty] private StreakMode _streakMode = StreakMode.Daily;
    [ObservableProperty] private StreakRecovery _streakRecovery = StreakRecovery.GraceDay;
    [ObservableProperty] private bool _isExpanded;

    public ObservableCollection<ScheduleDayToggle> Days { get; }
    public event EventHandler? Changed;
    public string Summary => BuildSummary();  // "Ogni giorno · recupero grace day" ecc.

    public ScheduleBrickViewModel()
    {
        Days = new(new[] { ("Mon", Strings.Day_Mon), ("Tue", Strings.Day_Tue), ("Wed", Strings.Day_Wed),
            ("Thu", Strings.Day_Thu), ("Fri", Strings.Day_Fri), ("Sat", Strings.Day_Sat), ("Sun", Strings.Day_Sun) }
            .Select(d => new ScheduleDayToggle { DayKey = d.Item1, Label = d.Item2 }));
        foreach (var d in Days) d.PropertyChanged += (_, _) => Raise();
    }

    partial void OnScheduleTypeChanged(ScheduleType value) => Raise();
    partial void OnWeeklyTargetChanged(int? value) => Raise();
    partial void OnEveryXDaysChanged(int? value) => Raise();
    partial void OnStreakModeChanged(StreakMode value) => Raise();
    partial void OnStreakRecoveryChanged(StreakRecovery value) => Raise();

    private void Raise() { OnPropertyChanged(nameof(Summary)); Changed?.Invoke(this, EventArgs.Empty); }

    public void ApplyTo(HabitConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.ScheduleType = ScheduleType;
        var selected = Days.Where(d => d.IsSelected).Select(d => d.DayKey).ToArray();
        config.ScheduleDaysJson = ScheduleType == ScheduleType.SpecificDays && selected.Length > 0
            ? JsonSerializer.Serialize(selected) : null;
        config.ScheduleWeeklyTarget = ScheduleType == ScheduleType.WeeklyX ? WeeklyTarget : null;
        config.ScheduleEveryXDays = ScheduleType == ScheduleType.EveryXDays ? EveryXDays : null;
        config.StreakMode = StreakMode;
        config.StreakRecovery = StreakRecovery;
    }
}
```

`BuildSummary()` concatena pezzi localizzati: tipo programma (`Strings.HabitBuilder_Schedule_Daily` / `_SpecificDays` con conteggio giorni / `_WeeklyX` con target / `_EveryXDays`), più recovery se ≠ `GraceDay` e streak mode se ≠ `Daily` (il default non fa rumore).

- [ ] **Step 4: integrare nell'host** — in `HabitBuilderViewModel`: `public ScheduleBrickViewModel Schedule { get; } = new();` nel costruttore `Schedule.Changed += (_, _) => TemplateSaved = false;`. `BuildConfig` diventa:

```csharp
private HabitConfig BuildConfig(Guid nodeId)
{
    var cfg = new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = nodeId, Repeatable = Repeatable };
    Schedule.ApplyTo(cfg);
    return cfg;
}
```

Test host (in `HabitBuilderViewModelTests`): `BuildRequestForTest()` con `Schedule.ScheduleType = SpecificDays` + 2 giorni → `request.Config.ScheduleDaysJson == "[\"Mon\",\"Fri\"]"`.

- [ ] **Step 5: stringhe** — chiavi nuove (EN/IT): `HabitBuilder_Advanced` ("Composition details"/"Dettagli della composizione"), `HabitBuilder_Schedule_Header` ("Schedule & streak"/"Programma e streak"), `HabitBuilder_Schedule_Daily` ("Every day"/"Ogni giorno"), `HabitBuilder_Schedule_SpecificDays` ("Specific days"/"Giorni specifici"), `HabitBuilder_Schedule_WeeklyX` ("Times per week"/"Volte a settimana"), `HabitBuilder_Schedule_EveryXDays` ("Every X days"/"Ogni X giorni"), `HabitBuilder_Schedule_StreakMode` ("Streak"/"Streak"), `HabitBuilder_Schedule_Recovery` ("Recovery"/"Recupero"), `Day_Mon`…`Day_Sun` (se non esistono già — **prima grep**: `grep -n "Day_Mon" src/Recordum.WPF/Resources/Strings.Designer.cs`; il day-picker di Configura potrebbe già averle: in quel caso riusarle e non crearne di nuove). Editare le due `.resx` con Edit; il `Designer.cs` **via Bash** (blocco `python` che inserisce le proprietà in ordine alfabetico accanto alle esistenti). Poi `dotnet build src/Recordum.WPF -p:UseAppHost=false`.

- [ ] **Step 6: XAML** — dopo la field list (riga `</ItemsControl>` della Zone 2), nuova zona:

```xml
<!-- ===== Zone 2b: Composizione avanzata (Onda 2.5) ===== -->
<TextBlock Text="{x:Static res:Strings.HabitBuilder_Advanced}" FontSize="{StaticResource FontSize.Body}"
           FontWeight="SemiBold" Foreground="{DynamicResource Brush.Text}" Margin="0,4,0,6"/>
<Expander IsExpanded="{Binding Schedule.IsExpanded}" Margin="0,0,0,8">
    <Expander.Header>
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="{x:Static res:Strings.HabitBuilder_Schedule_Header}" FontWeight="SemiBold"/>
            <TextBlock Text="{Binding Schedule.Summary}" Margin="8,0,0,0"
                       Foreground="{DynamicResource Brush.TextMuted}"/>
        </StackPanel>
    </Expander.Header>
    <StackPanel Margin="12,6,0,4">
        <!-- tipo programma: ComboBox su enum con ItemTemplate localizzato; day-toggles visibili solo per SpecificDays -->
        <!-- StreakMode / StreakRecovery: due ComboBox localizzate, stesso pattern -->
    </StackPanel>
</Expander>
```

Il corpo dell'Expander usa ComboBox + `ItemsControl` orizzontale di `ToggleButton` per `Days` (visibilità con `DataTrigger` su `Schedule.ScheduleType`, pattern identico ai picker per-natura già nel file). Per le voci enum localizzate seguire il pattern usato da `SchedulingSectionView` (F0-11: mai enum grezzi a schermo — guardare come lo risolve quella view e replicare).

- [ ] **Step 7: run verde + suite WPF** — filtro `ScheduleBrick` PASS, poi suite completa WPF (serializzata se serve).

- [ ] **Step 8: Commit** — `git commit -am "feat(builder): mattone Programma — schedule/streak/recovery nel draft (2.5 T3)"`

---

### Task 4: WPF — mattone Obiettivo

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/GoalBrickViewModel.cs`
- Modify: `HabitBuilderViewModel.cs` (proprietà `GoalBrick`, `BuildRequest`, `Recompute`)
- Modify: `HabitBuilderView.xaml` (sotto l'Expander Programma)
- Modify (shell): stringhe
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/GoalBrickViewModelTests.cs` + host test

**Interfaces:**
- Consumes: `ComposedHabitRequest.Goal` (Task 2), `Goal { HabitNodeId, Name, MetricDefinitionId, TargetValue, PeriodType }`, `GoalPeriodType { Fixed, Rolling, Open }`.
- Produces: `GoalBrickViewModel` con `bool HasGoal`, `decimal? Target`, `GoalPeriodType PeriodType` (default `Open`), `bool IsAvailable`, `event EventHandler? Changed`, `Goal? Build(Guid nodeId, string habitName, Guid? firstMetricId)`.

**Regole**: `IsAvailable` = il tipo derivato è `Counter` (metrica numerica o tap counter — il goal di Water intake è il precedente). `Build` torna `null` se `!HasGoal || Target is null or <= 0`; il gate `CanLaunch` si estende: `HasGoal` con target mancante/≤0 blocca (con guard anche dentro `LaunchAsync`/`SaveAsTemplateAsync`, già presente per R7 — il `CanLaunch` esteso copre entrambi perché entrambi lo consultano dentro il corpo... verificare che `LaunchAsync` abbia il guard interno come `SaveAsTemplateAsync`; se manca, aggiungerlo in questo task).

- [ ] **Step 1: test (rossi)** — casi: default `HasGoal=false` → `Build` null; `HasGoal` + `Target=2` + `PeriodType=Open` → `Goal` con `Name` = nome passato, `TargetValue=2`, `MetricDefinitionId` = quello passato; `HasGoal` + target null → `CanLaunch` dell'host false (test sull'host con un campo Quantità + goal incompleto); tap counter (`CountsOccurrences=true`) → `IsAvailable` true; Action pura → false e `HasGoal` forzato false da `Recompute` (il mattone si spegne se la composizione smette di supportarlo — mai un goal orfano nel draft).

```csharp
[Fact]
public void Build_returns_goal_bound_to_habit_name_and_first_metric()
{
    var vm = new GoalBrickViewModel { HasGoal = true, Target = 2m, PeriodType = GoalPeriodType.Open };
    var metricId = Guid.NewGuid();
    var nodeId = Guid.NewGuid();
    var goal = vm.Build(nodeId, "Acqua", metricId);
    goal.Should().NotBeNull();
    goal!.Name.Should().Be("Acqua");           // CR9: mai literal hardcoded
    goal.HabitNodeId.Should().Be(nodeId);
    goal.MetricDefinitionId.Should().Be(metricId);
    goal.TargetValue.Should().Be(2m);
}
```

- [ ] **Step 2: run rosso** — filtro `GoalBrick` FAIL.

- [ ] **Step 3: implementare** — VM `ObservableObject` col pattern di Task 3 (ogni `On…Changed` → `Changed`). In `HabitBuilderViewModel`: `GoalBrick.IsAvailable` ricalcolato in `Recompute()` da `DerivedType == HabitType.Counter`; quando diventa false → `GoalBrick.HasGoal = false`. `BuildRequest`: passare `Goal: GoalBrick.Build(nodeId, Name.Trim(), metrics.FirstOrDefault()?.Id)`. `CanLaunch += && (!GoalBrick.HasGoal || GoalBrick.Target is > 0)`.

- [ ] **Step 4: XAML** — chip "+ Obiettivo" (ToggleButton su `GoalBrick.HasGoal`, visibile solo se `IsAvailable`) che espande: campo target (`TextBox` numerica, stesso stile campi esistenti) + ComboBox periodo localizzata. Stringhe: `HabitBuilder_Goal_Add` ("+ Goal"/"+ Obiettivo"), `HabitBuilder_Goal_Target` ("Target"/"Obiettivo"), `HabitBuilder_Goal_Period_Open` ("Open"/"Aperto"), `_Period_Fixed` ("Per period"/"A periodo"), `_Period_Rolling` ("Rolling"/"Mobile") — 3 edit come sempre.

- [ ] **Step 5: run verde + suite** e **Commit** — `git commit -am "feat(builder): mattone Obiettivo (2.5 T4)"`

---

### Task 5: WPF — mattone Sessione

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/SessionBrickViewModel.cs`
- Modify: `HabitBuilderViewModel.cs` (`SessionBrick`, `BuildConfig`, `BuildRequest`, `Recompute`)
- Modify: `HabitBuilderView.xaml`
- Modify (shell): stringhe
- Test: `SessionBrickViewModelTests.cs` + host test

**Interfaces:**
- Consumes: `ComposedHabitRequest.Segments` (Task 2), `SegmentDefinition { HabitNodeId, Name, IsRest, SortOrder, Color?, DurationSeconds? }`, `HabitConfig { HasIntegratedTimer, TimerMode, SessionBaseTimer }`, `TimerMode.Session`.
- Produces: `SessionBrickViewModel` con `bool HasSession`, `ObservableCollection<SegmentRowViewModel> Segments` (`SegmentRowViewModel { string Name, bool IsRest }`, observable), `bool IsAvailable`, `event EventHandler? Changed`, `void ApplyTo(HabitConfig config)`, `IReadOnlyList<SegmentDefinition> BuildSegments(Guid nodeId)`, comandi `AddSegmentCommand`/`RemoveSegmentCommand`.

**Regole**: `IsAvailable` = `DerivedType == Counter && !CountsOccurrences` (la sessione richiede campi da registrare per segmento — Workout log è il precedente). Attivarlo semina i default `Work` (`IsRest=false`) e `Rest` (`IsRest=true`) — nomi da stringhe (`HabitBuilder_Segment_Work`/`_Rest`; il seed usa "Work"/"Rest" letterali: le stringhe EN devono essere esattamente "Work" e "Rest" perché il guard di parità del Task 10 confronta i nomi col seed). `ApplyTo`: `HasSession` → `HasIntegratedTimer=true; TimerMode=TimerMode.Session; SessionBaseTimer=true` (mirror del lettore, `HabitTemplateService.cs:116`); spento → i tre campi restano ai default (false/null/false). `BuildSegments`: `SortOrder` = indice in lista; segmenti senza nome scartati. Se `IsAvailable` diventa false → `HasSession=false` (come il goal).

- [ ] **Step 1: test (rossi)**

```csharp
[Fact]
public void Enabling_session_seeds_work_rest_and_flags_config()
{
    var vm = new SessionBrickViewModel();
    vm.HasSession = true;
    vm.Segments.Select(s => (s.Name, s.IsRest)).Should()
        .Equal(("Work", false), ("Rest", true));
    var cfg = new HabitConfig();
    vm.ApplyTo(cfg);
    cfg.HasIntegratedTimer.Should().BeTrue();
    cfg.TimerMode.Should().Be(TimerMode.Session);
    cfg.SessionBaseTimer.Should().BeTrue();
}

[Fact]
public void BuildSegments_orders_by_list_position()
{
    var vm = new SessionBrickViewModel { HasSession = true };
    vm.Segments.Add(new SegmentRowViewModel { Name = "Cooldown", IsRest = true });
    var nodeId = Guid.NewGuid();
    var segs = vm.BuildSegments(nodeId);
    segs.Select(s => s.SortOrder).Should().Equal(0, 1, 2);
    segs.All(s => s.HabitNodeId == nodeId).Should().BeTrue();
}
```

- [ ] **Step 2–3: rosso → implementare** (pattern Task 3/4; `Segments` è bindata: mai `Clear()` a collection viva — per lo spegnimento del mattone svuotare **solo** togliendo le righe una a una o ricreando la collection intera e ri-notificando la proprietà, non `Clear()`+repopulate mentre è `ItemsSource`; la via semplice: la collection si svuota con `Clear()` SOLO se il pannello segmenti è nascosto prima (`HasSession=false` collassa la vista) — documentare la scelta nel codice).

- [ ] **Step 4: host + XAML** — `BuildConfig` chiama anche `SessionBrick.ApplyTo(cfg)`; `BuildRequest` passa `Segments: SessionBrick.HasSession ? SessionBrick.BuildSegments(nodeId) : null`. XAML: chip "+ Sessione" → lista righe (TextBox nome + CheckBox "riposo" + ✕) + bottone aggiungi. Stringhe: `HabitBuilder_Session_Add` ("+ Session"/"+ Sessione"), `HabitBuilder_Session_IsRest` ("Rest"/"Riposo"), `HabitBuilder_Session_AddSegment` ("+ Segment"/"+ Segmento"), `HabitBuilder_Segment_Work` ("Work"/"Work"), `HabitBuilder_Segment_Rest` ("Rest"/"Rest") — i due nomi-segmento restano identici in IT (nomi propri del seed, il guard T10 li confronta).

- [ ] **Step 5: run verde + suite, Commit** — `git commit -am "feat(builder): mattone Sessione — timer integrato + segmenti (2.5 T5)"`

---

### Task 6: WPF — serie nominate nel mattone Ripetibile

**Files:**
- Modify: `HabitBuilderViewModel.cs` (collection `SeriesLabels`, `BuildConfig`)
- Modify: `HabitBuilderView.xaml` (editor chip sotto il CheckBox Ripetibile)
- Modify (shell): stringhe
- Test: `HabitBuilderViewModelTests.cs`

**Interfaces:**
- Consumes: `HabitConfig.SeriesLabelsJson` (formato: array JSON di stringhe, es. `["Night","Nap"]` — lo stesso che `InstantiateAsync` serializza da `payload.Config.SeriesLabels`).
- Produces: `ObservableCollection<SeriesLabelRowViewModel> SeriesLabels` sull'host (`SeriesLabelRowViewModel { string Name }` observable), comandi `AddSeriesLabelCommand`/`RemoveSeriesLabelCommand`.

- [ ] **Step 1: test (rossi)** — con `Repeatable=true` e labels `["Notte","Riposino"]` → `BuildRequestForTest().Config.SeriesLabelsJson == "[\"Notte\",\"Riposino\"]"`; senza labels → null; con `Repeatable=false` → null anche se la lista non è vuota (le labels viaggiano solo col flag); ogni mutazione labels → `TemplateSaved=false`.

- [ ] **Step 2–3: rosso → implementare** — in `BuildConfig`:

```csharp
var labels = SeriesLabels.Select(l => l.Name.Trim()).Where(n => n.Length > 0).ToArray();
cfg.SeriesLabelsJson = Repeatable && labels.Length > 0 ? JsonSerializer.Serialize(labels) : null;
```

- [ ] **Step 4: XAML** — sotto il CheckBox Ripetibile, visibile solo con `Repeatable` true: `ItemsControl` orizzontale di chip (TextBox inline + ✕) + "+ Aggiungi". Stringhe: `HabitBuilder_SeriesLabels_Hint` ("Name your entries (optional): Night, Nap…"/"Dai un nome alle registrazioni (opzionale): Notte, Riposino…"), `HabitBuilder_SeriesLabels_Add` ("+ Add"/"+ Aggiungi").

- [ ] **Step 5: run verde + suite, Commit** — `git commit -am "feat(builder): serie nominate nel mattone Ripetibile (2.5 T6)"`

---

### Task 7: WPF — unità e scale custom staged nei picker

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/StagedReferenceData.cs` (righe staged + micro-form VM)
- Modify: `HabitBuilderViewModel.cs` (comandi `AddCustomUnitCommand`/`AddCustomScaleCommand`, staging, `BuildRequest`)
- Modify: `HabitBuilderView.xaml` (bottone "+" accanto ai picker Unità/Scala + Popup micro-form)
- Modify (shell): stringhe
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/StagedReferenceDataTests.cs` + host test

**Interfaces:**
- Consumes: `ReferenceCodeSlug.From` (Task 1); `ComposedHabitRequest.NewUnits/NewScales` (Task 2); `UnitOfMeasure { Id, Code, Name, Symbol?, IsOfficial }`, `ScaleTemplate { Id, Code, Name, MinValue, MaxValue, LabelsJson, IsOfficial }`.
- Produces (per Task 8 e 10): sull'host `IReadOnlyList<UnitOfMeasure> StagedUnits`, `IReadOnlyList<ScaleTemplate> StagedScales` (Guid pre-assegnati, `IsOfficial=false`); `NewUnitFormViewModel { string Name, string? Symbol, bool CanConfirm }`, `NewScaleFormViewModel { string Name, string LabelsText, bool CanConfirm }` (labels separate da virgola, `CanConfirm` richiede nome non vuoto e ≥2 labels).

**Regole**: alla conferma del micro-form l'unità/scala si **aggiunge** (`Add`) ad `AvailableUnits`/`AvailableScales` (mai `Clear()`: le liste sono ItemsSource vive — global constraint) e alle liste staged; il campo che ha aperto il form riceve subito l'`Id` staged (`UnitOfMeasureId`/`ScaleTemplateId`). Codice = `ReferenceCodeSlug.From(name, codici già in AvailableUnits/AvailableScales + staged)`. Scala: `MinValue=1`, `MaxValue=labels.Count`, `LabelsJson` = array JSON delle labels. Una metrica su scala staged snapshotta `LabelsJson` sulla `MetricDefinition` in `BuildRequest` (il quick-add State legge da lì). L'unità staged NON entra in `AvailableTimeUnits` (le unità tempo sono chiuse in Core, `SecondsDisplay.TimeUnitCodes`). Se l'utente rimuove il campo che usava la staged, la definizione resta nel draft ed è innocua: viaggia nel request/payload **solo se referenziata da almeno una metrica** (filtro in `BuildRequest`/`BuildTemplatePayloadJson`).

- [ ] **Step 1: test (rossi)**

```csharp
[Fact]
public void Confirming_unit_form_stages_and_selects()
{
    var vm = BuildHost();                       // helper esistente dei test host col set di fake
    vm.AddFieldCommand.Execute(FieldNature.Quantity);
    var field = vm.Fields.Single();
    vm.BeginAddCustomUnit(field);
    vm.UnitForm!.Name = "Tazze";
    vm.ConfirmCustomUnitCommand.Execute(null);
    var staged = vm.StagedUnits.Single();
    staged.Code.Should().Be("tazze");
    staged.IsOfficial.Should().BeFalse();
    field.UnitOfMeasureId.Should().Be(staged.Id);
    vm.AvailableUnits.Should().Contain(staged);
}

[Fact]
public void Unreferenced_staged_definitions_do_not_reach_the_request()
{
    var vm = BuildHost();
    vm.AddFieldCommand.Execute(FieldNature.Quantity);
    var field = vm.Fields.Single();
    vm.BeginAddCustomUnit(field);
    vm.UnitForm!.Name = "Tazze";
    vm.ConfirmCustomUnitCommand.Execute(null);
    vm.RemoveFieldCommand.Execute(field);       // il campo sparisce, la staged resta nel draft
    vm.AddFieldCommand.Execute(FieldNature.Quantity); // nuovo campo senza unità
    var request = vm.BuildRequestForTest();
    request.NewUnits.Should().BeNullOrEmpty();
}

[Fact]
public void Scale_form_requires_two_labels()
{
    var form = new NewScaleFormViewModel();
    form.Name = "Energia";
    form.LabelsText = "Bassa";
    form.CanConfirm.Should().BeFalse();
    form.LabelsText = "Bassa, Alta";
    form.CanConfirm.Should().BeTrue();
}

[Fact]
public void Metric_on_staged_scale_snapshots_labels()
{
    var vm = BuildHost();
    vm.AddFieldCommand.Execute(FieldNature.Scale);
    var field = vm.Fields.Single();
    vm.BeginAddCustomScale(field);
    vm.ScaleForm!.Name = "Energia";
    vm.ScaleForm.LabelsText = "Bassa, Media, Alta";
    vm.ConfirmCustomScaleCommand.Execute(null);
    var request = vm.BuildRequestForTest();
    request.Metrics.Single().LabelsJson.Should().Be("""["Bassa","Media","Alta"]""");
    request.NewScales.Single().MaxValue.Should().Be(3);
}
```

- [ ] **Step 2–3: rosso → implementare** — `BeginAddCustomUnit/Scale(field)` istanziano il form VM e memorizzano il campo target; `ConfirmCustom…` validano (`CanConfirm`, con guard dentro il metodo — R7), creano l'entità con `Guid.NewGuid()`, `Add` alle liste, assegnano al campo, chiudono il form (proprietà null). `BuildRequest`: `NewUnits` = staged con `Id ∈ Metrics.UnitOfMeasureId`, `NewScales` = staged con `Id ∈ Metrics.ScaleTemplateId`.

- [ ] **Step 4: XAML** — accanto a ciascun picker (Quantità e Scala, NON Tempo) un Button "+" che apre un `Popup` col micro-form (TextBox nome, TextBox simbolo/labels, Conferma/Annulla). Stile popup: famiglia flottante esistente (raggio 8 + ombra, come ComboBox/menu). Stringhe: `HabitBuilder_NewUnit` ("+ New unit…"/"+ Nuova unità…"), `HabitBuilder_NewUnit_Name` ("Name"/"Nome"), `HabitBuilder_NewUnit_Symbol` ("Symbol (optional)"/"Simbolo (opzionale)"), `HabitBuilder_NewScale` ("+ New scale…"/"+ Nuova scala…"), `HabitBuilder_NewScale_Labels` ("Steps, comma-separated"/"Gradini, separati da virgola"), `HabitBuilder_New_Confirm` ("Add"/"Aggiungi"), `HabitBuilder_New_Cancel` ("Cancel"/"Annulla"), `HabitBuilder_StagedBadge` ("new"/"nuova").

- [ ] **Step 5: run verde + suite, Commit** — `git commit -am "feat(builder): unità e scale custom staged nei picker (2.5 T7)"`

---

### Task 8: Data+WPF — payload template v2 (scrittore) + risolvi-o-crea (lettore)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs` (`BuildTemplatePayloadJson` + classi payload private)
- Modify: `src/Recordum.Data/Services/HabitTemplateService.cs` (`TemplatePayload` + `MaterializeMetricsSegmentsGoalAsync`)
- Test: `tests/Recordum.Data.Tests/Integration/HabitTemplateServiceTests.cs`, `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs`

**Interfaces:**
- Consumes: tutto il draft dei Task 3–7.
- Produces: il payload v2, contratto per il Task 10. Forma (snake_case via options esistenti):

```jsonc
{
  "node":   { "name", "node_type", "habit_type", "color", "icon" },
  "config": { "schedule_type", "schedule_days_json", "schedule_weekly_target", "schedule_every_x_days",
              "streak_mode", "streak_recovery", "has_integrated_timer", "timer_mode",
              "repeatable", "series_labels" },
  "ui_prefs": { "journal_gesture" },
  "metrics":  [{ "name", "type", "unit", "scale_code" }],
  "segments": [{ "name", "is_rest", "sort_order" }],
  "goal":     { "target", "unit", "period_type" },
  "units":    [{ "code", "name", "symbol" }],          // SOLO definizioni staged referenziate
  "scales":   [{ "code", "name", "min_value", "max_value", "labels" }]
}
```

Regole scrittore: `config` sempre presente (i default `Daily/Daily/GraceDay` si scrivono espliciti — sono ciò che i seed dichiarano; il confronto semantico del Task 10 ci conta); `has_integrated_timer`/`timer_mode` solo con Sessione attiva (null altrimenti, `WhenWritingNull` li omette); `series_labels` = lista o assente; `goal.unit` = code dell'unità della prima metrica (o assente) — il lettore lo ignora, ma i seed lo portano e la parità semantica lo confronta; `units`/`scales` solo per staged referenziate; metriche su unità staged usano il **code staged** in `"unit"`.

Regole lettore: `TemplatePayload` guadagna `List<UnitDefPayload>? Units` e `List<ScaleDefPayload>? Scales`; in `MaterializeMetricsSegmentsGoalAsync`, dopo il load dei dizionari per code e **dentro la stessa transazione**: per ogni def con code non presente nel dizionario → `db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = Guid.NewGuid(), Code, Name, Symbol, IsOfficial = false })` (idem scala, con `LabelsJson` serializzato dalle labels) e inserimento nel dizionario. Code già esistente → si usa l'esistente (risolvi-o-crea). Payload senza `units`/`scales` → percorso identico a oggi (compat I5: nessun test esistente deve cambiare).

- [ ] **Step 1: test lettore (rossi)** in `HabitTemplateServiceTests`:

```csharp
[Fact]
public async Task InstantiateAsync_creates_missing_units_and_scales_from_embedded_definitions()
{
    await using var fx = new DbFixture();
    var svc = fx.CreateTemplateService();   // usare il factory-helper esistente nel file
    var tmplId = Guid.NewGuid();
    const string payload = """
        {"node":{"name":"Tè","node_type":"Habit","habit_type":"Counter","color":"#123456"},
         "config":{"schedule_type":"Daily","streak_mode":"Daily","streak_recovery":"GraceDay"},
         "metrics":[{"name":"Tazze","type":"Decimal","unit":"tazze"},
                    {"name":"Energia","type":"Integer","scale_code":"energia-3"}],
         "segments":[],
         "units":[{"code":"tazze","name":"Tazze"}],
         "scales":[{"code":"energia-3","name":"Energia","min_value":1,"max_value":3,"labels":["Bassa","Media","Alta"]}]}
        """;
    // seed del template via db diretto (pattern esistente nel file), poi:
    var id = await svc.InstantiateAsync(tmplId, null);
    await using var db = await fx.Factory.CreateDbContextAsync();
    var unit = await db.UnitsOfMeasure.SingleAsync(u => u.Code == "tazze");
    unit.IsOfficial.Should().BeFalse();
    var metric = await db.MetricDefinitions.SingleAsync(m => m.HabitNodeId == id && m.Name == "Tazze");
    metric.UnitOfMeasureId.Should().Be(unit.Id);
    var scaleMetric = await db.MetricDefinitions.SingleAsync(m => m.HabitNodeId == id && m.Name == "Energia");
    scaleMetric.LabelsJson.Should().Be("""["Bassa","Media","Alta"]""");
}

[Fact]
public async Task InstantiateAsync_reuses_an_existing_code_instead_of_duplicating()
{
    // payload con "units":[{"code":"l","name":"Duplicato"}] su un DB che ha già "l" seed:
    // il conteggio di UnitsOfMeasure con Code=="l" resta 1 e la metrica punta all'esistente.
}
```

- [ ] **Step 2: run rosso**, poi **implementare il lettore**.

- [ ] **Step 3: test scrittore (rossi)** in `HabitBuilderViewModelTests` — comporre nel VM: nome "Sonno", Ripetibile + labels `["Notte","Riposino"]`, Programma default, un campo Quantità con unità staged "tazze"; parsare `BuildTemplatePayloadJsonForTest()` (esporre il metodo come test seam `internal`, gemello di `BuildRequestForTest`) con `JsonDocument` e asserire: `config.schedule_type=="Daily"`, `config.streak_recovery=="GraceDay"`, `config.repeatable==true`, `config.series_labels==["Notte","Riposino"]`, `units[0].code=="tazze"`, `metrics[0].unit=="tazze"`. Secondo test: composizione con Sessione → `config.has_integrated_timer==true`, `config.timer_mode=="Session"`, `segments==[Work(0,false),Rest(1,true)]`. Terzo: goal → `goal.target`, `goal.period_type=="Open"`, `goal.unit` = code unità metrica.

- [ ] **Step 4: implementare lo scrittore** — estendere le classi private del VM (`ConfigPayload` con tutte le chiavi del contratto sopra, `SegmentPayload`, `GoalPayload`, `UnitDefPayload`, `ScaleDefPayload`); i nomi C# PascalCase producono le chiavi snake_case via le options esistenti (`SnakeCaseLower`). Il commento-sentinella del VM ("i nomi e l'annidamento devono restare identici a quelli del servizio") resta e ora è garantito dal guard del Task 10.

- [ ] **Step 5: suite Data + WPF verdi, Commit** — `git commit -am "feat(templates): payload v2 — config completa, goal/segmenti/serie, unità e scale incorporate con risolvi-o-crea (2.5 T8)"`

---

### Task 9: WPF — anteprima a 3 piani (E7) + anti-WebView2 + W24-Q1

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/Preview/SamplePreviewFactory.cs`
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/Preview/SampleServices.cs`
- Create: `src/Recordum.WPF/Views/HabitBuilder/BuilderPreviewPane.xaml(.cs)`
- Modify: `HabitBuilderView.xaml` (layout 2 colonne: contenuto attuale a sinistra, `BuilderPreviewPane` a destra, larghezza fissa 360; la vecchia Zone 3 "Anteprima" si rimuove)
- Delete: `src/Recordum.WPF/ViewModels/HabitBuilder/RecordingPreviewViewModel.cs` (e i suoi usi/test)
- Modify: `src/Recordum.WPF/Views/WpfHabitBuilderLauncher.cs` (allargare la finestra del dialog per la colonna anteprima)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/SamplePreviewFactoryTests.cs`, `tests/Recordum.WPF.Tests/Views/HabitBuilderPreviewViewTests.cs` (STA), `tests/Recordum.WPF.Tests/Views/HabitScreenDiarioColumnViewTests.cs` (W24-Q1)

**Interfaces:**
- Consumes: il draft dell'host (Task 3–7); i componenti reali: `HeatmapStrip` (control), i DataTemplates per-tipo di `TodayStripView.xaml` (`ITodayContent` implementations: `ActionTodayContent`, `CounterTodayContent`, `ResistanceTodayContent`, `StateTodayContent`, `JournalTodayContent` — costruttori visti in `TodayStripViewModel.LoadAsync`), i DataTemplates quick-add selezionati da `QuickAddTemplateSelector` (trovarli: `grep -rn "QuickAddTemplateSelector" src/Recordum.WPF --include=*.xaml`).
- Produces: `SamplePreviewFactory` con:

```csharp
public sealed record BuilderPreviewModel(
    HomeRowPreviewModel HomeRow,     // nome, colore, icona, tipo, chip cifra chiave (testo), celle striscia (IReadOnlyList<double>)
    ITodayContent? Today,            // VM reale caricato dagli stub, o null se il tipo non ha fascia
    object? Recording);              // VM quick-add reale, o JournalDiaryPlaceholder (marker record vuoto)

public BuilderPreviewModel Build(HabitBuilderPreviewInput input);  // input = proiezione del draft
```

`HomeRowPreviewModel` è il "modello visivo minimo" della riga (spec §3: `HomeRowViewModel` di produzione è troppo accoppiato — 4 servizi + machinery albero); il pane la renderizza con gli **stessi pezzi visivi** (accent bar 4px, `HeatmapStrip`, stile chip della riga vera).

**Determinismo**: dati campione fissi — streak 5, striscia 7/14 giorni `[1,0,1,1,0,1,1]`-ripetuta, valore odierno 2 formattato con l'unità del draft, label scala = seconda label della scala del draft, astinenza "20g 4:12" **statico**. Stessa composizione in ingresso → stesso `BuilderPreviewModel` (test).

**Stub servizi** (`SampleServices.cs`, `internal sealed class` per interfaccia): implementano `IAbstinenceService`, `ICompletionService`, `IStreakAggregator`, `IHourlyDistributionQueryService`, `IMetricDefinitionService`, `IReferenceDataQueryService`, `IGoalService`, `IHabitUiPrefsService`, `IClock` restituendo i dati campione (metriche/scale/goal presi dal draft, non dal DB). Regola di fallback: se il `LoadAsync` di un content/quick-add VM pretende più di quanto uno stub possa dare sensatamente, NON forzare — degradare quel riquadro a modello visivo minimo come la riga Home, e annotarlo nel commit (la spec §3 lo permette esplicitamente).

**Vincolo C1 (globale)**: il riquadro Registrazione usa un `DataTemplateSelector` dedicato (`PreviewRecordingTemplateSelector`) che per `JournalDiaryPlaceholder` torna la card statica (titolo + righe fantasma disegnate in XAML puro) e **mai** un template contenente `MarkdownEditorControl`. Nessun `ContentControl` del pane ha `ContentTemplate` fisso senza selector.

- [ ] **Step 1: test factory (rossi)** — `SamplePreviewFactoryTests`: (a) counter con unità "tazze" → `HomeRow` chip contiene "tazze"; (b) journal `Diary` → `Recording is JournalDiaryPlaceholder`; (c) journal `QuickNote` → `Recording` è il VM del composer (tipo reale); (d) resistance → `HomeRow` chip `"20g 4:12"`; (e) due chiamate con lo stesso input → modelli con gli stessi valori (determinismo, confrontare le proprietà del record).

- [ ] **Step 2: run rosso → implementare factory + stub.**

- [ ] **Step 3: pane XAML** — `BuilderPreviewPane` con 3 GroupBox etichettati (`Strings.HabitBuilder_Preview_Home` "In Home"/"In Home", `_Today` "Today"/"Oggi", `_Recording` "Recording"/"Registrazione"); `IsHitTestVisible="False"` sull'intero contenuto (riquadri inerti). Riquadro 2 riusa i DataTemplates di `TodayStripView` (merged dictionary o riferimento alle stesse risorse). L'host chiama `Preview = _factory.Build(...)` dentro `Recompute()` (che già scatta su ogni mutazione; i brick `Changed` dei Task 3–7 devono chiamare `Recompute`, non solo `TemplateSaved=false` — sistemare qui se i task precedenti hanno collegato solo il flag).

- [ ] **Step 4: layout** — `HabitBuilderView`: il `ScrollViewer` esistente diventa colonna 0 di una `Grid` a 2 colonne (`*` / `360`); colonna 1 = pane. `WpfHabitBuilderLauncher`: larghezza finestra +360 (leggere il valore attuale nel file e sommarlo).

- [ ] **Step 5: test anti-WebView2 (rossi → verdi)** — in `HabitBuilderPreviewViewTests` (STA, pattern `HabitScreenDiarioColumnViewTests`): montare `HabitBuilderView` con composizione journal `Diary` → `FindDescendants<MarkdownEditorControl>()` sull'intera view **vuoto**; idem con `QuickNote`.

- [ ] **Step 6: W24-Q1** — in `HabitScreenDiarioColumnViewTests`: nuovo test che monta l'intera `HabitScreen` (pannello Note **chiuso**) per un journal `QuickNote` e scansiona `FindDescendants<MarkdownEditorControl>()` sull'**intera view** (non solo `DiarioColumn`). Se il test è **rosso** (l'editor di `NotesTabView` si istanzia anche `Collapsed`): applicare a `NotesTabView` la stessa cura C1 (il contenuto Note dentro un `ContentControl` + selector/guard che non materializza l'editor finché il pannello non è aperto) finché il test è verde. Se è **verde** da subito: la sonda `Collapsed` dell'implementatore 2.4 era sbagliata sul caso reale — aggiornare la voce W24-Q1 nel tracker con l'esito, in entrambi i casi (adjudicazione, spec §5).

- [ ] **Step 7: suite WPF completa (serializzata), Commit** — `git commit -am "feat(builder): anteprima a 3 piani con componenti reali + guard anti-WebView2; adjudicato W24-Q1 (2.5 T9)"`

---

### Task 10: guard round-trip E16 (gemello di R7)

**Files:**
- Create: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/BuilderTemplateParityTests.cs`
- Test: se stesso (è un task solo-test; può toccare produzione SOLO per bug reali che scopre — ogni fix va in un commit separato con test).

**Interfaces:**
- Consumes: `HabitBuilderViewModel` completo (Task 3–8), `HabitTemplateSeed.All` (`src/Recordum.Data/SeedData/HabitTemplateSeed.cs`), `HabitTemplateService.InstantiateAsync`, il fixture DB usato da `BackupRoundTripE2ETests` (stesso pattern per un DB reale nei test WPF).
- Produces: il guard permanente. Nessuna API nuova.

**I quattro strati del guard** (spec §4):

- [ ] **Step 1: copertura** —

```csharp
private static readonly Dictionary<string, Action<HabitBuilderViewModel>> FixtureBuilders = new()
{
    ["Morning routine"] = vm => { vm.Name = "Morning routine"; vm.Color = "#f5a623"; },
    ["Workout log"]     = vm => { /* SpecificDays+Weekly+None via vm.Schedule, 3 campi (Time minutes/Quantity/Quantity),
                                     vm.SessionBrick.HasSession = true */ },
    ["Daily journal"]   = vm => { /* campo Text, gesto Diary */ },
    ["Water intake"]    = vm => { /* Quantity con unità "l", goal 2 Open */ },
    ["Sleep tracker"]   = vm => { /* 2 campi, Repeatable + labels Night/Nap, scala sleep-quality */ },
    ["Stop smoking"]    = vm => { /* Abstinence, recovery None */ },
    ["Mood tracker"]    = vm => { /* campo Scale mood-5 */ },
    ["Food diary"]      = vm => { /* campo Text, gesto QuickNote */ },
};

[Fact]
public void Every_official_template_has_a_builder_fixture()
{
    foreach (var t in HabitTemplateSeed.All)
        FixtureBuilders.Should().ContainKey(t.Name,
            $"il template ufficiale '{t.Name}' deve essere componibile nel builder (E16); aggiungere la fixture O il mattone mancante");
}
```

(Le fixture si scrivono per intero: selezionare unità/scale seed per Code dalle `AvailableUnits`/`AvailableScales` caricate con uno stub `IReferenceDataQueryService` che serve il catalogo seed reale — leggerlo dal DB del fixture.)

- [ ] **Step 2: parità semantica** — per ogni template: costruire il VM, applicare la fixture, `BuildTemplatePayloadJsonForTest()`, e confrontare col payload seed con un normalizzatore: parse `JsonDocument`, confronto ricorsivo per chiave con queste equivalenze — chiave assente ≡ null ≡ default (`"schedule_type"` assente = `"Daily"`, `"repeatable"` assente = false, array assente = vuoto); `"segments":[]` ≡ assente; l'ordine delle chiavi ignorato; i nomi metrica/segmento confrontati letterali. **Ogni chiave incontrata (in entrambi i payload) deve appartenere alla whitelist `KnownPayloadKeys`** (l'elenco completo del contratto Task 8 + `"description"`): chiave fuori lista → fail con messaggio "capacità nuova nei template senza mattone/confronto — estendere builder e guard". Questo è il meccanismo non-eludibile.

- [ ] **Step 3: round-trip pieno** — composizione → `SaveAsTemplateCommand` (o `CreateAsync` col payload del VM) → `InstantiateAsync` su DB fixture → rileggere nodo/config/metriche/goal/segmenti/ui_prefs e confrontare col draft campo per campo (schedule, streak, recovery, repeatable, series labels, timer/sessione, labels scala snapshottate).

- [ ] **Step 4: sintetico "tutto insieme"** — composizione con: 2 campi (Quantity su unità staged "tazze", Scale su scala staged "energia-3"), Ripetibile + 2 labels, Programma SpecificDays 3 giorni + Weekly + Manual, Sessione con 3 segmenti, Goal 5 Rolling → salva template → **ristanzia due volte** (la seconda deve riusare l'unità/scala create dalla prima: conteggio 1 per code) → confronto pieno.

- [ ] **Step 5: run** — questi test + l'intera suite WPF e Data. Bug scoperti dal guard (probabile: qualche default che non combacia) si fixano ciascuno nel proprio micro-ciclo rosso→verde.

- [ ] **Step 6: Commit** — `git commit -am "test(builder): guard round-trip template<->builder — copertura seed, parità semantica, ristanziazione (E16) (2.5 T10)"`

---

### Task 11: chiusura — suite completa, tracker, CLAUDE.md

**Files:**
- Modify: `docs/phase-4-issues.md` (W24-R1 → `fixed` by design T2; W24-Q1 → esito adjudicazione T9; nuova sezione "Onda 2.5")
- Modify: `CLAUDE.md` (§1 nessun cambio schema; §7: progresso 2.5, conteggi suite nuovi, prossimo passo = pass visivo unico dell'Onda 2 con checklist scritta)
- Modify: `docs/ARCHITECTURE.md` SOLO se il payload template è documentato lì (grep "payload" — in tal caso aggiornare il contratto v2)

- [ ] **Step 1: suite completa** — nell'ordine: `dotnet test tests/Recordum.Core.Tests -p:UseAppHost=false`, `tests/Recordum.Data.Tests`, `tests/Recordum.WPF.Tests … -- xUnit.ParallelizeTestCollections=false`, più `tests/` di Plugins/ExportEngine/ChartEngine se esistono directory dedicate (`ls tests/`). Annotare i conteggi.
- [ ] **Step 2: aggiornare i tre documenti** (fatti, non promesse: conteggi reali, esito W24-Q1 reale).
- [ ] **Step 3: Commit** — `git commit -am "docs: chiusura Onda 2.5 — tracker, progressi, conteggi suite"`
- [ ] **Step 4: NON mergiare.** Il merge in `main` avviene dopo la review finale di branch (flusso Onda 2.4: review sull'intero diff `main..wave-2-5-builder`) e resta comunque **prima** del pass visivo unico — decidere con l'autore.

---

## Self-review (fatta in scrittura)

- **Copertura spec**: §1 → T1/T2; §2 mattoni 1–5 → T3/T4/T5/T6/T7; §3 anteprima → T9; §4 payload+guard → T8/T10; §5 test → distribuiti + T9 (W24-Q1); §7 vincoli → Global Constraints. Nessun task per "aprire la superficie su habit esistente": fuori scope esplicito (spec §6).
- **Tipi coerenti**: `ComposedHabitRequest` v2 (T2) è consumata con gli stessi nomi in T4/T5/T7/T10; `ApplyTo(HabitConfig)` uniforme tra Schedule e Session brick; `BuildTemplatePayloadJsonForTest` introdotto in T8 e usato in T10.
- **Punti di rischio dichiarati**: stub-services del T9 (fallback al modello visivo minimo, permesso dalla spec); parità semantica T10 (equivalenze default esplicitate); `InternalsVisibleTo` per `HabitUiPrefsJson` (verifica nel T2, con alternativa `public`).
