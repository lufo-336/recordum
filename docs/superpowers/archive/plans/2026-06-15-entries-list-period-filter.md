# Lista inserimenti filtrabile per periodo (Feature B) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nella colonna Cronologia dello HabitScreen, rendere la lista registrazioni commutabile Giorno/Periodo, con la modalità Periodo che elenca tutti i completamenti dell'abitudine in un intervallo (Settimana/Mese/Anno/Tutto/Personalizzato).

**Architecture:** Un resolver puro in Core (`EntriesPeriod` + `EntriesPeriodRange`) calcola l'intervallo. `CompletionListViewModel` guadagna un `LoadRangeAsync` (riusa l'overload range del service, già esistente). Un nuovo `HabitRecordsViewModel` incapsula modalità+filtro+lista, alleggerendo `HabitScreenViewModel`. La UI (toggle + ComboBox + date-picker) è additiva sulla colonna Cronologia.

**Tech Stack:** .NET 10, C#, CommunityToolkit.Mvvm, WPF, xUnit + `[WpfFact]` (STA) + FluentAssertions. Nessuna migrazione, nessun cambio a `ICompletionService`.

Spec: `docs/superpowers/archive/specs/2026-06-15-entries-list-period-filter-design.md`.

---

## File Structure

- **Create** `src/Recordum.Core/Enums/EntriesPeriod.cs` — enum dominio del periodo (riusabile da Feature A / web / MAUI).
- **Create** `src/Recordum.Core/Services/EntriesPeriodRange.cs` — resolver puro `Resolve(period, today, customFrom?, customTo?) → (DateOnly? From, DateOnly? To)`. Accanto a `IntensityRampResolver`.
- **Create** `tests/Recordum.Core.Tests/Services/EntriesPeriodRangeTests.cs`.
- **Modify** `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs` — `LoadRangeAsync` + `LoadDayAsync` come wrapper.
- **Modify** `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CompletionListTests.cs` — test del range.
- **Create** `src/Recordum.WPF/ViewModels/HabitScreen/HabitRecordsViewModel.cs` — pannello (enum `EntriesRecordsMode` + VM).
- **Create** `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitRecordsViewModelTests.cs`.
- **Modify** `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` — `Records` rimpiazza `DayCompletions`.
- **Modify** `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` — bind one-line in Task 4; toggle/ComboBox/date-picker additivi in Task 5.
- **Modify** `src/Recordum.WPF/Resources/Strings.resx` + `Strings.it.resx` + `Strings.Designer.cs` — nuove stringhe (Task 5).
- **Create** `src/Recordum.WPF/Converters/EntriesPeriodLabelConverter.cs` — enum periodo → etichetta localizzata per il ComboBox (Task 5).
- **Create** `src/Recordum.WPF/Converters/DateOnlyToDateTimeConverter.cs` — `DateOnly? ↔ DateTime?` per i DatePicker del periodo Personalizzato (Task 5).

---

## Task 1: `EntriesPeriod` + `EntriesPeriodRange` (resolver puro, Core)

**Files:**
- Create: `src/Recordum.Core/Enums/EntriesPeriod.cs`
- Create: `src/Recordum.Core/Services/EntriesPeriodRange.cs`
- Test: `tests/Recordum.Core.Tests/Services/EntriesPeriodRangeTests.cs`

- [ ] **Step 1: Scrivi i test che falliscono**

Crea `tests/Recordum.Core.Tests/Services/EntriesPeriodRangeTests.cs`:

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Services;
using Xunit;

namespace Recordum.Core.Tests.Services;

public class EntriesPeriodRangeTests
{
    // 2026-06-17 è un mercoledì: il lunedì della settimana ISO (15) è ≠ today, così l'arrotondamento è davvero esercitato.
    private static readonly DateOnly Wed = new(2026, 6, 17);

    [Fact]
    public void Settimana_is_iso_week_monday_to_sunday()
    {
        EntriesPeriodRange.Resolve(EntriesPeriod.Settimana, Wed)
            .Should().Be(((DateOnly?)new DateOnly(2026, 6, 15), (DateOnly?)new DateOnly(2026, 6, 21)));
    }

    [Fact]
    public void Mese_is_first_to_last_day_of_current_month()
    {
        EntriesPeriodRange.Resolve(EntriesPeriod.Mese, Wed)
            .Should().Be(((DateOnly?)new DateOnly(2026, 6, 1), (DateOnly?)new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void Anno_is_jan1_to_dec31_of_current_year()
    {
        EntriesPeriodRange.Resolve(EntriesPeriod.Anno, Wed)
            .Should().Be(((DateOnly?)new DateOnly(2026, 1, 1), (DateOnly?)new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Tutto_is_unbounded_both_sides()
    {
        EntriesPeriodRange.Resolve(EntriesPeriod.Tutto, Wed).Should().Be(((DateOnly?)null, (DateOnly?)null));
    }

    [Fact]
    public void Personalizzato_passes_custom_bounds_through()
    {
        var from = new DateOnly(2026, 3, 1);
        var to = new DateOnly(2026, 3, 31);
        EntriesPeriodRange.Resolve(EntriesPeriod.Personalizzato, Wed, from, to)
            .Should().Be(((DateOnly?)from, (DateOnly?)to));
    }

    [Fact]
    public void Personalizzato_keeps_one_side_unbounded_when_null()
    {
        var from = new DateOnly(2026, 3, 1);
        EntriesPeriodRange.Resolve(EntriesPeriod.Personalizzato, Wed, from, null)
            .Should().Be(((DateOnly?)from, (DateOnly?)null));
    }
}
```

- [ ] **Step 2: Esegui, verifica che FALLISCA (compilazione: tipi inesistenti)**

Run: `dotnet test tests/Recordum.Core.Tests/Recordum.Core.Tests.csproj --filter "FullyQualifiedName~EntriesPeriodRangeTests"`
Expected: FAIL in compilazione — `EntriesPeriod`/`EntriesPeriodRange` non esistono.

- [ ] **Step 3: Crea l'enum**

`src/Recordum.Core/Enums/EntriesPeriod.cs`:

```csharp
namespace Recordum.Core.Enums;

/// <summary>Periodo selezionabile per filtrare la lista degli inserimenti di un'abitudine.</summary>
public enum EntriesPeriod { Settimana, Mese, Anno, Tutto, Personalizzato }
```

- [ ] **Step 4: Crea il resolver**

`src/Recordum.Core/Services/EntriesPeriodRange.cs`:

```csharp
using Recordum.Core.Enums;

namespace Recordum.Core.Services;

/// <summary>Risolve un <see cref="EntriesPeriod"/> in un intervallo di date (estremi inclusivi).
/// Semantica "calendario corrente": Settimana = settimana ISO (lun→dom) di <paramref name="today"/>,
/// Mese/Anno = mese/anno di calendario, Tutto = illimitato, Personalizzato = gli estremi passati.
/// Puro: <paramref name="today"/> arriva dal chiamante (via IClock), nessuna dipendenza da clock/UI.</summary>
public static class EntriesPeriodRange
{
    public static (DateOnly? From, DateOnly? To) Resolve(
        EntriesPeriod period, DateOnly today, DateOnly? customFrom = null, DateOnly? customTo = null) => period switch
    {
        EntriesPeriod.Settimana => WeekOf(today),
        EntriesPeriod.Mese => (new DateOnly(today.Year, today.Month, 1),
                               new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),
        EntriesPeriod.Anno => (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
        EntriesPeriod.Personalizzato => (customFrom, customTo),
        _ => (null, null), // Tutto (e fallback difensivo)
    };

    private static (DateOnly?, DateOnly?) WeekOf(DateOnly today)
    {
        // DayOfWeek: Sunday=0..Saturday=6. (+6)%7 mappa Lunedì→0 … Domenica→6 (settimana ISO lun-first).
        int sinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-sinceMonday);
        return (monday, monday.AddDays(6));
    }
}
```

- [ ] **Step 5: Esegui, verifica che PASSI (6 test verdi)**

Run: `dotnet test tests/Recordum.Core.Tests/Recordum.Core.Tests.csproj --filter "FullyQualifiedName~EntriesPeriodRangeTests"`
Expected: PASS (6).

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Core/Enums/EntriesPeriod.cs src/Recordum.Core/Services/EntriesPeriodRange.cs tests/Recordum.Core.Tests/Services/EntriesPeriodRangeTests.cs
git commit -m "feat(core): EntriesPeriod + EntriesPeriodRange resolver puro

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: `CompletionListViewModel.LoadRangeAsync` + `LoadDayAsync` come wrapper

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CompletionListTests.cs`

Contesto: la classe ha già `LoadAsync` (ultime `Take`), `LoadDayAsync` (un giorno), `BuildLookupAsync`, `Populate` (ordina `OrderByDescending(CompletedAtMs)`). `ICompletionService.ListByHabitAsync(habitNodeId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? take, ct)` ha gli estremi **nullable** → `null` = illimitato. `FakeCompletionService` (in `Recordum.WPF.Tests.Fixtures`) filtra per `CompletedAtMs`.

- [ ] **Step 1: Scrivi i test che falliscono**

Aggiungi a `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CompletionListTests.cs` (la classe `CompletionListViewModelTests`), in cima il file ha già `using Recordum.WPF.Tests.Fixtures;` ecc.:

```csharp
[Fact]
public async Task LoadRangeAsync_lists_only_completions_within_range_descending()
{
    var cs = new FakeCompletionService();
    var habit = Guid.NewGuid();
    foreach (var d in new[] { 10, 14, 18, 25 }) // giorni di giugno 2026
    {
        var ts = new DateTime(2026, 6, d, 12, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = ts,
            CompletedAtMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds(),
        };
    }
    var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData());

    await sut.LoadRangeAsync(habit, new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 20));

    sut.Rows.Should().HaveCount(2);
    sut.Rows[0].CompletedAt.Day.Should().Be(18); // desc
    sut.Rows[1].CompletedAt.Day.Should().Be(14);
}

[Fact]
public async Task LoadRangeAsync_with_null_bounds_lists_everything()
{
    var cs = new FakeCompletionService();
    var habit = Guid.NewGuid();
    for (int d = 1; d <= 5; d++)
    {
        var ts = new DateTime(2026, 6, d, 9, 0, 0, DateTimeKind.Utc);
        cs.Store[Guid.NewGuid()] = new Completion
        {
            Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = ts,
            CompletedAtMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds(),
        };
    }
    var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData());

    await sut.LoadRangeAsync(habit, null, null);

    sut.Rows.Should().HaveCount(5);
}
```

- [ ] **Step 2: Esegui, verifica che FALLISCA (compilazione: `LoadRangeAsync` inesistente)**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~CompletionListViewModelTests"`
Expected: FAIL in compilazione.

- [ ] **Step 3: Aggiungi `LoadRangeAsync` e rifattorizza `LoadDayAsync`**

In `CompletionListViewModel.cs`, sostituisci il metodo `LoadDayAsync` esistente (e il suo doc-comment) con questi due metodi:

```csharp
/// <summary>Loads the completions in [from, to] (inclusive; null bound = unbounded that side), each
/// with readable per-field values. Powers both the day panel (from==to) and the period list.</summary>
public async Task LoadRangeAsync(Guid habitNodeId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
{
    NodeId = habitNodeId;
    var fromTs = from is { } f ? new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : (DateTimeOffset?)null;
    var toTs = to is { } t ? new DateTimeOffset(t.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero) : (DateTimeOffset?)null;
    var list = await _completions.ListByHabitAsync(habitNodeId, fromTs, toTs, ct: ct).ConfigureAwait(true);
    var lookup = await BuildLookupAsync(habitNodeId, ct).ConfigureAwait(true);
    Populate(list, lookup);
}

/// <summary>Loads the completions recorded on a single day. Thin wrapper over <see cref="LoadRangeAsync"/>.</summary>
public Task LoadDayAsync(Guid habitNodeId, DateOnly date, CancellationToken ct = default)
    => LoadRangeAsync(habitNodeId, date, date, ct);
```

- [ ] **Step 4: Esegui, verifica che PASSINO (i 2 nuovi + i `LoadDayAsync`/`LoadAsync` esistenti, regressione)**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~CompletionListViewModelTests"`
Expected: PASS (tutti, inclusi i `LoadDayAsync_*` esistenti).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CompletionListTests.cs
git commit -m "feat(wpf): CompletionListViewModel.LoadRangeAsync; LoadDayAsync diventa wrapper

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: `EntriesRecordsMode` + `HabitRecordsViewModel`

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/HabitRecordsViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitRecordsViewModelTests.cs`

Il pannello possiede modalità + filtro + la `CompletionListViewModel` interna. I cambi di proprietà (toggle/ComboBox/date-picker) ricaricano via hook fire-and-forget (UI-thread, coerente con i tanti `async void` del codebase). I test pilotano i **metodi async espliciti** (deterministici) e, per gli hook, asseriscono dopo il set di proprietà: i fake ritornano `ValueTask` già completati, quindi l'`await` su un task completato prosegue **inline** e la lista è popolata prima che il setter ritorni.

- [ ] **Step 1: Scrivi i test che falliscono**

`tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitRecordsViewModelTests.cs`:

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitRecordsViewModelTests
{
    private static (HabitRecordsViewModel sut, FakeCompletionService cs, Guid habit) Build(DateTimeOffset now)
    {
        var cs = new FakeCompletionService();
        var habit = Guid.NewGuid();
        var list = new CompletionListViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData());
        var sut = new HabitRecordsViewModel(list, new FakeClock(now));
        sut.SetContext(habit);
        return (sut, cs, habit);
    }

    private static void Seed(FakeCompletionService cs, Guid habit, params DateTime[] daysUtc)
    {
        foreach (var ts in daysUtc)
            cs.Store[Guid.NewGuid()] = new Completion
            {
                Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = ts,
                CompletedAtMs = new DateTimeOffset(ts, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            };
    }

    [Fact]
    public async Task ShowDayAsync_in_day_mode_lists_only_that_day()
    {
        var (sut, cs, habit) = Build(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        Seed(cs, habit, new DateTime(2026, 6, 17, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 6, 16, 8, 0, 0, DateTimeKind.Utc));

        await sut.ShowDayAsync(new DateOnly(2026, 6, 17));

        sut.Mode.Should().Be(EntriesRecordsMode.Giorno);
        sut.Records.Rows.Should().ContainSingle();
        sut.Records.Rows[0].CompletedAt.Day.Should().Be(17);
    }

    [Fact]
    public void Switching_to_periodo_loads_the_default_month_range()
    {
        var (sut, cs, habit) = Build(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        Seed(cs, habit, new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 6, 28, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 5, 30, 8, 0, 0, DateTimeKind.Utc)); // fuori mese

        sut.Mode = EntriesRecordsMode.Periodo; // default SelectedPeriod = Mese; hook ricarica inline (fake sincrono)

        sut.SelectedPeriod.Should().Be(EntriesPeriod.Mese);
        sut.Records.Rows.Should().HaveCount(2); // solo i due di giugno
    }

    [Fact]
    public void Custom_period_uses_custom_bounds()
    {
        var (sut, cs, habit) = Build(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        Seed(cs, habit, new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 6, 25, 8, 0, 0, DateTimeKind.Utc));

        sut.Mode = EntriesRecordsMode.Periodo;
        sut.CustomFrom = new DateOnly(2026, 6, 1);
        sut.CustomTo = new DateOnly(2026, 6, 10);
        sut.SelectedPeriod = EntriesPeriod.Personalizzato; // ricarica con gli estremi custom

        sut.Records.Rows.Should().ContainSingle();
        sut.Records.Rows[0].CompletedAt.Day.Should().Be(5);
    }

    [Fact]
    public async Task ReloadAsync_reruns_current_mode_and_day_returns_after_periodo()
    {
        var (sut, cs, habit) = Build(new DateTimeOffset(2026, 6, 17, 0, 0, 0, TimeSpan.Zero));
        Seed(cs, habit, new DateTime(2026, 6, 17, 8, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc));
        await sut.ShowDayAsync(new DateOnly(2026, 6, 17)); // memorizza il giorno

        sut.Mode = EntriesRecordsMode.Periodo;  // mostra il mese (2 righe)
        sut.Records.Rows.Should().HaveCount(2);

        sut.Mode = EntriesRecordsMode.Giorno;   // torna al giorno memorizzato (1 riga)
        sut.Records.Rows.Should().ContainSingle();
        sut.Records.Rows[0].CompletedAt.Day.Should().Be(17);
    }
}
```

- [ ] **Step 2: Esegui, verifica che FALLISCA (compilazione)**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~HabitRecordsViewModelTests"`
Expected: FAIL in compilazione — `HabitRecordsViewModel`/`EntriesRecordsMode` non esistono.

- [ ] **Step 3: Crea `HabitRecordsViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Services;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>Modalità della lista registrazioni: il giorno selezionato sul calendario, o un periodo.</summary>
public enum EntriesRecordsMode { Giorno, Periodo }

/// <summary>Incapsula il pannello "Registrazioni" della colonna Cronologia: modalità (Giorno/Periodo),
/// filtro periodo e la <see cref="CompletionListViewModel"/> interna. Tiene il concern fuori dalla
/// già-grande HabitScreenViewModel.</summary>
public sealed partial class HabitRecordsViewModel : ObservableObject
{
    private readonly IClock _clock;
    private Guid? _nodeId;
    private DateOnly? _calendarDay;

    public CompletionListViewModel Records { get; }

    [ObservableProperty] private EntriesRecordsMode _mode = EntriesRecordsMode.Giorno;
    [ObservableProperty] private EntriesPeriod _selectedPeriod = EntriesPeriod.Mese;
    [ObservableProperty] private DateOnly? _customFrom;
    [ObservableProperty] private DateOnly? _customTo;

    public HabitRecordsViewModel(CompletionListViewModel records, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(clock);
        Records = records;
        _clock = clock;
    }

    public void SetContext(Guid nodeId) => _nodeId = nodeId;

    /// <summary>Registra il giorno scelto sul calendario; ricarica la lista solo se si è in modalità Giorno.</summary>
    public async Task ShowDayAsync(DateOnly date, CancellationToken ct = default)
    {
        _calendarDay = date;
        if (Mode == EntriesRecordsMode.Giorno && _nodeId is { } id)
            await Records.LoadDayAsync(id, date, ct).ConfigureAwait(true);
    }

    /// <summary>Carica i completamenti del periodo selezionato (today da IClock, fuso locale).</summary>
    public async Task ApplyPeriodAsync(CancellationToken ct = default)
    {
        if (_nodeId is not { } id) return;
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, _clock.LocalTimeZone));
        var (from, to) = EntriesPeriodRange.Resolve(SelectedPeriod, today, CustomFrom, CustomTo);
        await Records.LoadRangeAsync(id, from, to, ct).ConfigureAwait(true);
    }

    /// <summary>Rilancia la modalità corrente (usato dopo un completamento).</summary>
    public Task ReloadAsync(CancellationToken ct = default) =>
        Mode == EntriesRecordsMode.Giorno
            ? (_calendarDay is { } d ? ShowDayAsync(d, ct) : Task.CompletedTask)
            : ApplyPeriodAsync(ct);

    partial void OnModeChanged(EntriesRecordsMode value) => _ = ReloadAsync();
    partial void OnSelectedPeriodChanged(EntriesPeriod value) { if (Mode == EntriesRecordsMode.Periodo) _ = ApplyPeriodAsync(); }
    partial void OnCustomFromChanged(DateOnly? value) => ReloadIfCustom();
    partial void OnCustomToChanged(DateOnly? value) => ReloadIfCustom();

    private void ReloadIfCustom()
    {
        if (Mode == EntriesRecordsMode.Periodo && SelectedPeriod == EntriesPeriod.Personalizzato)
            _ = ApplyPeriodAsync();
    }
}
```

- [ ] **Step 4: Esegui, verifica che PASSINO (4 test verdi)**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~HabitRecordsViewModelTests"`
Expected: PASS (4).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/HabitRecordsViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitRecordsViewModelTests.cs
git commit -m "feat(wpf): HabitRecordsViewModel — toggle Giorno/Periodo + filtro periodo

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Cablaggio in `HabitScreenViewModel` (Records rimpiazza DayCompletions)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (solo il bind, una riga)

Refactor che **preserva il comportamento** (resta in modalità Giorno guidata dal calendario): introduce `Records` e instrada calendario/refresh attraverso di esso. Il toggle UI è additivo nel Task 5. Rete di sicurezza: build + suite WPF verdi.

- [ ] **Step 1: Sostituisci la proprietà `DayCompletions` con `Records`**

Trova (≈ riga 252):
```csharp
    public CompletionListViewModel DayCompletions { get; }
```
Sostituisci con:
```csharp
    public HabitRecordsViewModel Records { get; }
```

- [ ] **Step 2: Costruiscilo nel ctor**

Trova (≈ riga 312):
```csharp
        DayCompletions = new CompletionListViewModel(completions, metrics, refData);
```
Sostituisci con:
```csharp
        Records = new HabitRecordsViewModel(new CompletionListViewModel(completions, metrics, refData), clock);
```

- [ ] **Step 3: Imposta il contesto e il giorno iniziale nel load**

Trova (≈ righe 393-399):
```csharp
        DayNote.SetContext(node.Id);
        if (Calendar.SelectedDate is { } sel)
        {
            await DayNote.LoadDayAsync(sel, ct).ConfigureAwait(true);
            await PushDaySummaryAsync(sel, ct).ConfigureAwait(true);
            await DayCompletions.LoadDayAsync(node.Id, sel, ct).ConfigureAwait(true);
        }
```
Sostituisci con:
```csharp
        DayNote.SetContext(node.Id);
        Records.SetContext(node.Id);
        if (Calendar.SelectedDate is { } sel)
        {
            await DayNote.LoadDayAsync(sel, ct).ConfigureAwait(true);
            await PushDaySummaryAsync(sel, ct).ConfigureAwait(true);
            await Records.ShowDayAsync(sel, ct).ConfigureAwait(true);
        }
```

- [ ] **Step 4: Refresh post-completamento**

Trova (≈ righe 447-454):
```csharp
        var dayReload = Calendar.SelectedDate is { } sel
            ? DayCompletions.LoadDayAsync(Node.Id, sel)
            : Task.CompletedTask;
        await Task.WhenAll(
            CompletionList.LoadAsync(Node.Id),
            dayReload,
            Calendar.JumpAsync())
            .ConfigureAwait(true);
```
Sostituisci con:
```csharp
        await Task.WhenAll(
            CompletionList.LoadAsync(Node.Id),
            Records.ReloadAsync(),
            Calendar.JumpAsync())
            .ConfigureAwait(true);
```

- [ ] **Step 5: Selezione giorno sul calendario**

Trova (≈ riga 565):
```csharp
        if (Node is { } n) await DayCompletions.LoadDayAsync(n.Id, date).ConfigureAwait(true);
```
Sostituisci con:
```csharp
        if (Node is not null) await Records.ShowDayAsync(date).ConfigureAwait(true);
```

- [ ] **Step 6: Aggiorna il bind XAML (una riga + il placeholder)**

In `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`, trova (≈ righe 864-868):
```xml
                                            <TextBlock Text="{x:Static res:Strings.HabitScreen_DayRecords_Empty}"
                                                       Foreground="{DynamicResource Brush.TextMuted}"
                                                       FontSize="{StaticResource FontSize.Small}"
                                                       Visibility="{Binding DayCompletions.Rows.Count, Converter={StaticResource ZeroToVisible}}"/>
                                            <ContentControl Content="{Binding DayCompletions}"/>
```
Sostituisci i due binding `DayCompletions` con `Records.Records`:
```xml
                                            <TextBlock Text="{x:Static res:Strings.HabitScreen_DayRecords_Empty}"
                                                       Foreground="{DynamicResource Brush.TextMuted}"
                                                       FontSize="{StaticResource FontSize.Small}"
                                                       Visibility="{Binding Records.Records.Rows.Count, Converter={StaticResource ZeroToVisible}}"/>
                                            <ContentControl Content="{Binding Records.Records}"/>
```

- [ ] **Step 7: Cerca riferimenti residui a `DayCompletions`**

Run: usa lo strumento di ricerca per `DayCompletions` in `src/`. Atteso: **nessun** risultato (né in .cs né in .xaml). Se ne resta uno, sistemalo prima di procedere.

- [ ] **Step 8: Build + suite WPF completa**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: 0 errori, 0 warning nuovi.

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj`
Expected: PASS — tutti verdi (i precedenti + i nuovi). Il comportamento è invariato (lista del giorno guidata dal calendario).

- [ ] **Step 9: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml
git commit -m "refactor(wpf): HabitScreenViewModel usa HabitRecordsViewModel (comportamento invariato)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: UI toggle Giorno/Periodo + ComboBox periodo + date-picker + i18n

**Files:**
- Create: `src/Recordum.WPF/Converters/EntriesPeriodLabelConverter.cs`
- Create: `src/Recordum.WPF/Converters/DateOnlyToDateTimeConverter.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`
- Modify: `src/Recordum.WPF/App.xaml` (registra i due converter + l'ObjectDataProvider dei valori enum)

Additivo: aggiunge i controlli sopra la lista, legati alle proprietà già presenti su `Records`. Converter `EnumToBoolConverter`/`EnumToVisibilityConverter` **esistono già** in `Converters/` e sono registrati in `App.xaml` (verificalo; se un converter non è tra le risorse, aggiungilo). Verifica visiva nel Task 6.

- [ ] **Step 1: Aggiungi le stringhe i18n**

In `src/Recordum.WPF/Resources/Strings.resx` aggiungi (valori inglesi):

```xml
  <data name="HabitScreen_Records" xml:space="preserve"><value>Records</value></data>
  <data name="Records_Mode_Day" xml:space="preserve"><value>Day</value></data>
  <data name="Records_Mode_Period" xml:space="preserve"><value>Period</value></data>
  <data name="Period_Week" xml:space="preserve"><value>Week</value></data>
  <data name="Period_Month" xml:space="preserve"><value>Month</value></data>
  <data name="Period_Year" xml:space="preserve"><value>Year</value></data>
  <data name="Period_All" xml:space="preserve"><value>All</value></data>
  <data name="Period_Custom" xml:space="preserve"><value>Custom</value></data>
  <data name="Period_From" xml:space="preserve"><value>From</value></data>
  <data name="Period_To" xml:space="preserve"><value>To</value></data>
```

In `src/Recordum.WPF/Resources/Strings.it.resx` aggiungi gli stessi `name` con valori italiani: `Registrazioni`, `Giorno`, `Periodo`, `Settimana`, `Mese`, `Anno`, `Tutto`, `Personalizzato`, `Da`, `A`.

In `src/Recordum.WPF/Resources/Strings.Designer.cs` aggiungi le 10 proprietà statiche corrispondenti (stesso pattern delle esistenti, es. `HabitScreen_DayRecords`). Esempio per una:

```csharp
public static string Records_Mode_Day => ResourceManager.GetString("Records_Mode_Day", resourceCulture);
```

(Replica per tutte e 10 le chiavi, seguendo lo stile già presente nel Designer.)

- [ ] **Step 2: Crea i due converter (etichetta periodo + DateOnly↔DateTime)**

`src/Recordum.WPF/Converters/EntriesPeriodLabelConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;
using Recordum.Core.Enums;
using Recordum.WPF.Resources;

namespace Recordum.WPF.Converters;

/// <summary>EntriesPeriod → etichetta localizzata per il ComboBox del filtro periodo.</summary>
public sealed class EntriesPeriodLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EntriesPeriod.Settimana => Strings.Period_Week,
        EntriesPeriod.Mese => Strings.Period_Month,
        EntriesPeriod.Anno => Strings.Period_Year,
        EntriesPeriod.Tutto => Strings.Period_All,
        EntriesPeriod.Personalizzato => Strings.Period_Custom,
        _ => string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
```

`src/Recordum.WPF/Converters/DateOnlyToDateTimeConverter.cs` (NON esiste in `Converters/` — va creato; serve perché `DatePicker.SelectedDate` è `DateTime?` mentre `CustomFrom/To` sono `DateOnly?`):

```csharp
using System.Globalization;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary><c>DateOnly?</c> ↔ <c>DateTime?</c> per legare un DatePicker a una proprietà DateOnly.</summary>
public sealed class DateOnlyToDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? DateOnly.FromDateTime(dt) : null;
}
```

- [ ] **Step 3: Registra le risorse in `App.xaml`**

I namespace `xmlns:conv` (Converters), `xmlns:sys` (System), `xmlns:enums` (Core.Enums) sono **già** nell'header di `App.xaml`, e i converter `EnumToBool`/`EnumToVisibility`/`ZeroToVisible` sono già registrati. Aggiungi, accanto agli altri converter (dopo `BoolToCheckMark`):

```xml
<conv:EntriesPeriodLabelConverter x:Key="EntriesPeriodLabel"/>
<conv:DateOnlyToDateTimeConverter x:Key="DateOnlyToDateTime"/>
```

e, accanto agli altri `ObjectDataProvider` (es. `ScheduleTypeValues`), il provider dei valori dell'enum periodo:

```xml
<ObjectDataProvider x:Key="EntriesPeriodValues" MethodName="GetValues" ObjectType="{x:Type sys:Enum}">
    <ObjectDataProvider.MethodParameters>
        <x:Type TypeName="enums:EntriesPeriod"/>
    </ObjectDataProvider.MethodParameters>
</ObjectDataProvider>
```

- [ ] **Step 4: Aggiungi i controlli nella colonna Cronologia**

Premessa: nell'header di `HabitScreen.xaml`, `xmlns:vm` è già mappato a `Recordum.WPF.ViewModels.HabitScreen` (usato a riga 74 per `vm:CompletionListViewModel`), quindi `vm:EntriesRecordsMode` risolve. **Aggiungi** `xmlns:enums="clr-namespace:Recordum.Core.Enums;assembly=Recordum.Core"` all'header se non presente (serve per `enums:EntriesPeriod`).

I `ConverterParameter` di `EnumToBool`/`EnumToVisibility` devono essere enum **tipizzati** via `{x:Static …}` (il converter fa `value.Equals(parameter)`: una stringa non eguaglierebbe mai l'enum). Pattern identico a `SettingsWindow.xaml` (ThemeMode, two-way).

In `HabitScreen.xaml`, sostituisci il blocco header "Registrazioni del giorno" (≈ righe 859-863, il `TextBlock` con `HabitScreen_DayRecords`) con il blocco toggle+filtro. Risultato del blocco `<StackPanel Margin="12,4,12,12">`:

```xml
                                        <StackPanel Margin="12,4,12,12">
                                            <DockPanel Margin="0,0,0,6">
                                                <TextBlock Text="{x:Static res:Strings.HabitScreen_Records}"
                                                           FontWeight="SemiBold"
                                                           Foreground="{DynamicResource Brush.Text}"
                                                           VerticalAlignment="Center"/>
                                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                                                    <RadioButton Content="{x:Static res:Strings.Records_Mode_Day}"
                                                                 GroupName="RecordsMode"
                                                                 IsChecked="{Binding Records.Mode, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:EntriesRecordsMode.Giorno}}"/>
                                                    <RadioButton Content="{x:Static res:Strings.Records_Mode_Period}"
                                                                 GroupName="RecordsMode" Margin="8,0,0,0"
                                                                 IsChecked="{Binding Records.Mode, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:EntriesRecordsMode.Periodo}}"/>
                                                </StackPanel>
                                            </DockPanel>
                                            <StackPanel Orientation="Horizontal" Margin="0,0,0,6"
                                                        Visibility="{Binding Records.Mode, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static vm:EntriesRecordsMode.Periodo}}">
                                                <ComboBox ItemsSource="{Binding Source={StaticResource EntriesPeriodValues}}"
                                                          SelectedItem="{Binding Records.SelectedPeriod}"
                                                          MinWidth="120">
                                                    <ComboBox.ItemTemplate>
                                                        <DataTemplate>
                                                            <TextBlock Text="{Binding Converter={StaticResource EntriesPeriodLabel}}"/>
                                                        </DataTemplate>
                                                    </ComboBox.ItemTemplate>
                                                </ComboBox>
                                                <StackPanel Orientation="Horizontal" Margin="8,0,0,0"
                                                            Visibility="{Binding Records.SelectedPeriod, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static enums:EntriesPeriod.Personalizzato}}">
                                                    <TextBlock Text="{x:Static res:Strings.Period_From}" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                                    <DatePicker SelectedDate="{Binding Records.CustomFrom, Converter={StaticResource DateOnlyToDateTime}}"/>
                                                    <TextBlock Text="{x:Static res:Strings.Period_To}" VerticalAlignment="Center" Margin="6,0,4,0"/>
                                                    <DatePicker SelectedDate="{Binding Records.CustomTo, Converter={StaticResource DateOnlyToDateTime}}"/>
                                                </StackPanel>
                                            </StackPanel>
                                            <TextBlock Text="{x:Static res:Strings.HabitScreen_DayRecords_Empty}"
                                                       Foreground="{DynamicResource Brush.TextMuted}"
                                                       FontSize="{StaticResource FontSize.Small}"
                                                       Visibility="{Binding Records.Records.Rows.Count, Converter={StaticResource ZeroToVisible}}"/>
                                            <ContentControl Content="{Binding Records.Records}"/>
                                        </StackPanel>
```

Nota: il converter `DateOnlyToDateTime` è quello creato nello Step 2 e registrato nello Step 3.

- [ ] **Step 5: Build**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: 0 errori. Le `x:Key` `EnumToBool`, `EnumToVisibility`, `ZeroToVisible` sono confermate in `App.xaml`; `EntriesPeriodLabel`, `DateOnlyToDateTime`, `EntriesPeriodValues` le hai aggiunte negli Step 2-3. Se un `StaticResource` non si risolve, è una di queste chiavi o un namespace mancante nell'header XAML.

- [ ] **Step 6: Suite WPF (regressione XAML/binding)**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj`
Expected: PASS — tutti verdi.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml src/Recordum.WPF/Converters/ src/Recordum.WPF/App.xaml src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "feat(wpf): UI toggle Giorno/Periodo + filtro periodo nella colonna Cronologia

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: Verifica visiva + aggiornamento tracker

**Files:**
- Modify: `docs/phase-4-issues.md`

Checkpoint umano: il comportamento UI si valida a vista.

- [ ] **Step 1: Avvia l'app**

Usa la skill `run` (build + launch). Apri un'abitudine con storico (es. "Diario" o "Niente fumo"), colonna **Cronologia**.

- [ ] **Step 2: Checklist visiva** (tema scuro e chiaro)

1. Default **Giorno**: la lista mostra le registrazioni del giorno selezionato sul calendario, **come prima** (nessuna regressione).
2. Toggle su **Periodo**: compare il ComboBox; default **Mese** → elenca i completamenti del mese corrente, indipendente dal giorno del calendario.
3. Cambia preset **Settimana / Anno / Tutto** → la lista si aggiorna all'intervallo atteso.
4. **Personalizzato** → compaiono i date-picker Da/A; impostando un intervallo la lista si filtra; `Da > A` ⇒ lista vuota (nessun crash).
5. Selezionando un giorno sul calendario **mentre si è in Periodo**: la lista NON cambia (resta per-periodo); QuickAdd/DayNote seguono il giorno.
6. Tornando a **Giorno**: riappare il giorno selezionato.
7. Registrando un nuovo completamento: la lista si aggiorna nella modalità corrente.
8. Le righe restano editabili (nota) ed eliminabili come prima.

- [ ] **Step 3: Aggiorna il tracker**

In `docs/phase-4-issues.md`, aggiungi una voce per la Feature B (lista inserimenti per-periodo) con stato `fixed`→`verified-fixed`, i commit SHA dei Task 1-5, e l'esito della checklist. Annota che la **Feature A** (tab Note cronologia pagine+note) resta il ciclo successivo aperto.

- [ ] **Step 4: Commit**

```bash
git add docs/phase-4-issues.md
git commit -m "docs: tracker — Feature B lista inserimenti per-periodo verificata

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Note di esecuzione

- **Ordine**: 1 → 6. Task 2 dipende da niente (LoadRangeAsync è autonomo). Task 3 dipende da 1+2. Task 4 dipende da 3. Task 5 dipende da 4. Ogni task lascia build+suite verdi.
- **STA**: i test puri (`EntriesPeriodRange`, `LoadRangeAsync`, `HabitRecordsViewModel`) sono `[Fact]` (i fake sono sincroni). Solo i test che istanziano visual reali usano `[WpfFact]`.
- **Nessuna migrazione**, nessun cambio a `ICompletionService`/schema.
- **Estensibilità**: "altri filtri" futuri = nuove proprietà su `HabitRecordsViewModel` composte nel load, senza toccare `CompletionListViewModel`.
- Suite di riferimento da tenere verde: Core / Data / WPF attuali + i nuovi test.
```
