# U9.1 — Orologio vivo d'astinenza + registrazioni periodi — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere all'astinenza (`Resistance`) un cronometro vivo (secondi che scorrono) in quick-add e Dettaglio, e una lista di registrazioni-periodo nella colonna Cronologia con durata, ricadute ed eliminazione con conferma.

**Architecture:** Nessun cambio schema — la tabella `abstinence_period` (v2.12) è già la fonte di verità. Si aggiunge `IAbstinenceService.DeletePeriodAsync`, un formato cronometro `DurationFormat.FormatStopwatch`, un helper riusabile `LiveElapsedClock` (DispatcherTimer 1s), un'astrazione di conferma `IConfirmationDialog`, e due VM dedicate (`AbstinencePeriodRowViewModel`/`AbstinencePeriodListViewModel`) esposte via `HabitRecordsViewModel` reso type-aware.

**Tech Stack:** C#/.NET 10, EF Core + SQLite, WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions. Spec: `docs/superpowers/specs/2026-07-01-abstinence-clock-and-records-design.md`.

## Global Constraints

- **English-first** per ogni stringa utente. Meccanismo stringhe: chiave puntata in `src/Recordum.WPF/Resources/Strings.resx` (EN) + `Strings.it.resx` (IT) + una riga `public static string X => Get("Key.Path");` in `Strings.Designer.cs`. Nessun code-gen ResX.
- **Mai** `DateTime.Now`/`UtcNow` diretti: tempo via `IClock` (`UtcNow`, `UtcNowMs`, `LocalTimeZone`).
- **DispatcherTimer** creato **solo se** `System.Windows.Application.Current is not null` (guardia test headless), come già fa `AbstinenceQuickAddViewModel`.
- **MVVM + DI** via `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`) e `Microsoft.Extensions.DependencyInjection`.
- **Nessun cambio schema** (niente migrazioni).
- **Build/test**: RID pinnato `win-x64`. Data: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false`; WPF: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false`. Se l'app gira, chiuderla prima (lock su WPF bin).
- **Commit**: uno per task; messaggi terminano con `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Branch corrente `fase-4-polish` (non `main`).

---

### Task 1: Data — `IAbstinenceService.DeletePeriodAsync` (TDD)

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/IAbstinenceService.cs` (add method to interface)
- Modify: `src/Recordum.Data/Services/AbstinenceService.cs` (implement)
- Modify: `tests/Recordum.WPF.Tests/Fixtures/FakeAbstinenceService.cs` (implement new interface member so the WPF test project keeps compiling)
- Test: `tests/Recordum.Data.Tests/Integration/AbstinenceServiceTests.cs` (extend existing)

**Interfaces:**
- Consumes: `RecordumDbContext.AbstinencePeriods`, `IClock`.
- Produces: `Task IAbstinenceService.DeletePeriodAsync(Guid periodId, CancellationToken ct = default)` — hard-deletes one `abstinence_period` row; no-op if absent. Consumed by Task 4 (fake) and Task 7 (row VM).

- [ ] **Step 1: Add the interface method**

In `IAbstinenceService.cs`, after `LapseAsync`:
```csharp
    /// <summary>Hard-deletes one abstinence period by id. No-op if it does not exist. Deleting the
    /// open period reverts the derived state to the previous closed period (Relapsed) or NotStarted.</summary>
    Task DeletePeriodAsync(Guid periodId, CancellationToken ct = default);
```

- [ ] **Step 2: Write the failing tests**

Append to `AbstinenceServiceTests.cs` (mirror the existing test harness in the file for DB + a Resistance `HabitNode` seed + `FakeClock`):
```csharp
[Fact]
public async Task DeletePeriod_removes_a_closed_period_from_history()
{
    // Arrange: start, lapse (1 closed), start again (1 open). Delete the closed one.
    var (svc, db, id, clock) = await CreateAsync();
    await svc.StartAsync(id);
    clock.Advance(TimeSpan.FromDays(1));
    await svc.LapseAsync(id);
    clock.Advance(TimeSpan.FromDays(1));
    await svc.StartAsync(id);
    var closed = await db.AbstinencePeriods.AsNoTracking().FirstAsync(p => p.EndedAtMs != null);

    await svc.DeletePeriodAsync(closed.Id);

    var remaining = await db.AbstinencePeriods.AsNoTracking().ToListAsync();
    remaining.Should().ContainSingle().Which.EndedAtMs.Should().BeNull();
}

[Fact]
public async Task DeletePeriod_of_open_period_reverts_state_to_previous()
{
    // Arrange: closed period, then an open one. Delete the open -> state falls back to Relapsed.
    var (svc, db, id, clock) = await CreateAsync();
    await svc.StartAsync(id);
    clock.Advance(TimeSpan.FromDays(1));
    await svc.LapseAsync(id);
    clock.Advance(TimeSpan.FromDays(1));
    await svc.StartAsync(id);
    var open = await db.AbstinencePeriods.AsNoTracking().FirstAsync(p => p.EndedAtMs == null);

    await svc.DeletePeriodAsync(open.Id);

    var state = await svc.GetStateAsync(id);
    state.Status.Should().Be(AbstinenceStatus.Relapsed);
}

[Fact]
public async Task DeletePeriod_with_unknown_id_is_noop()
{
    var (svc, db, id, _) = await CreateAsync();
    await svc.StartAsync(id);

    await svc.DeletePeriodAsync(Guid.NewGuid()); // does not throw

    (await db.AbstinencePeriods.CountAsync()).Should().Be(1);
}
```
> If the existing tests don't already expose a `CreateAsync()` helper returning `(service, db, habitNodeId, clock)`, follow whatever setup the sibling tests in this file use to obtain those four — the assertions above are the contract, adapt the arrange to the file's harness.

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter AbstinenceServiceTests`
Expected: FAIL (compile error — `DeletePeriodAsync` not implemented).

- [ ] **Step 4: Implement in `AbstinenceService`**

Add to `AbstinenceService.cs`:
```csharp
    public async Task DeletePeriodAsync(Guid periodId, CancellationToken ct = default)
    {
        var period = await db.AbstinencePeriods
            .FirstOrDefaultAsync(p => p.Id == periodId, ct).ConfigureAwait(false);
        if (period is null) return; // guard: unknown id
        db.AbstinencePeriods.Remove(period);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
```

- [ ] **Step 5: Keep the WPF fake compiling**

In `tests/Recordum.WPF.Tests/Fixtures/FakeAbstinenceService.cs`, add:
```csharp
    public Task DeletePeriodAsync(Guid periodId, CancellationToken ct = default)
    {
        foreach (var list in Periods.Values) list.RemoveAll(p => p.Id == periodId);
        return Task.CompletedTask;
    }
```

- [ ] **Step 6: Run to verify pass**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter AbstinenceServiceTests`
Expected: PASS. Then `dotnet build tests/Recordum.WPF.Tests -p:UseAppHost=false` → `Errori: 0`.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.Core/Abstractions/Services/IAbstinenceService.cs src/Recordum.Data/Services/AbstinenceService.cs tests/Recordum.Data.Tests/Integration/AbstinenceServiceTests.cs tests/Recordum.WPF.Tests/Fixtures/FakeAbstinenceService.cs
git commit -m "feat(data): IAbstinenceService.DeletePeriodAsync (hard-delete, state reverts)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: WPF — `DurationFormat.FormatStopwatch` (TDD)

**Files:**
- Modify: `src/Recordum.WPF/Formatting/DurationFormat.cs` (add method)
- Test: `tests/Recordum.WPF.Tests/Formatting/DurationFormatTests.cs` (extend or create — check which exists)

**Interfaces:**
- Produces: `static string DurationFormat.FormatStopwatch(TimeSpan elapsed)` → `"{days}g {h}:{mm:00}:{ss:00}"` when `>= 1` day, else `"{h}:{mm:00}:{ss:00}"`. Consumed by Tasks 5, 6, 7.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData(0, 0, 0, 0, "0:00:00")]
[InlineData(0, 6, 14, 9, "6:14:09")]
[InlineData(0, 0, 3, 7, "0:03:07")]
[InlineData(21, 6, 14, 9, "21g 6:14:09")]
[InlineData(1, 0, 0, 0, "1g 0:00:00")]
public void FormatStopwatch_formats_days_h_mm_ss(int d, int h, int m, int s, string expected)
    => DurationFormat.FormatStopwatch(new TimeSpan(d, h, m, s)).Should().Be(expected);

[Fact]
public void FormatStopwatch_clamps_negative_to_zero()
    => DurationFormat.FormatStopwatch(TimeSpan.FromSeconds(-5)).Should().Be("0:00:00");
```
(Add `using Recordum.WPF.Formatting;` and `using FluentAssertions;` if creating the file; namespace `Recordum.WPF.Tests.Formatting`.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter DurationFormatTests`
Expected: FAIL (compile — `FormatStopwatch` not defined).

- [ ] **Step 3: Implement**

In `DurationFormat.cs`, after `FormatElapsed`:
```csharp
    /// <summary>Live stopwatch format for the abstinence clock: seconds always visible.
    /// "21g 6:14:09" / "6:14:09" / "0:03:07". Days use the same "g" suffix as FormatDays.</summary>
    public static string FormatStopwatch(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        int days = (int)elapsed.TotalDays;
        int h = elapsed.Hours, m = elapsed.Minutes, s = elapsed.Seconds;
        return days >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{days}g {h}:{m:00}:{s:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{h}:{m:00}:{s:00}");
    }
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter DurationFormatTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Formatting/DurationFormat.cs tests/Recordum.WPF.Tests/Formatting/DurationFormatTests.cs
git commit -m "feat(wpf): DurationFormat.FormatStopwatch (live g h:mm:ss)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: WPF — nuove stringhe (EN + IT + Designer)

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`
- Modify: `src/Recordum.WPF/Resources/Strings.it.resx`
- Modify: `src/Recordum.WPF/Resources/Strings.Designer.cs`

**Interfaces:**
- Produces: `Strings.Abstinence_InProgress`, `Abstinence_CleanDays`, `Abstinence_Since`, `Abstinence_DeleteConfirm_Title`, `Abstinence_DeleteConfirm_Message`. Consumed by Tasks 6, 7, 9. (`Abstinence_RelapsedOn` already exists.)

- [ ] **Step 1: Add EN entries**

In `Strings.resx`, after the `Abstinence.NotStarted` data node (~line 751):
```xml
  <data name="Abstinence.InProgress" xml:space="preserve">
    <value>In progress</value>
  </data>
  <data name="Abstinence.CleanDays" xml:space="preserve">
    <value>{0} clean days</value>
  </data>
  <data name="Abstinence.Since" xml:space="preserve">
    <value>since {0}</value>
  </data>
  <data name="Abstinence.DeleteConfirm.Title" xml:space="preserve">
    <value>Delete record</value>
  </data>
  <data name="Abstinence.DeleteConfirm.Message" xml:space="preserve">
    <value>Delete this abstinence record? This cannot be undone.</value>
  </data>
```

- [ ] **Step 2: Add IT entries**

In `Strings.it.resx`, after the `Abstinence.NotStarted` data node (~line 731):
```xml
  <data name="Abstinence.InProgress" xml:space="preserve">
    <value>In corso</value>
  </data>
  <data name="Abstinence.CleanDays" xml:space="preserve">
    <value>{0} giorni puliti</value>
  </data>
  <data name="Abstinence.Since" xml:space="preserve">
    <value>dal {0}</value>
  </data>
  <data name="Abstinence.DeleteConfirm.Title" xml:space="preserve">
    <value>Elimina registrazione</value>
  </data>
  <data name="Abstinence.DeleteConfirm.Message" xml:space="preserve">
    <value>Eliminare questa registrazione d'astinenza? L'operazione non è reversibile.</value>
  </data>
```

- [ ] **Step 3: Add Designer accessors**

In `Strings.Designer.cs`, after the line `public static string Abstinence_NotStarted => Get("Abstinence.NotStarted");` (~line 277):
```csharp
    public static string Abstinence_InProgress => Get("Abstinence.InProgress");
    public static string Abstinence_CleanDays => Get("Abstinence.CleanDays");
    public static string Abstinence_Since => Get("Abstinence.Since");
    public static string Abstinence_DeleteConfirm_Title => Get("Abstinence.DeleteConfirm.Title");
    public static string Abstinence_DeleteConfirm_Message => Get("Abstinence.DeleteConfirm.Message");
```

- [ ] **Step 4: Build**

Run: `dotnet build src/Recordum.WPF -p:UseAppHost=false`
Expected: `Errori: 0`.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "i18n(wpf): abstinence clock + records strings (EN+IT)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: WPF — `IConfirmationDialog` + MessageBox impl + fake + DI

**Files:**
- Create: `src/Recordum.WPF/Services/IConfirmationDialog.cs`
- Create: `src/Recordum.WPF/Services/MessageBoxConfirmationDialog.cs`
- Create: `tests/Recordum.WPF.Tests/Fixtures/FakeConfirmationDialog.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (register)

**Interfaces:**
- Produces: `IConfirmationDialog { Task<bool> ConfirmAsync(string title, string message); }`. Consumed by Tasks 7, 8.
- Produces (test): `FakeConfirmationDialog { bool Result; int Calls; }`.

- [ ] **Step 1: Create the interface**

```csharp
// src/Recordum.WPF/Services/IConfirmationDialog.cs
namespace Recordum.WPF.Services;

/// <summary>Yes/No confirmation prompt. Abstracted so delete flows stay unit-testable.</summary>
public interface IConfirmationDialog
{
    Task<bool> ConfirmAsync(string title, string message);
}
```

- [ ] **Step 2: Create the MessageBox implementation**

```csharp
// src/Recordum.WPF/Services/MessageBoxConfirmationDialog.cs
using System.Windows;

namespace Recordum.WPF.Services;

public sealed class MessageBoxConfirmationDialog : IConfirmationDialog
{
    public Task<bool> ConfirmAsync(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
```

- [ ] **Step 3: Create the test fake**

```csharp
// tests/Recordum.WPF.Tests/Fixtures/FakeConfirmationDialog.cs
using Recordum.WPF.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeConfirmationDialog : IConfirmationDialog
{
    public bool Result { get; set; } = true;
    public int Calls { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}
```

- [ ] **Step 4: Register in DI**

In `ServiceRegistrar.cs`, near the other WPF service singletons (search for an existing `AddSingleton<I...Dialog` or the `IAbstinenceService` registration), add:
```csharp
services.AddSingleton<IConfirmationDialog, MessageBoxConfirmationDialog>();
```
(Add `using Recordum.WPF.Services;` if not already present.)

- [ ] **Step 5: Build**

Run: `dotnet build src/Recordum.WPF -p:UseAppHost=false` and `dotnet build tests/Recordum.WPF.Tests -p:UseAppHost=false`
Expected: `Errori: 0`.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/Services/IConfirmationDialog.cs src/Recordum.WPF/Services/MessageBoxConfirmationDialog.cs tests/Recordum.WPF.Tests/Fixtures/FakeConfirmationDialog.cs src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs
git commit -m "feat(wpf): IConfirmationDialog (MessageBox-backed, testable)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: WPF — `LiveElapsedClock` helper (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/LiveElapsedClock.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/LiveElapsedClockTests.cs`

**Interfaces:**
- Consumes: `IClock`, `DurationFormat.FormatStopwatch` (Task 2).
- Produces: `LiveElapsedClock(IClock) : ObservableObject` with `string Display`, `void Start(long startedAtMs)`, `void Stop()`. Consumed by Tasks 6, 7.

- [ ] **Step 1: Write the failing tests**

```csharp
// Uses the project's FakeClock. In headless tests Application.Current is null,
// so no DispatcherTimer is created; Start() still computes Display once.
[Fact]
public void Start_computes_display_from_started_instant()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
    var startedMs = new DateTimeOffset(2026, 5, 20, 6, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    var sut = new LiveElapsedClock(clock);

    sut.Start(startedMs); // elapsed = 1 day 6 hours

    sut.Display.Should().Be("1g 6:00:00");
}

[Fact]
public void Stop_clears_the_display()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
    var sut = new LiveElapsedClock(clock);
    sut.Start(clock.UtcNowMs);

    sut.Stop();

    sut.Display.Should().BeEmpty();
}
```
(Namespace `Recordum.WPF.Tests.ViewModels.HabitScreen`; `using Recordum.WPF.ViewModels.HabitScreen;` and the fixtures namespace for `FakeClock`.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter LiveElapsedClockTests`
Expected: FAIL (compile — type not defined).

- [ ] **Step 3: Implement**

```csharp
// src/Recordum.WPF/ViewModels/HabitScreen/LiveElapsedClock.cs
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.WPF.Formatting;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>A live-ticking elapsed clock: given a start instant, exposes a FormatStopwatch string
/// refreshed every second. Reusable by the quick-add, the detail streak summary, and the open
/// period row. The DispatcherTimer is created only under a running WPF app (null in headless tests).</summary>
public sealed partial class LiveElapsedClock : ObservableObject
{
    private readonly IClock _clock;
    private readonly DispatcherTimer? _timer;
    private long? _startedMs;

    [ObservableProperty] private string _display = string.Empty;

    public LiveElapsedClock(IClock clock)
    {
        _clock = clock;
        if (System.Windows.Application.Current is not null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => Refresh();
        }
    }

    public void Start(long startedAtMs)
    {
        _startedMs = startedAtMs;
        Refresh();
        _timer?.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _startedMs = null;
        Display = string.Empty;
    }

    private void Refresh()
        => Display = _startedMs is { } s
            ? DurationFormat.FormatStopwatch(_clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(s))
            : string.Empty;
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter LiveElapsedClockTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/LiveElapsedClock.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/LiveElapsedClockTests.cs
git commit -m "feat(wpf): LiveElapsedClock reusable live stopwatch helper

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: WPF — cronometro vivo in quick-add + Dettaglio (TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModel.cs` (use FormatStopwatch; fix the `RelapsedOn` `{0}` bug)
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs` (live current display via `LiveElapsedClock`)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Detail/StreakSummaryViewModelTests.cs` (extend)

**Interfaces:**
- Consumes: `DurationFormat.FormatStopwatch` (Task 2), `LiveElapsedClock` (Task 5), `Strings.Abstinence_RelapsedOn`.

- [ ] **Step 1: Fix the quick-add display**

In `AbstinenceQuickAddViewModel.cs` (keep its existing DispatcherTimer — only two changes):
- `RefreshElapsed` (line ~90): change `DurationFormat.FormatElapsed(...)` to `DurationFormat.FormatStopwatch(...)`.
- `RelapsedOnDisplay` (line ~53): the string `Abstinence.RelapsedOn` is `"Relapsed on {0}"` / `"Ricaduto il {0}"` — it must be formatted, not concatenated (today it renders a literal "{0}"). Replace:
```csharp
    public string RelapsedOnDisplay => RelapsedOn is { } d
        ? string.Format(CultureInfo.CurrentCulture, Strings.Abstinence_RelapsedOn, d.ToString("d", CultureInfo.CurrentCulture))
        : string.Empty;
```

- [ ] **Step 2: Write the failing test for the detail summary**

In `StreakSummaryViewModelTests.cs`, add (follow the file's existing arrange for a Resistance node + `FakeAbstinenceService` + `FakeClock`; the fake's `States[id]` drives `GetStateAsync`). In headless the `LiveElapsedClock` timer is inert but `Start()` computes the display once, so `CurrentDisplay` is deterministic:
```csharp
[Fact]
public async Task Resistance_current_display_is_stopwatch_from_start()
{
    // clock "now" minus 1 day 6h as the abstinence start -> "1g 6:00:00"
    var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
    var startMs = new DateTimeOffset(2026, 5, 20, 6, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    var abstinence = new FakeAbstinenceService();
    var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Resistance, Name = "x" };
    abstinence.States[node.Id] = new AbstinenceState(AbstinenceStatus.Abstaining, startMs, null);
    var sut = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);

    await sut.LoadAsync(node);

    sut.CurrentDisplay.Should().Be("1g 6:00:00");
}

[Fact]
public async Task Resistance_not_abstaining_shows_not_started_and_stops_clock()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
    var abstinence = new FakeAbstinenceService();
    var node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Resistance, Name = "x" };
    abstinence.States[node.Id] = new AbstinenceState(AbstinenceStatus.NotStarted, null, null);
    var sut = new StreakSummaryViewModel(new FakeStreakAggregator(), abstinence, clock);

    await sut.LoadAsync(node);

    sut.CurrentDisplay.Should().Be(Strings.Abstinence_NotStarted);
}
```

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter StreakSummaryViewModelTests`
Expected: FAIL (still `FormatElapsed` → `"1g 6h"`).

- [ ] **Step 4: Edit `StreakSummaryViewModel` to tick live (option B)**

The current class uses a primary constructor. Convert to an explicit constructor so it can own a `LiveElapsedClock` and forward its ticks to `CurrentDisplay`, so the Detail number scrolls each second exactly like the quick-add. Replace the class header + fields:
```csharp
public sealed partial class StreakSummaryViewModel : ObservableObject
{
    private readonly IStreakAggregator _aggregator;
    private readonly IAbstinenceService _abstinence;
    private readonly IClock _clock;
    private readonly LiveElapsedClock _liveClock;

    public StreakSummaryViewModel(IStreakAggregator aggregator, IAbstinenceService abstinence, IClock clock)
    {
        _aggregator = aggregator;
        _abstinence = abstinence;
        _clock = clock;
        _liveClock = new LiveElapsedClock(clock);
        // Forward each 1s tick to the bound CurrentDisplay so the Detail streak number stays live.
        _liveClock.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LiveElapsedClock.Display) && _liveClock.Display.Length > 0)
                CurrentDisplay = _liveClock.Display;
        };
    }
```
Then replace every `aggregator.`→`_aggregator.`, `abstinence.`→`_abstinence.`, `clock.`→`_clock.` in `LoadAsync` (the primary-ctor parameters no longer exist). In the Resistance branch replace the `CurrentDisplay = …FormatElapsed…` assignment with:
```csharp
            if (state.Status == AbstinenceStatus.Abstaining && state.CurrentStartedAtMs is { } sMs)
            {
                _liveClock.Start(sMs);          // computes display now + ticks under a running app
                CurrentDisplay = _liveClock.Display;
            }
            else
            {
                _liveClock.Stop();
                CurrentDisplay = Strings.Abstinence_NotStarted;
            }
```
(Add `using Recordum.WPF.ViewModels.HabitScreen;` for `LiveElapsedClock` if the Detail namespace doesn't already see it.)

- [ ] **Step 5: Run to verify pass + build**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter StreakSummaryViewModelTests`
Expected: PASS. Then `dotnet build src/Recordum.WPF -p:UseAppHost=false` → `Errori: 0` (fix any other `new StreakSummaryViewModel(...)` call site — the ctor signature is unchanged, so none should break).

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Detail/StreakSummaryViewModelTests.cs
git commit -m "feat(wpf): live stopwatch for abstinence clock in quick-add + detail (fix RelapsedOn {0})

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 7: WPF — `AbstinencePeriodRowViewModel` (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/AbstinencePeriodRowViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/AbstinencePeriodRowViewModelTests.cs`

**Interfaces:**
- Consumes: `AbstinencePeriod`, `IAbstinenceService.DeletePeriodAsync` (Task 1), `IConfirmationDialog` (Task 4), `IClock`, `LiveElapsedClock` (Task 5), `Strings.*` (Task 3).
- Produces: `AbstinencePeriodRowViewModel(AbstinencePeriod, IAbstinenceService, IConfirmationDialog, IClock, Action<AbstinencePeriodRowViewModel>? onDeleted)` with `bool IsCurrent`, `Guid Id`, `LiveElapsedClock Clock`, `string DurationDisplay`, `string SinceDisplay`, `string RelapsedOnDisplay`, `DeleteCommand`, `event EventHandler? Changed`. Consumed by Task 8.

- [ ] **Step 1: Write the failing tests**

```csharp
private static AbstinencePeriod Closed(DateTime start, DateTime end) => new()
{
    Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(),
    StartedAtMs = new DateTimeOffset(start, TimeSpan.Zero).ToUnixTimeMilliseconds(),
    EndedAtMs = new DateTimeOffset(end, TimeSpan.Zero).ToUnixTimeMilliseconds(),
};

[Fact]
public void Closed_period_shows_clean_days_and_relapse()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));
    var p = Closed(new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc));
    var sut = new AbstinencePeriodRowViewModel(p, new FakeAbstinenceService(), new FakeConfirmationDialog(), clock);

    sut.IsCurrent.Should().BeFalse();
    sut.DurationDisplay.Should().Be(string.Format(Strings.Abstinence_CleanDays, 3)); // 10,11,12 inclusive
    sut.RelapsedOnDisplay.Should().NotBeEmpty();
}

[Fact]
public void Open_period_is_current_and_clock_runs()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
    var p = new AbstinencePeriod
    {
        Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(),
        StartedAtMs = new DateTimeOffset(new DateTime(2026, 5, 20, 6, 0, 0, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds(),
        EndedAtMs = null,
    };
    var sut = new AbstinencePeriodRowViewModel(p, new FakeAbstinenceService(), new FakeConfirmationDialog(), clock);

    sut.IsCurrent.Should().BeTrue();
    sut.Clock.Display.Should().Be("1g 6:00:00");
}

[Fact]
public async Task Delete_confirmed_calls_service_and_onDeleted_and_raises_changed()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));
    var p = Closed(new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc));
    var abstinence = new FakeAbstinenceService();
    abstinence.Periods[p.HabitNodeId] = new List<AbstinencePeriod> { p };
    var confirm = new FakeConfirmationDialog { Result = true };
    var removed = false; var changed = false;
    var sut = new AbstinencePeriodRowViewModel(p, abstinence, confirm, clock, _ => removed = true);
    sut.Changed += (_, _) => changed = true;

    await sut.DeleteCommand.ExecuteAsync(null);

    confirm.Calls.Should().Be(1);
    abstinence.Periods[p.HabitNodeId].Should().BeEmpty();
    removed.Should().BeTrue();
    changed.Should().BeTrue();
}

[Fact]
public async Task Delete_cancelled_does_nothing()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));
    var p = Closed(new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc));
    var abstinence = new FakeAbstinenceService();
    abstinence.Periods[p.HabitNodeId] = new List<AbstinencePeriod> { p };
    var sut = new AbstinencePeriodRowViewModel(p, abstinence, new FakeConfirmationDialog { Result = false }, clock, _ => { });

    await sut.DeleteCommand.ExecuteAsync(null);

    abstinence.Periods[p.HabitNodeId].Should().ContainSingle();
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter AbstinencePeriodRowViewModelTests`
Expected: FAIL (compile — type not defined).

- [ ] **Step 3: Implement**

```csharp
// src/Recordum.WPF/ViewModels/HabitScreen/AbstinencePeriodRowViewModel.cs
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.WPF.Resources;
using Recordum.WPF.Services;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>One abstinence period as a Cronologia record. Open period = live clock + "since {date}";
/// closed period = "{n} clean days" + "Relapsed on {date}". Delete asks confirmation, then removes.</summary>
public sealed partial class AbstinencePeriodRowViewModel : ObservableObject
{
    private readonly AbstinencePeriod _period;
    private readonly IAbstinenceService _abstinence;
    private readonly IConfirmationDialog _confirm;
    private readonly Action<AbstinencePeriodRowViewModel>? _onDeleted;

    public LiveElapsedClock Clock { get; }
    public bool IsCurrent => _period.EndedAtMs is null;
    public Guid Id => _period.Id;
    public string DurationDisplay { get; }
    public string SinceDisplay { get; }
    public string RelapsedOnDisplay { get; }

    public event EventHandler? Changed;

    public AbstinencePeriodRowViewModel(
        AbstinencePeriod period,
        IAbstinenceService abstinence,
        IConfirmationDialog confirm,
        IClock clock,
        Action<AbstinencePeriodRowViewModel>? onDeleted = null)
    {
        ArgumentNullException.ThrowIfNull(period);
        _period = period;
        _abstinence = abstinence;
        _confirm = confirm;
        _onDeleted = onDeleted;
        Clock = new LiveElapsedClock(clock);

        var start = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(period.StartedAtMs).UtcDateTime);
        if (IsCurrent)
        {
            Clock.Start(period.StartedAtMs);
            DurationDisplay = string.Empty; // shown live via Clock.Display
            SinceDisplay = string.Format(CultureInfo.CurrentCulture, Strings.Abstinence_Since, start.ToString("d", CultureInfo.CurrentCulture));
            RelapsedOnDisplay = string.Empty;
        }
        else
        {
            var end = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(period.EndedAtMs!.Value).UtcDateTime);
            int cleanDays = end.DayNumber - start.DayNumber + 1; // inclusive; pauses not subtracted (see spec)
            DurationDisplay = string.Format(CultureInfo.CurrentCulture, Strings.Abstinence_CleanDays, cleanDays);
            SinceDisplay = $"{start.ToString("d", CultureInfo.CurrentCulture)} → {end.ToString("d", CultureInfo.CurrentCulture)}";
            RelapsedOnDisplay = string.Format(CultureInfo.CurrentCulture, Strings.Abstinence_RelapsedOn, end.ToString("d", CultureInfo.CurrentCulture));
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var ok = await _confirm.ConfirmAsync(
            Strings.Abstinence_DeleteConfirm_Title, Strings.Abstinence_DeleteConfirm_Message).ConfigureAwait(true);
        if (!ok) return;
        Clock.Stop();
        await _abstinence.DeletePeriodAsync(_period.Id).ConfigureAwait(true);
        _onDeleted?.Invoke(this);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter AbstinencePeriodRowViewModelTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/AbstinencePeriodRowViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/AbstinencePeriodRowViewModelTests.cs
git commit -m "feat(wpf): AbstinencePeriodRowViewModel (duration/relapse/delete with confirm)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 8: WPF — `AbstinencePeriodListViewModel` (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/AbstinencePeriodListViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/AbstinencePeriodListViewModelTests.cs`

**Interfaces:**
- Consumes: `IAbstinenceService.ListPeriodsAsync` (existing), `AbstinencePeriodRowViewModel` (Task 7), `IConfirmationDialog`, `IClock`.
- Produces: `AbstinencePeriodListViewModel(IAbstinenceService, IConfirmationDialog, IClock)` with `ObservableCollection<AbstinencePeriodRowViewModel> Rows`, `Task LoadRangeAsync(Guid, DateOnly? from, DateOnly? to, ct)`, `Task LoadDayAsync(Guid, DateOnly, ct)`, `bool IsEmpty`, `event EventHandler? Changed`. Consumed by Task 9.

- [ ] **Step 1: Write the failing tests**

```csharp
private static AbstinencePeriod P(Guid habit, DateTime start, DateTime? end) => new()
{
    Id = Guid.NewGuid(), HabitNodeId = habit,
    StartedAtMs = new DateTimeOffset(start, TimeSpan.Zero).ToUnixTimeMilliseconds(),
    EndedAtMs = end is { } e ? new DateTimeOffset(e, TimeSpan.Zero).ToUnixTimeMilliseconds() : null,
};

[Fact]
public async Task LoadRange_orders_open_first_then_newest()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));
    var habit = Guid.NewGuid();
    var abstinence = new FakeAbstinenceService();
    abstinence.Periods[habit] = new List<AbstinencePeriod>
    {
        P(habit, new DateTime(2026, 5, 1, 0,0,0, DateTimeKind.Utc), new DateTime(2026, 5, 6, 0,0,0, DateTimeKind.Utc)),   // older closed
        P(habit, new DateTime(2026, 5, 10, 0,0,0, DateTimeKind.Utc), new DateTime(2026, 5, 31, 0,0,0, DateTimeKind.Utc)), // newer closed
        P(habit, new DateTime(2026, 6, 10, 0,0,0, DateTimeKind.Utc), null),                                              // open
    };
    var sut = new AbstinencePeriodListViewModel(abstinence, new FakeConfirmationDialog(), clock);

    await sut.LoadRangeAsync(habit, new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 30));

    sut.Rows.Should().HaveCount(3);
    sut.Rows[0].IsCurrent.Should().BeTrue();               // open pinned first
    sut.Rows[1].RelapsedOnDisplay.Should().NotBeEmpty();   // then newest closed
    sut.IsEmpty.Should().BeFalse();
}

[Fact]
public async Task Empty_when_no_periods()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));
    var sut = new AbstinencePeriodListViewModel(new FakeAbstinenceService(), new FakeConfirmationDialog(), clock);

    await sut.LoadRangeAsync(Guid.NewGuid(), null, null);

    sut.Rows.Should().BeEmpty();
    sut.IsEmpty.Should().BeTrue();
}

[Fact]
public async Task Deleting_a_row_removes_it_from_the_list()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));
    var habit = Guid.NewGuid();
    var abstinence = new FakeAbstinenceService();
    abstinence.Periods[habit] = new List<AbstinencePeriod>
    {
        P(habit, new DateTime(2026, 5, 10, 0,0,0, DateTimeKind.Utc), new DateTime(2026, 5, 31, 0,0,0, DateTimeKind.Utc)),
    };
    var sut = new AbstinencePeriodListViewModel(abstinence, new FakeConfirmationDialog { Result = true }, clock);
    await sut.LoadRangeAsync(habit, null, null);

    await sut.Rows[0].DeleteCommand.ExecuteAsync(null);

    sut.Rows.Should().BeEmpty();
}
```
> `ListPeriodsAsync(from, to)` in `FakeAbstinenceService` treats `null`… — the fake takes concrete `DateOnly`. For the null-bounded tests pass `DateOnly.MinValue`/`MaxValue` from the VM when a bound is null (see Step 3), so the fake receives real dates.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter AbstinencePeriodListViewModelTests`
Expected: FAIL (compile — type not defined).

- [ ] **Step 3: Implement**

```csharp
// src/Recordum.WPF/ViewModels/HabitScreen/AbstinencePeriodListViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.Services;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>The Cronologia "Registrazioni" list for a Resistance habit: abstinence periods as rows,
/// open period pinned first then newest-closed first. Deletion (with confirm) removes the row.</summary>
public sealed partial class AbstinencePeriodListViewModel : ObservableObject
{
    private readonly IAbstinenceService _abstinence;
    private readonly IConfirmationDialog _confirm;
    private readonly IClock _clock;

    public ObservableCollection<AbstinencePeriodRowViewModel> Rows { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;

    public event EventHandler? Changed;

    public AbstinencePeriodListViewModel(IAbstinenceService abstinence, IConfirmationDialog confirm, IClock clock)
    {
        _abstinence = abstinence;
        _confirm = confirm;
        _clock = clock;
    }

    public Task LoadDayAsync(Guid habitNodeId, DateOnly day, CancellationToken ct = default)
        => LoadRangeAsync(habitNodeId, day, day, ct);

    public async Task LoadRangeAsync(Guid habitNodeId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var periods = await _abstinence.ListPeriodsAsync(
            habitNodeId, from ?? DateOnly.MinValue, to ?? DateOnly.MaxValue, ct).ConfigureAwait(true);

        Rows.Clear();
        var ordered = periods
            .OrderByDescending(p => p.EndedAtMs is null)          // open first
            .ThenByDescending(p => p.StartedAtMs);                // then newest start
        foreach (var p in ordered)
        {
            var row = new AbstinencePeriodRowViewModel(p, _abstinence, _confirm, _clock, r => Rows.Remove(r));
            row.Changed += (_, _) => { IsEmpty = Rows.Count == 0; Changed?.Invoke(this, EventArgs.Empty); };
            Rows.Add(row);
        }
        IsEmpty = Rows.Count == 0;
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter AbstinencePeriodListViewModelTests`
Expected: PASS (3 tests). (`Rows.Remove` fires the row's `onDeleted`; the row's `Changed` recomputes `IsEmpty`.)

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/AbstinencePeriodListViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/AbstinencePeriodListViewModelTests.cs
git commit -m "feat(wpf): AbstinencePeriodListViewModel (open-first ordering, delete removes row)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 9: WPF — `HabitRecordsViewModel` type-aware + XAML + wiring + visual launch

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitRecordsViewModel.cs` (hold + route to a period list for Resistance; expose `CurrentList` + `IsEmpty`)
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` (construct the period list; pass HabitType into `SetContext`; wire refresh on `Changed`)
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml:1104-1108` (empty-state + `ContentControl` → `Records.CurrentList`; add `AbstinencePeriodListViewModel` DataTemplate)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitRecordsViewModelTests.cs` (extend)

**Interfaces:**
- Consumes: `AbstinencePeriodListViewModel` (Task 8), `CompletionListViewModel` (existing).
- Produces: `HabitRecordsViewModel.CurrentList` (object: `CompletionListViewModel` or `AbstinencePeriodListViewModel`), `HabitRecordsViewModel.IsEmpty`.

- [ ] **Step 1: Read the current `HabitRecordsViewModel` + its ctor call site**

Read `HabitRecordsViewModel.cs` (whole file) and find where `HabitScreenViewModel` builds it and calls `SetContext` (search `new HabitRecordsViewModel` and `.SetContext(`). Note how `IAbstinenceService`, `IConfirmationDialog`, `IClock` are already available in `HabitScreenViewModel` (abstinence + clock are injected; add `IConfirmationDialog` to its ctor if not present, matching the sibling injections).

- [ ] **Step 2: Write the failing test**

In `HabitRecordsViewModelTests.cs` (follow the file's existing construction of the VM; it builds a `CompletionListViewModel` — now also pass an `AbstinencePeriodListViewModel`; see Step 3 for the new ctor):
```csharp
[Fact]
public async Task Resistance_context_routes_records_to_period_list()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));
    var habit = Guid.NewGuid();
    var abstinence = new FakeAbstinenceService();
    abstinence.Periods[habit] = new List<AbstinencePeriod>
    {
        new() { Id = Guid.NewGuid(), HabitNodeId = habit,
                StartedAtMs = new DateTimeOffset(new DateTime(2026,6,10,0,0,0,DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds(),
                EndedAtMs = null },
    };
    var periods = new AbstinencePeriodListViewModel(abstinence, new FakeConfirmationDialog(), clock);
    var completions = /* build the CompletionListViewModel exactly as the existing tests in this file do */;
    var sut = new HabitRecordsViewModel(completions, periods, clock);

    sut.SetContext(habit, isJournal: false, isResistance: true);
    await sut.ApplyPeriodAsync();

    sut.CurrentList.Should().BeSameAs(periods);
    periods.Rows.Should().ContainSingle();
}
```

- [ ] **Step 3: Edit `HabitRecordsViewModel`**

- Add ctor param `AbstinencePeriodListViewModel periods` (after `records`), store it.
- Add fields `bool _isResistance;` and update `SetContext(Guid nodeId, bool isJournal, bool isResistance)` to store `_isResistance` (default `false` overload optional — update the single call site instead).
- Add:
```csharp
    public AbstinencePeriodListViewModel Periods { get; }
    public object CurrentList => _isResistance ? Periods : Records;
    public bool IsEmpty => _isResistance ? Periods.IsEmpty : Records.Rows.Count == 0;
```
- Route the load methods: in `ShowDayAsync` and `ApplyPeriodAsync`, when `_isResistance`, call the period list instead:
```csharp
    // ShowDayAsync body:
    _calendarDay = date;
    if (Mode == EntriesRecordsMode.Giorno && _nodeId is { } id)
    {
        if (_isResistance) await Periods.LoadDayAsync(id, date, ct).ConfigureAwait(true);
        else await Records.LoadDayAsync(id, date, ct).ConfigureAwait(true);
    }
```
```csharp
    // ApplyPeriodAsync body (after computing from,to):
    if (_isResistance) await Periods.LoadRangeAsync(id, from, to, ct).ConfigureAwait(true);
    else await Records.LoadRangeAsync(id, from, to, ct).ConfigureAwait(true);
```
- Raise `OnPropertyChanged(nameof(CurrentList))` and `nameof(IsEmpty)` at the end of `SetContext` and after each load. Subscribe to `Periods.Changed` to re-raise `IsEmpty`.

- [ ] **Step 4: Update the ctor call site + context in `HabitScreenViewModel`**

- Build the `AbstinencePeriodListViewModel` (using the already-injected `_abstinence`, a newly-injected `IConfirmationDialog`, `_clock`) and pass it to `new HabitRecordsViewModel(records, periods, clock)`.
- Change the `SetContext(nodeId, isJournal)` call to `SetContext(nodeId, isJournal, isResistance: node.HabitType == HabitType.Resistance)`.
- Subscribe to the period list's `Changed` (or the records VM's) to trigger the same post-mutation refresh already used after a completion/abstinence action (calendar + summary + home). Mirror the existing `Changed` hook wired for `AbstinenceQuickAddViewModel`.

- [ ] **Step 5: Update the XAML**

In `HabitScreen.xaml`, the records block (~lines 1104-1108):
- Change the empty-state `TextBlock` visibility binding from `Records.Records.Rows.Count` (ZeroToVisible) to `Records.IsEmpty` (BoolToVisible).
- Change `<ContentControl Content="{Binding Records.Records}"/>` to `<ContentControl Content="{Binding Records.CurrentList}"/>`.
- In the same resource dictionary where `<DataTemplate DataType="{x:Type vm:CompletionListViewModel}">` lives (line ~75), add a sibling template for the period list, rendering the righe-scheda layout (option A):
```xml
<DataTemplate DataType="{x:Type vm:AbstinencePeriodListViewModel}">
    <ItemsControl ItemsSource="{Binding Rows}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Margin="0,0,0,8" Padding="12" CornerRadius="8"
                        BorderThickness="1" BorderBrush="{DynamicResource Brush.Border}"
                        Background="{DynamicResource Brush.Surface}">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="4"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <!-- left bar: accent (open) / danger (closed) -->
                        <Border Grid.Column="0" CornerRadius="2" Margin="0,0,10,0">
                            <Border.Style>
                                <Style TargetType="Border">
                                    <Setter Property="Background" Value="{DynamicResource Brush.Danger}"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsCurrent}" Value="True">
                                            <Setter Property="Background" Value="{DynamicResource Brush.Accent}"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Border.Style>
                        </Border>
                        <StackPanel Grid.Column="1">
                            <!-- in-progress badge -->
                            <TextBlock Text="{x:Static res:Strings.Abstinence_InProgress}"
                                       FontSize="{StaticResource FontSize.Small}"
                                       Foreground="{DynamicResource Brush.Accent}"
                                       Visibility="{Binding IsCurrent, Converter={StaticResource BoolToVisible}}"/>
                            <!-- live clock (open) -->
                            <TextBlock Text="{Binding Clock.Display}" FontFamily="Consolas"
                                       FontSize="{StaticResource FontSize.Heading}"
                                       Foreground="{DynamicResource Brush.Text}"
                                       Visibility="{Binding IsCurrent, Converter={StaticResource BoolToVisible}}"/>
                            <!-- clean-days (closed) -->
                            <TextBlock Text="{Binding DurationDisplay}"
                                       FontSize="{StaticResource FontSize.Body}" FontWeight="SemiBold"
                                       Foreground="{DynamicResource Brush.Text}"
                                       Visibility="{Binding IsCurrent, Converter={StaticResource InverseBoolToVisible}}"/>
                            <TextBlock Text="{Binding SinceDisplay}"
                                       FontSize="{StaticResource FontSize.Small}"
                                       Foreground="{DynamicResource Brush.TextMuted}"/>
                            <TextBlock Text="{Binding RelapsedOnDisplay}"
                                       FontSize="{StaticResource FontSize.Small}"
                                       Foreground="{DynamicResource Brush.Danger}"
                                       Visibility="{Binding IsCurrent, Converter={StaticResource InverseBoolToVisible}}"/>
                        </StackPanel>
                        <Button Grid.Column="2" Command="{Binding DeleteCommand}"
                                Style="{StaticResource IconButton}"
                                ToolTip="{x:Static res:Strings.Abstinence_DeleteConfirm_Title}"
                                VerticalAlignment="Top">
                            <TextBlock Text="&#xE74D;" FontFamily="Segoe MDL2 Assets"/>
                        </Button>
                    </Grid>
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</DataTemplate>
```
> Before writing this, confirm the exact resource keys in use elsewhere in this file: `Brush.Surface`, `Brush.Accent`, `InverseBoolToVisible`, `IconButton`, `FontSize.Body`. If a key differs (e.g. no `InverseBoolToVisible`), reuse whatever the file already uses for the same purpose (grep the file) rather than inventing one. The trash glyph `&#xE74D;` is the Segoe MDL2 delete icon; if the app already has a delete icon pattern (search other `DeleteCommand` buttons), reuse it for consistency.

- [ ] **Step 6: Run tests + build**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter HabitRecordsViewModelTests`
Expected: PASS. Then `dotnet build src/Recordum.WPF -p:UseAppHost=false` → `Errori: 0`.

- [ ] **Step 7: Full suite**

Close the app if running, then:
Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false`
Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false`
Expected: all green (baseline Data 367 / WPF 661 + the new tests).

- [ ] **Step 8: Visual launch (human-in-the-loop)**

Use the `run` skill with `--clean`. Verify on a "Stop smoking" habit:
- Quick-add mentre in astinenza: cronometro `Ng h:mm:ss` che **scorre ogni secondo**.
- Dettaglio: streak corrente mostra il formato cronometro.
- Colonna Cronologia → "Registrazioni": riga in corso in cima con orologio vivo; periodi chiusi con "N giorni puliti" + "Ricaduta il …"; cestino → dialog di conferma → la riga sparisce; calendario/summary si aggiornano.

- [ ] **Step 9: Commit + update tracker**

```bash
git add -A
git commit -m "feat(wpf): Cronologia abstinence period records + live clock wiring (U9.1)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```
Then note U9.1 done in `docs/phase-4-issues.md` (append under the U9 entry: live clock + period records + delete) and commit the tracker.

---

## Notes for the executor

- **Read-before-edit** ogni file puntato per `file:line` — i numeri di riga sono del 2026-07-01 e possono spostarsi. Segui il pattern circostante (registrazione DI, posizione dei `DataTemplate` QuickAdd/CompletionList, pipeline di reload di `HabitRecordsViewModel`) più che il numero letterale.
- **Riuso vs invenzione**: per icone, brush e converter nella XAML, **grep il file** e riusa le chiavi già presenti; non introdurre nuove risorse se ne esiste una equivalente.
- **Out of scope** (non costruire): modifica delle date di un periodo, sottrazione pause sub-giornaliere nel cronometro, backfill dei vecchi completamenti in periodi, secondo `DispatcherTimer` in StreakSummary.
