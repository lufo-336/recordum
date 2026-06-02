# Modular Habit Builder Implementation Plan (Spec 1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un costruttore di abitudini **block-first**: tela vuota → assembli mattoni (campi misurati, serie, modo) → anteprima dal vivo → **Lancia** (creazione atomica). Il `habit_type` è dedotto dai mattoni. Più: salvataggio della composizione come **template personalizzato**.

**Architecture:** Una **grammatica pura** in `Recordum.Core` risolve i mattoni → `HabitType` e applica i guardrail (zero dipendenze UI/EF, interamente testabile). Un **draft in memoria** nel `HabitBuilderViewModel` (WPF) compone nodo+metriche+config con GUID client-side; al Lancia un nuovo **`IComposedHabitService.CreateComposedHabitAsync`** (Data) li persiste in **una transazione**. Il picker apre il builder per "Da zero" e lista i template custom (salvati via gli esistenti `IHabitTemplateService.CreateAsync/UpdateAsync/DeleteAsync`).

**Tech Stack:** C#/.NET 10, EF Core + SQLite, WPF + CommunityToolkit.Mvvm, xUnit (+ FluentAssertions, Xunit.StaFact per i test WPF).

**Spec:** `docs/superpowers/specs/2026-06-02-modular-habit-builder-design.md`

## Scope di QUESTO piano (importante)
- **IN**: grammatica mattoni→tipo + guardrail; campi (conteggio/quantità/tempo/scala) + testo libero; serie (default count); modo (manuale/cronometro/countdown); draft + Lancia atomico (nodo+config+metriche); anteprima dal vivo; "Da zero" dal picker; **template personalizzati** (salva/lista/rinomina/elimina).
- **FUORI (deferito, dichiarato)**: **modo sessione + runtime timer** → Spec 2. **Pannello "Configurazione avanzata"** (riuso delle 11 section view in modalità bozza) → Spec 3: in Spec 1 il Lancia crea l'abitudine con **config di default** (timer-mode incluso); il resto si rifinisce nell'editor "Configura" esistente post-lancio. **Modifica di abitudini esistenti** → Spec 3. **Gruppi** → invariati.

**Convenzioni di progetto:**
- TDD rosso→verde, commit frequenti. Mai `DateTime.Now`; usare `IClock`/`TimeProvider`.
- Test: `dotnet test tests/Recordum.Core.Tests`, `tests/Recordum.Data.Tests`, `tests/Recordum.WPF.Tests`.
- L'app aperta blocca `Recordum.WPF.exe`: chiuderla prima di build.
- Stringhe utente in `.resx` (EN in `Strings.resx`, IT in `Strings.it.resx`); il Designer (`Strings.Designer.cs`) è **mantenuto a mano** — chiave dot-notation, proprietà underscore via `Get(...)`.
- Nessuna migrazione: schema resta **v2.8**.

---

## File map

| File | Azione | Responsabilità |
|---|---|---|
| `src/Recordum.Core/Enums/FieldNature.cs` | Create | enum `Count, Quantity, Time, Scale, Text` |
| `src/Recordum.Core/Enums/RecordingMode.cs` | Create | enum `Manual, Stopwatch, Countdown` (Session deferita) |
| `src/Recordum.Core/Composition/HabitComposition.cs` | Create | record snapshot dei mattoni |
| `src/Recordum.Core/Composition/HabitCompositionRules.cs` | Create | `ResolveType` + `CanAddField`/`CanEnableSeries`/`CanSetMode` (pure) |
| `src/Recordum.Core/Abstractions/Services/ComposedHabitRequest.cs` | Create | DTO `(HabitNode, HabitConfig?, IReadOnlyList<MetricDefinition>)` |
| `src/Recordum.Core/Abstractions/Services/IComposedHabitService.cs` | Create | `CreateComposedHabitAsync` |
| `src/Recordum.Data/Services/ComposedHabitService.cs` | Create | create atomico in transazione |
| `src/Recordum.WPF/ViewModels/HabitBuilder/FieldBlockViewModel.cs` | Create | un campo (nature, nome, unità/scala) |
| `src/Recordum.WPF/ViewModels/HabitBuilder/RecordingPreviewViewModel.cs` | Create | anteprima righe(serie)×celle(campi) |
| `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs` | Create | builder: palette, grammatica, preview, Launch/Cancel/SaveAsTemplate |
| `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml` (+ `.xaml.cs`) | Create | superficie ampia |
| `src/Recordum.WPF/ViewModels/HabitBuilder/IHabitBuilderLauncher.cs` | Create | `Task<Guid?> LaunchAsync(parentId)` |
| `src/Recordum.WPF/Views/WpfHabitBuilderLauncher.cs` | Create | mostra il builder come finestra/dialog |
| `src/Recordum.WPF/ViewModels/HabitEditor/HabitTemplatePickerViewModel.cs` | Modify | "Da zero" → builder; sezione "I tuoi template" (rinomina/elimina) |
| `src/Recordum.WPF/ViewModels/HabitEditor/HabitTemplatePicker.xaml` | Modify | UI custom templates |
| `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` | Modify | DI dei nuovi servizi/VM/launcher |
| `src/Recordum.WPF/Resources/Strings.resx` (+ `.it.resx` + `.Designer.cs`) | Modify | nuove stringhe |
| `tests/Recordum.Core.Tests/Composition/HabitCompositionRulesTests.cs` | Create | grammatica (load-bearing) |
| `tests/Recordum.Data.Tests/Integration/ComposedHabitServiceTests.cs` | Create | create atomico |
| `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs` | Create | VM grammar-driven + preview + save-template |

> Verifica che esista un progetto `tests/Recordum.Core.Tests`; se non c'è, mettere i test della grammatica in `tests/Recordum.Data.Tests` (referenzia Core) in una cartella `Composition/`.

---

## Task 1: `FieldNature` + `RecordingMode` + `HabitComposition` (Core)

**Files:**
- Create: `src/Recordum.Core/Enums/FieldNature.cs`
- Create: `src/Recordum.Core/Enums/RecordingMode.cs`
- Create: `src/Recordum.Core/Composition/HabitComposition.cs`

- [ ] **Step 1: enums**

`FieldNature.cs`:
```csharp
namespace Recordum.Core.Enums;

/// <summary>La "natura" di un campo misurato nel costruttore block-first.</summary>
public enum FieldNature { Count, Quantity, Time, Scale, Text }
```
`RecordingMode.cs`:
```csharp
namespace Recordum.Core.Enums;

/// <summary>Modo di registrazione componibile in Spec 1 (Session arriva con Spec 2).</summary>
public enum RecordingMode { Manual, Stopwatch, Countdown }
```

- [ ] **Step 2: composition snapshot**

`HabitComposition.cs`:
```csharp
using Recordum.Core.Enums;

namespace Recordum.Core.Composition;

/// <summary>Snapshot immutabile dei mattoni correnti, input puro della grammatica.</summary>
public sealed record HabitComposition(
    IReadOnlyList<FieldNature> Fields,
    bool SeriesEnabled,
    RecordingMode Mode)
{
    public static HabitComposition Empty { get; } =
        new(Array.Empty<FieldNature>(), SeriesEnabled: false, RecordingMode.Manual);
}
```

- [ ] **Step 3: Build** · `dotnet build src/Recordum.Core` → 0 errori.

- [ ] **Step 4: Commit** · `git commit -m "feat(core): composition primitives (FieldNature, RecordingMode, HabitComposition)"`

---

## Task 2: `HabitCompositionRules.ResolveType` (Core, grammatica)

**Files:**
- Create: `src/Recordum.Core/Composition/HabitCompositionRules.cs`
- Test: `tests/Recordum.Core.Tests/Composition/HabitCompositionRulesTests.cs`

- [ ] **Step 1: Failing tests** (risoluzione per priorità, §4.2 della spec)
```csharp
using Recordum.Core.Composition;
using Recordum.Core.Enums;
using FluentAssertions;
using Xunit;

namespace Recordum.Core.Tests.Composition;

public class HabitCompositionRulesTests
{
    private static HabitComposition Comp(bool series = false, RecordingMode mode = RecordingMode.Manual, params FieldNature[] fields)
        => new(fields, series, mode);

    [Fact] public void Text_present_resolves_journal()
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Text })).Should().Be(HabitType.Journal);

    [Fact] public void Text_with_time_still_journal()
        => HabitCompositionRules.ResolveType(Comp(mode: RecordingMode.Stopwatch, fields: new[]{ FieldNature.Text, FieldNature.Time })).Should().Be(HabitType.Journal);

    [Fact] public void Series_resolves_resistance()
        => HabitCompositionRules.ResolveType(Comp(series: true, fields: new[]{ FieldNature.Count, FieldNature.Time })).Should().Be(HabitType.Resistance);

    [Fact] public void Scale_only_resolves_state()
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Scale })).Should().Be(HabitType.State);

    [Fact] public void Count_resolves_counter()
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Count })).Should().Be(HabitType.Counter);

    [Fact] public void Quantity_or_time_resolves_counter()
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Time })).Should().Be(HabitType.Counter);

    [Fact] public void Scale_plus_count_resolves_counter() // scala come rating extra in counter
        => HabitCompositionRules.ResolveType(Comp(fields: new[]{ FieldNature.Scale, FieldNature.Count })).Should().Be(HabitType.Counter);

    [Fact] public void Empty_resolves_action()
        => HabitCompositionRules.ResolveType(HabitComposition.Empty).Should().Be(HabitType.Action);

    [Fact] public void Series_beats_text_is_impossible_text_wins_priority() // text ha priorità 1
        => HabitCompositionRules.ResolveType(Comp(series: true, fields: new[]{ FieldNature.Text })).Should().Be(HabitType.Journal);
}
```

- [ ] **Step 2: Run — verify FAIL** · `dotnet test tests/Recordum.Core.Tests --filter HabitCompositionRules` → FAIL (classe assente).

- [ ] **Step 3: Implementation**

`HabitCompositionRules.cs` (parte 1 — `ResolveType`):
```csharp
using Recordum.Core.Enums;

namespace Recordum.Core.Composition;

/// <summary>Grammatica pura mattoni→tipo + guardrail (spec §4). Zero dipendenze UI/EF.</summary>
public static class HabitCompositionRules
{
    public static HabitType ResolveType(HabitComposition c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Fields.Contains(FieldNature.Text)) return HabitType.Journal;   // #1
        if (c.SeriesEnabled) return HabitType.Resistance;                    // #2
        bool hasMeasured = c.Fields.Any(f => f is FieldNature.Count or FieldNature.Quantity or FieldNature.Time);
        bool hasScale = c.Fields.Contains(FieldNature.Scale);
        if (hasScale && !hasMeasured) return HabitType.State;                // #3
        if (hasMeasured) return HabitType.Counter;                           // #4
        return HabitType.Action;                                            // #5
    }
}
```

- [ ] **Step 4: Run — verify PASS** · stesso filtro → tutti PASS.

- [ ] **Step 5: Commit** · `git commit -m "feat(core): habit composition type resolution"`

---

## Task 3: Guardrail `CanAddField` / `CanEnableSeries` / `CanSetMode` (Core)

**Files:**
- Modify: `src/Recordum.Core/Composition/HabitCompositionRules.cs`
- Test: `tests/Recordum.Core.Tests/Composition/HabitCompositionRulesTests.cs` (aggiungere)

> Regole dei guardrail (spec §4.3, ✗ = bloccato). `journal` (= testo libero presente) ammette **solo** un campo `Time` extra; `state`/`resistance` hanno esclusioni; `Series` esclusa con `Scale` o `Text`; modo cronometro/countdown solo se c'è almeno un campo misurabile o (journal) un campo Time.

- [ ] **Step 1: Failing tests**
```csharp
[Fact] public void Cannot_add_count_when_text_present()
    => HabitCompositionRules.CanAddField(Comp(fields: new[]{ FieldNature.Text }), FieldNature.Count).Should().BeFalse();

[Fact] public void Can_add_time_when_text_present() // rilassamento journal+tempo
    => HabitCompositionRules.CanAddField(Comp(fields: new[]{ FieldNature.Text }), FieldNature.Time).Should().BeTrue();

[Fact] public void Cannot_add_second_time_to_journal()
    => HabitCompositionRules.CanAddField(Comp(fields: new[]{ FieldNature.Text, FieldNature.Time }), FieldNature.Time).Should().BeFalse();

[Fact] public void Cannot_add_text_when_other_fields_present()
    => HabitCompositionRules.CanAddField(Comp(fields: new[]{ FieldNature.Count }), FieldNature.Text).Should().BeFalse();

[Fact] public void Cannot_add_scale_when_series_enabled() // serie=resistance, scala esclusa
    => HabitCompositionRules.CanAddField(Comp(series: true, fields: new[]{ FieldNature.Count }), FieldNature.Scale).Should().BeFalse();

[Fact] public void Cannot_enable_series_with_scale()
    => HabitCompositionRules.CanEnableSeries(Comp(fields: new[]{ FieldNature.Scale })).Should().BeFalse();

[Fact] public void Cannot_enable_series_with_text()
    => HabitCompositionRules.CanEnableSeries(Comp(fields: new[]{ FieldNature.Text })).Should().BeFalse();

[Fact] public void Can_enable_series_with_count()
    => HabitCompositionRules.CanEnableSeries(Comp(fields: new[]{ FieldNature.Count })).Should().BeTrue();

[Fact] public void Timer_mode_needs_measured_or_journal_time()
{
    HabitCompositionRules.CanSetMode(HabitComposition.Empty, RecordingMode.Stopwatch).Should().BeFalse(); // action: no timer
    HabitCompositionRules.CanSetMode(Comp(fields: new[]{ FieldNature.Count }), RecordingMode.Stopwatch).Should().BeTrue(); // counter
    HabitCompositionRules.CanSetMode(Comp(fields: new[]{ FieldNature.Scale }), RecordingMode.Countdown).Should().BeFalse(); // state: no timer
    HabitCompositionRules.CanSetMode(Comp(fields: new[]{ FieldNature.Text, FieldNature.Time }), RecordingMode.Stopwatch).Should().BeTrue(); // journal+time
}
```

- [ ] **Step 2: Run — verify FAIL**.

- [ ] **Step 3: Implementation** — aggiungere a `HabitCompositionRules`:
```csharp
    public static bool CanAddField(HabitComposition c, FieldNature nature)
    {
        ArgumentNullException.ThrowIfNull(c);
        bool hasText = c.Fields.Contains(FieldNature.Text);
        switch (nature)
        {
            case FieldNature.Text:
                // testo libero solo se non ci sono altri campi (tranne un eventuale Time) e niente serie
                return !hasText && !c.SeriesEnabled
                       && c.Fields.All(f => f == FieldNature.Time);
            case FieldNature.Count:
            case FieldNature.Quantity:
                return !hasText;                       // counter/resistance ok; journal no
            case FieldNature.Scale:
                return !hasText && !c.SeriesEnabled;   // niente scala in journal o resistance
            case FieldNature.Time:
                if (hasText) return !c.Fields.Contains(FieldNature.Time); // journal: max un Time
                return true;                           // counter/resistance: ok
            default:
                return false;
        }
    }

    public static bool CanEnableSeries(HabitComposition c)
    {
        ArgumentNullException.ThrowIfNull(c);
        // serie = resistance: niente scala, niente testo libero
        return !c.Fields.Contains(FieldNature.Scale) && !c.Fields.Contains(FieldNature.Text);
    }

    public static bool CanSetMode(HabitComposition c, RecordingMode mode)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (mode == RecordingMode.Manual) return true;
        // cronometro/countdown: serve un campo misurabile (counter/resistance) o journal+Time
        bool hasMeasured = c.Fields.Any(f => f is FieldNature.Count or FieldNature.Quantity or FieldNature.Time);
        return hasMeasured;
    }
```

- [ ] **Step 4: Run — verify PASS** (Task 2 + Task 3 verdi).

- [ ] **Step 5: Commit** · `git commit -m "feat(core): habit composition guardrails (CanAddField/Series/Mode)"`

---

## Task 4: `ComposedHabitRequest` + `IComposedHabitService` (Core)

**Files:**
- Create: `src/Recordum.Core/Abstractions/Services/ComposedHabitRequest.cs`
- Create: `src/Recordum.Core/Abstractions/Services/IComposedHabitService.cs`

- [ ] **Step 1: DTO**

`ComposedHabitRequest.cs`:
```csharp
using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

/// <summary>Composizione pronta per il Lancia: il nodo, la sua config, e le metriche
/// (con Id già assegnati lato client). Persistita atomicamente da IComposedHabitService.</summary>
public sealed record ComposedHabitRequest(
    HabitNode Node,
    HabitConfig? Config,
    IReadOnlyList<MetricDefinition> Metrics);
```

- [ ] **Step 2: Interface**

`IComposedHabitService.cs`:
```csharp
namespace Recordum.Core.Abstractions.Services;

public interface IComposedHabitService
{
    /// <summary>Crea nodo + config + metriche in UNA transazione. Ritorna l'id del nodo.</summary>
    ValueTask<Guid> CreateComposedHabitAsync(ComposedHabitRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build** · `dotnet build src/Recordum.Core` → 0 errori.

- [ ] **Step 4: Commit** · `git commit -m "feat(core): IComposedHabitService + ComposedHabitRequest"`

---

## Task 5: `ComposedHabitService` (Data, create atomico)

**Files:**
- Create: `src/Recordum.Data/Services/ComposedHabitService.cs`
- Test: `tests/Recordum.Data.Tests/Integration/ComposedHabitServiceTests.cs`

> Leggi PRIMA `src/Recordum.Data/Services/HabitNodeService.cs` per il pattern reale: nome del `IDbContextFactory` field, come si apre/salva il DbContext, come `TimestampInterceptor` popola `CreatedAt`/`UpdatedAt` (NON impostarli a mano), e come avviare una transazione. Adatta i nomi.

- [ ] **Step 1: Failing test**
```csharp
[Fact]
public async Task CreateComposedHabit_persists_node_config_and_metrics_atomically()
{
    await using var fx = new DbFixture();
    var svc = fx.CreateComposedHabitService(); // adatta agli helper reali del file
    var nodeId = Guid.NewGuid();
    var node = new HabitNode { Id = nodeId, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "Flessioni", Color = "#5fb37a" };
    var metric = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = nodeId, Name = "Ripetizioni", Type = MetricType.Integer, SortOrder = 0 };
    var req = new ComposedHabitRequest(node, Config: null, Metrics: new[]{ metric });

    var returned = await svc.CreateComposedHabitAsync(req);

    returned.Should().Be(nodeId);
    await using var db = await fx.Factory.CreateDbContextAsync();
    (await db.HabitNodes.FindAsync(nodeId)).Should().NotBeNull();
    (await db.MetricDefinitions.CountAsync(m => m.HabitNodeId == nodeId)).Should().Be(1);
}

[Fact]
public async Task CreateComposedHabit_rolls_back_when_a_metric_is_invalid()
{
    await using var fx = new DbFixture();
    var svc = fx.CreateComposedHabitService();
    var nodeId = Guid.NewGuid();
    var node = new HabitNode { Id = nodeId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "X", Color = "#808080" };
    // metrica con HabitNodeId errato → la FK/contesto deve far fallire e annullare tutto
    var bad = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), Name = "Bad", Type = MetricType.Integer };
    var req = new ComposedHabitRequest(node, null, new[]{ bad });

    var act = async () => await svc.CreateComposedHabitAsync(req);

    await act.Should().ThrowAsync<Exception>();
    await using var db = await fx.Factory.CreateDbContextAsync();
    (await db.HabitNodes.FindAsync(nodeId)).Should().BeNull(); // rollback: niente nodo orfano
}
```
> Se la metrica con HabitNodeId errato non provoca un errore nel tuo setup (SQLite FK off), sostituisci con un trigger d'errore realistico (es. nome troppo lungo che viola un vincolo, o forza un'eccezione iniettando un secondo nodo con stesso Id). L'importante è il test "rollback = nessun nodo orfano". Adatta al modello reale.

- [ ] **Step 2: Run — verify FAIL**.

- [ ] **Step 3: Implementation**

`ComposedHabitService.cs` (adatta `dbFactory` al nome reale):
```csharp
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Data.Context; // adatta al namespace reale del DbContext

namespace Recordum.Data.Services;

public sealed class ComposedHabitService(IDbContextFactory<RecordumDbContext> dbFactory) : IComposedHabitService
{
    public async ValueTask<Guid> CreateComposedHabitAsync(ComposedHabitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.HabitNodes.Add(request.Node);
        if (request.Config is not null) db.Set<HabitConfig>().Add(request.Config);
        foreach (var m in request.Metrics) db.MetricDefinitions.Add(m);

        await db.SaveChangesAsync(ct);   // TimestampInterceptor popola CreatedAt/UpdatedAt/CreatedByPluginId
        await tx.CommitAsync(ct);
        return request.Node.Id;
    }
}
```
> Verifica i `DbSet` reali (`HabitNodes`, `MetricDefinitions`, e il set per `HabitConfig`). Non impostare i timestamp a mano. Se il provider è SQLite in-memory nei test e le transazioni si comportano diversamente, mantieni comunque `BeginTransactionAsync` (l'interceptor SQLCipher in produzione lo richiede per atomicità).

- [ ] **Step 4: Run — verify PASS**.

- [ ] **Step 5: Commit** · `git commit -m "feat(data): atomic CreateComposedHabitAsync (node+config+metrics)"`

---

## Task 6: DI dei servizi Core/Data

**Files:**
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`

- [ ] **Step 1: registra il servizio** — accanto a `services.AddScoped<IHabitNodeService, HabitNodeService>();`:
```csharp
services.AddScoped<IComposedHabitService, ComposedHabitService>();
```
> Usa lo stesso lifetime degli altri servizi Data (`AddScoped`). Aggiungi i `using` necessari.

- [ ] **Step 2: Build** · `dotnet build src/Recordum.WPF` → 0 errori (l'app deve essere chiusa).

- [ ] **Step 3: Commit** · `git commit -m "chore(wpf): register IComposedHabitService"`

---

## Task 7: `FieldBlockViewModel` (WPF)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/FieldBlockViewModel.cs`
- Test: coperto indirettamente dai test del builder (Task 10); nessun test dedicato necessario.

- [ ] **Step 1: Implementation**
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>Un campo misurato nella composizione. La 'nature' determina i sotto-campi rilevanti
/// (unità per Quantity, scala per Scale). Possiede un Id client-side per il MetricDefinition futuro.</summary>
public sealed partial class FieldBlockViewModel : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public FieldNature Nature { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private Guid? _unitOfMeasureId;     // Quantity
    [ObservableProperty] private Guid? _scaleTemplateId;     // Scale

    public FieldBlockViewModel(FieldNature nature, string name)
    {
        Nature = nature;
        _name = name;
    }

    /// <summary>Mappa la nature al MetricType del dominio.</summary>
    public MetricType MetricType => Nature switch
    {
        FieldNature.Count => MetricType.Integer,
        FieldNature.Quantity => MetricType.Decimal,
        FieldNature.Time => MetricType.Seconds,
        FieldNature.Scale => MetricType.Integer,
        FieldNature.Text => MetricType.Text,
        _ => MetricType.Integer,
    };
}
```

- [ ] **Step 2: Build** · `dotnet build src/Recordum.WPF` → 0 errori.

- [ ] **Step 3: Commit** · `git commit -m "feat(wpf): FieldBlockViewModel for habit builder"`

---

## Task 8: `RecordingPreviewViewModel` (WPF)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/RecordingPreviewViewModel.cs`
- Test: coperto dai test del builder (Task 10).

> L'anteprima è **read-only**: righe = serie (1 se serie disattivata), celle = campi (escluso il Text, che è una casella sotto). Il builder chiama `Rebuild(fields, seriesEnabled, seriesCount)` ad ogni cambio.

- [ ] **Step 1: Implementation**
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitBuilder;

public sealed partial class RecordingPreviewViewModel : ObservableObject
{
    public ObservableCollection<PreviewRow> Rows { get; } = new();
    [ObservableProperty] private bool _hasTextField;

    public void Rebuild(IReadOnlyList<FieldBlockViewModel> fields, bool seriesEnabled, int seriesCount)
    {
        Rows.Clear();
        var cellNames = fields.Where(f => f.Nature != Core.Enums.FieldNature.Text)
                              .Select(f => f.Name).ToList();
        HasTextField = fields.Any(f => f.Nature == Core.Enums.FieldNature.Text);
        int rows = seriesEnabled ? Math.Max(1, seriesCount) : 1;
        for (int i = 0; i < rows; i++)
            Rows.Add(new PreviewRow(seriesEnabled ? $"Serie {i + 1}" : string.Empty, cellNames));
    }
}

public sealed record PreviewRow(string Label, IReadOnlyList<string> CellNames);
```

- [ ] **Step 2: Build** → 0 errori.

- [ ] **Step 3: Commit** · `git commit -m "feat(wpf): RecordingPreviewViewModel (series x fields)"`

---

## Task 9: `HabitBuilderViewModel` — composizione + grammatica

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs`

> Il VM possiede: nome/colore/icona, la collezione di `FieldBlockViewModel`, `SeriesEnabled`/`SeriesCount`, `Mode`, il `RecordingPreviewViewModel`, il **`DerivedType`** (via grammatica), i comandi `AddFieldCommand(FieldNature)`/`RemoveFieldCommand`/`ToggleSeries`, `LaunchCommand`, `SaveAsTemplateCommand`. Dipende da `IComposedHabitService`, `IHabitTemplateService`, `IClock`.

- [ ] **Step 1: Failing test** (grammatica guida tipo + preview; add bloccato dai guardrail)
```csharp
[WpfFact]
public void Adding_count_then_time_then_series_yields_resistance_and_preview()
{
    var sut = NewSut(); // helper: new HabitBuilderViewModel(fakeComposed, fakeTemplates, fakeClock)
    sut.Name = "Flessioni";
    sut.AddFieldCommand.Execute(FieldNature.Count);
    sut.AddFieldCommand.Execute(FieldNature.Time);
    sut.ToggleSeriesCommand.Execute(true);
    sut.SeriesCount = 3;

    sut.DerivedType.Should().Be(HabitType.Resistance);
    sut.Preview.Rows.Should().HaveCount(3);
    sut.Preview.Rows[0].CellNames.Should().HaveCount(2);
}

[WpfFact]
public void Text_field_blocks_count_and_makes_journal()
{
    var sut = NewSut();
    sut.AddFieldCommand.Execute(FieldNature.Text);
    sut.CanAddField(FieldNature.Count).Should().BeFalse();
    sut.DerivedType.Should().Be(HabitType.Journal);
}

[WpfFact]
public async Task Launch_persists_via_composed_service_with_derived_type()
{
    var fake = new FakeComposedHabitService();
    var sut = NewSut(composed: fake);
    sut.Name = "Bevi acqua";
    sut.AddFieldCommand.Execute(FieldNature.Quantity);

    await sut.LaunchCommand.ExecuteAsync(null);

    fake.LastRequest.Should().NotBeNull();
    fake.LastRequest!.Node.HabitType.Should().Be(HabitType.Counter);
    fake.LastRequest.Metrics.Should().ContainSingle();
    sut.CreatedHabitNodeId.Should().Be(fake.LastRequest.Node.Id);
}
```
> Crea un `FakeComposedHabitService` in `tests/.../Fixtures/` che cattura l'ultimo `ComposedHabitRequest` e ritorna `request.Node.Id`. `FakeHabitTemplateService` probabilmente esiste già (riusalo); altrimenti minimale. Usa `[WpfFact]` (Xunit.StaFact) come gli altri test VM.

- [ ] **Step 2: Run — verify FAIL**.

- [ ] **Step 3: Implementation** (struttura completa; adatta i `using`)
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Composition;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitBuilder;

public sealed partial class HabitBuilderViewModel : ObservableObject
{
    private readonly IComposedHabitService _composed;
    private readonly IHabitTemplateService _templates;
    private readonly IClock _clock;
    private Guid? _parentId;

    public HabitBuilderViewModel(IComposedHabitService composed, IHabitTemplateService templates, IClock clock)
    {
        _composed = composed; _templates = templates; _clock = clock;
    }

    public ObservableCollection<FieldBlockViewModel> Fields { get; } = new();
    public RecordingPreviewViewModel Preview { get; } = new();

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _color = "#808080";
    [ObservableProperty] private string? _icon;
    [ObservableProperty] private bool _seriesEnabled;
    [ObservableProperty] private int _seriesCount = 3;
    [ObservableProperty] private RecordingMode _mode = RecordingMode.Manual;
    [ObservableProperty] private HabitType _derivedType = HabitType.Action;
    [ObservableProperty] private Guid? _createdHabitNodeId;

    public void SetParent(Guid? parentId) => _parentId = parentId;

    private HabitComposition Snapshot() =>
        new(Fields.Select(f => f.Nature).ToList(), SeriesEnabled, Mode);

    public bool CanAddField(FieldNature nature) => HabitCompositionRules.CanAddField(Snapshot(), nature);

    [RelayCommand]
    private void AddField(FieldNature nature)
    {
        if (!CanAddField(nature)) return;
        Fields.Add(new FieldBlockViewModel(nature, DefaultName(nature)));
        Recompute();
    }

    [RelayCommand]
    private void RemoveField(FieldBlockViewModel field)
    {
        if (field is null) return;
        Fields.Remove(field);
        Recompute();
    }

    [RelayCommand]
    private void ToggleSeries(bool enabled)
    {
        if (enabled && !HabitCompositionRules.CanEnableSeries(Snapshot())) return;
        SeriesEnabled = enabled;
        Recompute();
    }

    partial void OnSeriesCountChanged(int value) => Recompute();
    partial void OnModeChanged(RecordingMode value) => Recompute();
    partial void OnNameChanged(string value) => LaunchCommand.NotifyCanExecuteChanged();

    private void Recompute()
    {
        DerivedType = HabitCompositionRules.ResolveType(Snapshot());
        Preview.Rebuild(Fields, SeriesEnabled, SeriesCount);
        LaunchCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Lancia abilitato solo con nome valido e ogni campo ben formato
    /// (Quantity → unità scelta; Scale → scala scelta). Spec §8.</summary>
    public bool CanLaunch =>
        !string.IsNullOrWhiteSpace(Name)
        && Fields.All(f =>
            (f.Nature != FieldNature.Quantity || f.UnitOfMeasureId is not null) &&
            (f.Nature != FieldNature.Scale || f.ScaleTemplateId is not null));

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync(CancellationToken ct)
    {
        CreatedHabitNodeId = await _composed.CreateComposedHabitAsync(BuildRequest(), ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveAsTemplateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Name)) return;
        var template = new HabitTemplate
        {
            Id = Guid.NewGuid(),
            Name = Name,
            IsOfficial = false,
            PayloadJson = BuildTemplatePayloadJson(),  // vedi Task 12
            UpdatedAtMs = _clock.UtcNowMs,
        };
        await _templates.CreateAsync(template, ct).ConfigureAwait(true);
    }

    private ComposedHabitRequest BuildRequest()
    {
        var nodeId = Guid.NewGuid();
        var node = new HabitNode
        {
            Id = nodeId, ParentId = _parentId, NodeType = NodeType.Habit,
            HabitType = DerivedType, Name = Name.Trim(), Color = Color, Icon = Icon,
        };
        var metrics = Fields.Where(f => f.Nature != FieldNature.Text).Select((f, i) => new MetricDefinition
        {
            Id = f.Id, HabitNodeId = nodeId, Name = f.Name, Type = f.MetricType,
            UnitOfMeasureId = f.UnitOfMeasureId, ScaleTemplateId = f.ScaleTemplateId, SortOrder = i,
        }).ToList();
        // config con timer-mode dal Mode (Manual = nessun timer). Vedi nota TimerMode sotto.
        var config = BuildConfig(nodeId);
        return new ComposedHabitRequest(node, config, metrics);
    }

    private static string DefaultName(FieldNature n) => n switch
    {
        FieldNature.Count => "Conteggio", FieldNature.Quantity => "Quantità",
        FieldNature.Time => "Tempo", FieldNature.Scale => "Scala", FieldNature.Text => "Note", _ => "Campo",
    };
}
```
> **`BuildConfig` e `TimerMode`**: leggi `TimerSectionViewModel` + il modello `HabitConfig` per il campo/enum reale del timer-mode (es. `HabitConfig.TimerMode`). Mappa `RecordingMode.Manual→nessun timer`, `Stopwatch→Stopwatch`, `Countdown→Countdown`. Se per il MVP preferisci, `BuildConfig` può ritornare `null` quando `Mode==Manual` e una `HabitConfig` con solo il timer-mode altrimenti. `BuildTemplatePayloadJson` è implementato nel Task 12.
> Il default delle stringhe (`DefaultName`) andrà localizzato (Task 13) — qui placeholder accettabile, sostituibili con `Strings.*`.

- [ ] **Step 4: Run — verify PASS** (i test della grammatica via VM passano; `SaveAsTemplate` testato in Task 12).

- [ ] **Step 5: Commit** · `git commit -m "feat(wpf): HabitBuilderViewModel (grammar-driven composition + launch)"`

---

## Task 10: `HabitBuilderView.xaml` — la superficie ampia

**Files:**
- Create: `src/Recordum.WPF/Views/HabitBuilder/HabitBuilderView.xaml` (+ `.xaml.cs`)

Solo XAML — verifica visiva. Copia struttura/namespace da una view esistente ampia (es. `Views/HabitScreen/HabitScreen.xaml` o l'editor). Tre zone (spec §5): testata (Nome/Colore/Icona + badge `DerivedType`), composizione (palette `AddFieldCommand` con `CommandParameter="{x:Static enums:FieldNature.Count}"` ecc.; lista `Fields` con `RemoveFieldCommand`; toggle serie + `SeriesCount`; selettore `Mode`), anteprima (`ItemsControl ItemsSource="{Binding Preview.Rows}"` → per riga `Label` + celle `CellNames`). In fondo: `Annulla` (chiude la finestra) · `Salva come template` (`SaveAsTemplateCommand`) · `Lancia` (`LaunchCommand`).

- [ ] **Step 1: Scrivi la view** seguendo i pattern esistenti (riusa `HexToBrush`, `BaseStyles`, `FontSize.*`). `.xaml.cs`: `public HabitBuilderView() => InitializeComponent();`.
- [ ] **Step 2: Build** · `dotnet build src/Recordum.WPF` → 0 errori.
- [ ] **Step 3: Commit** · `git commit -m "feat(wpf): HabitBuilderView (ample block-first surface)"`

---

## Task 11: Launcher + integrazione picker "Da zero"

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitBuilder/IHabitBuilderLauncher.cs`
- Create: `src/Recordum.WPF/Views/WpfHabitBuilderLauncher.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/HabitTemplatePickerViewModel.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`

> Modello: copia `IHabitTemplatePickerLauncher` + `WpfHabitTemplatePickerLauncher` (mostrano una finestra modale e ritornano l'id creato). Il builder espone `CreatedHabitNodeId`; la finestra `HabitBuilderWindow` chiude con `DialogResult=true` quando il Lancia ha valorizzato l'id.

- [ ] **Step 1:** `IHabitBuilderLauncher.cs`:
```csharp
namespace Recordum.WPF.ViewModels.HabitBuilder;

public interface IHabitBuilderLauncher
{
    Task<Guid?> LaunchAsync(Guid? parentId = null, CancellationToken ct = default);
}
```
- [ ] **Step 2:** `WpfHabitBuilderLauncher.cs` — risolve `HabitBuilderViewModel` dal `IServiceProvider`, chiama `SetParent(parentId)`, mostra la finestra che ospita `HabitBuilderView`, ritorna `CreatedHabitNodeId` su `ShowDialog()==true`. (Crea anche la `Window` ospite, come fa `HabitTemplatePicker`.)
- [ ] **Step 3:** In `HabitTemplatePickerViewModel.PickAsync`, per l'opzione **"Abitudine vuota / Da zero"** (TemplateId null, NodeType Habit): **non** creare più il nodo Action diretto — invece segnala al launcher del picker di aprire il builder. Approccio minimo: aggiungi una proprietà `[ObservableProperty] bool _openBuilderRequested;` e una `Guid? ParentForBuilder`; il launcher del picker, dopo la chiusura, se `OpenBuilderRequested` chiama `IHabitBuilderLauncher.LaunchAsync(ParentForBuilder)` e ne ritorna l'id. (In alternativa inietta `IHabitBuilderLauncher` nel picker VM e apri direttamente.) Mantieni "Gruppo vuoto" e i template invariati.
- [ ] **Step 4:** `ServiceRegistrar`: `services.AddTransient<HabitBuilderViewModel>();` e `services.AddSingleton<IHabitBuilderLauncher, WpfHabitBuilderLauncher>();`.
- [ ] **Step 5: Build** → 0 errori.
- [ ] **Step 6: Commit** · `git commit -m "feat(wpf): habit builder launcher + 'from scratch' opens builder"`

---

## Task 12: `BuildTemplatePayloadJson` (composizione → template) + test round-trip

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitBuilder/HabitBuilderViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitBuilder/HabitBuilderViewModelTests.cs` (aggiungere)

> Leggi PRIMA `src/Recordum.Data/Services/HabitTemplateService.cs` (`InstantiateAsync`) e il modello `TemplatePayload` (esteso in B6a) per la **forma esatta** del JSON che `InstantiateAsync` consuma (nodo/config/metriche/scala). `BuildTemplatePayloadJson` deve produrre quella stessa forma, così un template salvato dal builder reistanzia la stessa composizione.

- [ ] **Step 1: Failing test**
```csharp
[WpfFact]
public async Task SaveAsTemplate_creates_custom_template_with_roundtrippable_payload()
{
    var fakeTemplates = new FakeHabitTemplateService();
    var sut = NewSut(templates: fakeTemplates);
    sut.Name = "Flessioni";
    sut.AddFieldCommand.Execute(FieldNature.Count);
    sut.ToggleSeriesCommand.Execute(true);

    await sut.SaveAsTemplateCommand.ExecuteAsync(null);

    var saved = fakeTemplates.Created.Single();
    saved.IsOfficial.Should().BeFalse();
    saved.Name.Should().Be("Flessioni");
    saved.PayloadJson.Should().Contain("Ripetizioni").And.NotBe("{}");
    // round-trip: deserializzando il payload con il TemplatePayload reale, ritrovo 1 metrica + tipo resistance
}
```
> Adatta l'asserzione di round-trip alla forma reale di `TemplatePayload` (deserializza e verifica i campi). `FakeHabitTemplateService.Created` raccoglie i `HabitTemplate` passati a `CreateAsync`.

- [ ] **Step 2: Run — verify FAIL**.

- [ ] **Step 3: Implementation** — implementa `BuildTemplatePayloadJson()` serializzando la composizione corrente nella forma di `TemplatePayload` (nodo: tipo dedotto, nome, colore, icona; metriche: nome/tipo/unità/scala/sortorder; serie-default; modo). Usa `System.Text.Json` con lo stesso modello/opzioni di `HabitTemplateService`.

- [ ] **Step 4: Run — verify PASS**.

- [ ] **Step 5: Commit** · `git commit -m "feat(wpf): save composition as custom template (payload round-trip)"`

---

## Task 13: Picker — sezione "I tuoi template" (rinomina/elimina) + resx

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/HabitTemplatePickerViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitEditor/HabitTemplatePicker.xaml`
- Modify: `src/Recordum.WPF/Resources/Strings.resx` (+ `.it.resx` + `.Designer.cs`)

> I template custom sono quelli con `IsOfficial == false`. `ListAsync` li ritorna già; separali in due gruppi nel VM. Rinomina = `IHabitTemplateService.UpdateAsync(id, t => t.Name = newName)`; elimina = `DeleteAsync(id)` (metodi già esistenti).

- [ ] **Step 1: VM** — in `HabitTemplatePickerViewModel`, dopo `LoadAsync`, separa `OfficialOptions` e `CustomOptions` (custom = template con `IsOfficial==false`). Aggiungi `RenameCustomCommand(option, newName)` e `DeleteCustomCommand(option)` che chiamano `UpdateAsync`/`DeleteAsync` e ricaricano la lista. (Estendi `PickerOption` con `TemplateId`/`IsCustom` se serve per abilitare i comandi solo sui custom.)

- [ ] **Step 2: resx** — aggiungi (EN / IT), dot-notation in resx + proprietà underscore in Designer:
  - `HabitTemplatePicker.FromScratch` = `Build from scratch` / `Costruisci da zero`
  - `HabitTemplatePicker.YourTemplates` = `Your templates` / `I tuoi template`
  - `HabitTemplatePicker.Rename` = `Rename` / `Rinomina`
  - `HabitTemplatePicker.Delete` = `Delete` / `Elimina`
  - `HabitBuilder.Title` = `New habit` / `Nuova abitudine`
  - `HabitBuilder.SaveAsTemplate` = `Save as template` / `Salva come template`
  - `HabitBuilder.Launch` = `Launch` / `Lancia`
  - `HabitBuilder.AddBlock` = `Add block` / `Aggiungi mattone`
  - `HabitBuilder.DerivedType` = `Behaves as: {0}` / `Si comporta come: {0}`

- [ ] **Step 3: XAML** — nel `HabitTemplatePicker.xaml`, rendi l'opzione **"Build from scratch"** ben visibile in cima, e aggiungi la sezione **"I tuoi template"** (lista `CustomOptions` con bottoncini Rinomina/Elimina). Localizza le stringhe del builder usate in Task 10 sostituendo i placeholder con `res:Strings.*`.

- [ ] **Step 4: Build + test** · `dotnet build src/Recordum.WPF` → 0/0; `dotnet test tests/Recordum.WPF.Tests` verde.

- [ ] **Step 5: Commit** · `git commit -m "feat(wpf): custom templates section in picker + i18n"`

---

## Task 14: Verifica finale

- [ ] `dotnet test tests/Recordum.Core.Tests` (o Data, se la grammatica è lì) → verde.
- [ ] `dotnet test tests/Recordum.Data.Tests` → verde.
- [ ] `dotnet test tests/Recordum.WPF.Tests` → verde.
- [ ] `dotnet build src/Recordum.WPF` → 0 errori, 0 warning.
- [ ] **Verifica visiva utente** (skill `run`): Home "+ Nuovo" → "Costruisci da zero" → builder; aggiungi *Ripetizioni*(conteggio) + *Riposo*(tempo) + attiva *Serie 3* → badge "Resistance", anteprima 3 righe × 2 celle; **Lancia** → l'abitudine compare in lista; testo libero blocca conteggio/serie e dà "Journal"; **Salva come template** → compare in "I tuoi template" (rinomina/elimina funzionano); **Annulla** non lascia residui.

## Self-review note (per chi esegue)
- I test usano `FluentAssertions` + `[WpfFact]` (Xunit.StaFact) per il WPF — **adatta agli helper realmente presenti** (`DbFixture`, `NewSut`, i Fake esistenti). I nomi qui sono indicativi.
- **Niente migrazione**: se `dotnet ef` viene invocato, fermarsi (schema già v2.8).
- I `Fake*Service` nuovi (`FakeComposedHabitService`) e gli eventuali campi nuovi sui fake esistenti vanno aggiunti coerentemente, altrimenti il progetto test WPF non compila.
- `TemplatePayload` e il timer-mode di `HabitConfig` sono tipi **esistenti**: leggili prima di serializzare/mappare (Task 9 e 12) — non inventare la forma.
- **Avanzata** e **modo sessione** sono FUORI da questo piano (Spec 3 / Spec 2): non costruirli.
