# U9 — Astinenza a stato esplicito — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire il modello d'astinenza "continuo" delle abitudini `Resistance` con un modello a **stato esplicito** (Inizio/Ricaduta) basato su periodi persistiti, con cronometro vivo e azioni chiare in abitudine + Home.

**Architecture:** Nuova tabella `abstinence_period` come unica fonte di verità (un periodo = un tentativo, `start` + `end` opzionale). Un `IAbstinenceService` apre/chiude periodi e ne deriva lo stato (NotStarted/Abstaining/Relapsed). Il motore streak, il riepilogo, il quick-add, la riga Home e il calendario leggono dai periodi. Migrazione additiva v2.12.

**Tech Stack:** C#/.NET 10, EF Core + SQLite (snake_case via EFCore.NamingConventions), WPF + CommunityToolkit.Mvvm, xUnit. Spec: `docs/superpowers/specs/2026-06-28-abstinence-explicit-state-design.md`.

## Global Constraints

- **English-first** per ogni stringa utente: 3 edit a mano per stringa — `src/Recordum.WPF/Resources/Strings.resx` + `Strings.it.resx` + proprietà in `Strings.Designer.cs` (nessun code-gen ResX).
- **Mai** `DateTime.Now`/`UtcNow` diretti: tempo via `IClock` (`UtcNow`, `UtcNowMs`, `LocalTimeZone`).
- **Cascade rules + indici sempre in Fluent API esplicita** (mai default EF). snake_case su tabelle/colonne (gestito da EFCore.NamingConventions; usare nomi PascalCase nelle proprietà).
- **Migrazioni** via `dotnet ef migrations add <Name> --project src/Recordum.Data --startup-project src/Recordum.WPF`; verificare il SQL generato contro la spec prima di `database update`. **Non** editare a mano i file `*.Designer.cs`/`*ModelSnapshot.cs` (hook li blocca) — rigenerali con `dotnet ef`.
- **Provenance**: `abstinence_period` è evento-figlio → **niente** `created_by_plugin_id`.
- **Build/test**: RID pinnato `win-x64`. Test data: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false`; WPF: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false`. Se l'app gira, chiuderla prima (lock su WPF bin).
- **Commit**: frequenti, uno per task; messaggi terminano con `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Branch corrente `fase-4-polish` (non `main`).

---

### Task 1: Core — modello dominio e contratto del service

**Files:**
- Create: `src/Recordum.Core/Models/AbstinencePeriod.cs`
- Create: `src/Recordum.Core/Abstractions/Services/IAbstinenceService.cs`

**Interfaces:**
- Produces: `AbstinencePeriod` (entity), `enum AbstinenceStatus { NotStarted, Abstaining, Relapsed }`, `record AbstinenceState(AbstinenceStatus Status, long? CurrentStartedAtMs, long? LastEndedAtMs)`, `IAbstinenceService { GetStateAsync, StartAsync, LapseAsync, ListPeriodsAsync }`.

- [ ] **Step 1: Create the entity**

```csharp
// src/Recordum.Core/Models/AbstinencePeriod.cs
namespace Recordum.Core.Models;

/// <summary>One abstinence attempt for a Resistance habit: opens on "Inizio" (StartedAtMs) and
/// closes on "Ricaduta" (EndedAtMs). A null EndedAtMs is the single currently-open period. The gap
/// between one period's end and the next start is "relapsed, not yet resumed" — not a clean run.</summary>
public sealed class AbstinencePeriod
{
    public Guid Id { get; set; }
    public Guid HabitNodeId { get; set; }
    public long StartedAtMs { get; set; }
    public long? EndedAtMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public HabitNode HabitNode { get; set; } = null!;
}
```

- [ ] **Step 2: Create the service contract**

```csharp
// src/Recordum.Core/Abstractions/Services/IAbstinenceService.cs
using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public enum AbstinenceStatus { NotStarted, Abstaining, Relapsed }

/// <summary>Snapshot of a Resistance habit's abstinence state. CurrentStartedAtMs is set only while
/// Abstaining; LastEndedAtMs is the most recent relapse instant (set while Relapsed).</summary>
public sealed record AbstinenceState(AbstinenceStatus Status, long? CurrentStartedAtMs, long? LastEndedAtMs);

public interface IAbstinenceService
{
    Task<AbstinenceState> GetStateAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Opens a new abstinence period (now). No-op if one is already open.</summary>
    Task StartAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Closes the open period (now = the relapse day). No-op if none is open.</summary>
    Task LapseAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Periods that intersect [from, to] (by day), newest-relevant order not guaranteed.</summary>
    Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(
        Guid habitNodeId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build Core**

Run: `dotnet build src/Recordum.Core -p:UseAppHost=false`
Expected: `Errori: 0`.

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.Core/Models/AbstinencePeriod.cs src/Recordum.Core/Abstractions/Services/IAbstinenceService.cs
git commit -m "feat(core): AbstinencePeriod model + IAbstinenceService contract

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Data — EF mapping, DbSet, migration v2.12 (+ CR8 seed)

**Files:**
- Create: `src/Recordum.Data/Configurations/AbstinencePeriodConfiguration.cs`
- Modify: `src/Recordum.Data/RecordumDbContext.cs` (add DbSet near line 36)
- Modify: `src/Recordum.Data/Seed/AppSettingSeed.cs` (CR8: schema_version constant → "2.12")
- Create (via `dotnet ef`): `src/Recordum.Data/Migrations/<ts>_AbstinencePeriodV212.cs` + Designer + updated `RecordumDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: `AbstinencePeriod` (Task 1).
- Produces: table `abstinence_period`; `RecordumDbContext.AbstinencePeriods`.

- [ ] **Step 1: Write the EF configuration**

Mirror `BackupLogConfiguration.cs` style; cascade + index explicit.

```csharp
// src/Recordum.Data/Configurations/AbstinencePeriodConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class AbstinencePeriodConfiguration : IEntityTypeConfiguration<AbstinencePeriod>
{
    public void Configure(EntityTypeBuilder<AbstinencePeriod> builder)
    {
        builder.ToTable("abstinence_period");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartedAtMs).IsRequired();
        builder.Property(x => x.EndedAtMs);
        builder.Property(x => x.CreatedAt).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.UpdatedAtMs).IsRequired();

        builder.HasOne(x => x.HabitNode)
               .WithMany()
               .HasForeignKey(x => x.HabitNodeId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.HabitNodeId, x.StartedAtMs })
               .HasDatabaseName("ix_abstinence_period_habit_started");
    }
}
```

- [ ] **Step 2: Register the DbSet**

In `src/Recordum.Data/RecordumDbContext.cs`, add after the `BackupLogs` line (currently line 35):

```csharp
    public DbSet<AbstinencePeriod> AbstinencePeriods => Set<AbstinencePeriod>();
```

(Configs are auto-discovered by `ApplyConfigurationsFromAssembly`, so no further wiring.)

- [ ] **Step 3: CR8 — bump the seed constant**

Open `src/Recordum.Data/Seed/AppSettingSeed.cs`, find the `schema_version` seed value (currently `"2.9"`) and change it to `"2.12"`. (This is the constant `HasData` seed; the live DB value is bumped by the migration in Step 4. Both must match — that's the CR8 fix.)

- [ ] **Step 4: Generate the migration**

Run:
```bash
dotnet ef migrations add AbstinencePeriodV212 --project src/Recordum.Data --startup-project src/Recordum.WPF
```
Then open the generated `Up(...)`: it must `CreateTable("abstinence_period", ...)` with the FK (cascade) + `CreateIndex("ix_abstinence_period_habit_started", ...)`. **Manually add** the schema-version bump at the end of `Up` (mirror `NoteCategoryV211`), and the inverse in `Down`:

```csharp
// end of Up(...)
migrationBuilder.UpdateData(
    table: "app_settings",
    keyColumn: "key",
    keyValue: "schema_version",
    columns: new[] { "updated_at", "value" },
    values: new object[] { new DateTime(2026, 6, 28, 0, 0, 0, 0, DateTimeKind.Utc), "2.12" });
```
```csharp
// end of Down(...)
migrationBuilder.UpdateData(
    table: "app_settings",
    keyColumn: "key",
    keyValue: "schema_version",
    columns: new[] { "updated_at", "value" },
    values: new object[] { new DateTime(2026, 6, 19, 0, 0, 0, 0, DateTimeKind.Utc), "2.11" });
```

Verify the generated SQL: `dotnet ef migrations script 20260619154318_NoteCategoryV211 AbstinencePeriodV212 --project src/Recordum.Data --startup-project src/Recordum.WPF` — confirm a single additive `CREATE TABLE abstinence_period` + index, no drops/renames on other tables.

- [ ] **Step 5: Build + apply to a scratch DB**

Run: `dotnet build src/Recordum.Data -p:UseAppHost=false`
Expected: `Errori: 0`. (DB update happens at app startup via `ApplicationInitializer`; no manual `database update` needed.)

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Data/Configurations/AbstinencePeriodConfiguration.cs src/Recordum.Data/RecordumDbContext.cs src/Recordum.Data/Seed/AppSettingSeed.cs src/Recordum.Data/Migrations/
git commit -m "feat(data): abstinence_period table + AbstinencePeriodV212 (schema 2.12, closes CR8)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Data — `AbstinenceService` (TDD)

**Files:**
- Create: `src/Recordum.Data/Services/AbstinenceService.cs`
- Test: `tests/Recordum.Data.Tests/Integration/AbstinenceServiceTests.cs`

**Interfaces:**
- Consumes: `IAbstinenceService` (Task 1), `RecordumDbContext.AbstinencePeriods` (Task 2), `IClock`.
- Produces: `AbstinenceService` (registered DI in Task 9).

Look at an existing Data service + its test for the DbContext-factory/clock test harness (e.g. `tests/Recordum.Data.Tests/Integration/` siblings). Use the same in-memory/SQLite test base the other integration tests use, and a `FakeClock` (search the test project for the existing fake).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Recordum.Data.Tests/Integration/AbstinenceServiceTests.cs
// (Follow the existing integration-test base in this folder for DB + a Resistance HabitNode seed +
//  the project's FakeClock. The four behaviors under test:)
[Fact] public async Task Start_when_none_open_opens_a_period() { /* GetStateAsync -> Abstaining, CurrentStartedAtMs == clock.UtcNowMs */ }
[Fact] public async Task Start_when_already_open_is_noop() { /* two StartAsync -> exactly one open period */ }
[Fact] public async Task Lapse_when_open_closes_the_period() { /* EndedAtMs set; GetStateAsync -> Relapsed, LastEndedAtMs == close ms */ }
[Fact] public async Task Lapse_when_none_open_is_noop() { /* no period created; state stays NotStarted */ }
[Fact] public async Task Start_lapse_start_yields_two_periods_second_open() { /* ListPeriodsAsync count == 2, one open */ }
[Fact] public async Task GetState_with_no_periods_is_NotStarted() { }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter AbstinenceServiceTests`
Expected: FAIL (compile error — `AbstinenceService` not defined).

- [ ] **Step 3: Implement the service**

```csharp
// src/Recordum.Data/Services/AbstinenceService.cs
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class AbstinenceService(RecordumDbContext db, IClock clock) : IAbstinenceService
{
    public async Task<AbstinenceState> GetStateAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        var open = await db.AbstinencePeriods.AsNoTracking()
            .Where(p => p.HabitNodeId == habitNodeId && p.EndedAtMs == null)
            .OrderByDescending(p => p.StartedAtMs).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (open is not null)
            return new AbstinenceState(AbstinenceStatus.Abstaining, open.StartedAtMs, null);

        var lastClosed = await db.AbstinencePeriods.AsNoTracking()
            .Where(p => p.HabitNodeId == habitNodeId && p.EndedAtMs != null)
            .OrderByDescending(p => p.EndedAtMs).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return lastClosed is null
            ? new AbstinenceState(AbstinenceStatus.NotStarted, null, null)
            : new AbstinenceState(AbstinenceStatus.Relapsed, null, lastClosed.EndedAtMs);
    }

    public async Task StartAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        var hasOpen = await db.AbstinencePeriods
            .AnyAsync(p => p.HabitNodeId == habitNodeId && p.EndedAtMs == null, ct).ConfigureAwait(false);
        if (hasOpen) return; // guard: at most one open period

        db.AbstinencePeriods.Add(new AbstinencePeriod
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitNodeId,
            StartedAtMs = clock.UtcNowMs,
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task LapseAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        var open = await db.AbstinencePeriods
            .Where(p => p.HabitNodeId == habitNodeId && p.EndedAtMs == null)
            .OrderByDescending(p => p.StartedAtMs).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (open is null) return; // guard

        open.EndedAtMs = clock.UtcNowMs;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(
        Guid habitNodeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var fromMs = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var toMs = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        return await db.AbstinencePeriods.AsNoTracking()
            .Where(p => p.HabitNodeId == habitNodeId
                        && p.StartedAtMs <= toMs
                        && (p.EndedAtMs == null || p.EndedAtMs >= fromMs))
            .OrderBy(p => p.StartedAtMs)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
```

(`CreatedAt`/`UpdatedAt`/`UpdatedAtMs` are populated by `TimestampInterceptor`.)

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter AbstinenceServiceTests`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.Data/Services/AbstinenceService.cs tests/Recordum.Data.Tests/Integration/AbstinenceServiceTests.cs
git commit -m "feat(data): AbstinenceService (start/lapse/state/list with guards)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Engine — aggregator reads periods (TDD)

**Files:**
- Modify: `src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs:9-23` (ctor: add `IAbstinenceService abstinence`) and `:168-217` (replace `ComputeAbstinenceAsync` body)
- Test: `tests/Recordum.Data.Tests/Integration/Evaluators/AbstinenceAggregatorTests.cs`

**Interfaces:**
- Consumes: `IAbstinenceService.ListPeriodsAsync` (Task 3), `IStreakPauseService`, `IClock`, `StreakRun`.
- Produces: Resistance branch of `IStreakAggregator.GetHistoryAsync` now period-based.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Recordum.Data.Tests/Integration/Evaluators/AbstinenceAggregatorTests.cs
// (Seed a Resistance HabitNode + abstinence_period rows directly; FakeClock fixes "today".)
[Fact] public async Task Open_period_is_the_current_clean_run() { /* one run, IsCurrent, Length = days start..today */ }
[Fact] public async Task Closed_period_is_a_historic_run_with_Lapse() { /* run BreakReason == Lapse, End == period end day */ }
[Fact] public async Task Gap_between_lapse_and_restart_is_not_counted() { /* two runs; days in the gap excluded from both lengths */ }
[Fact] public async Task No_periods_yields_no_runs() { /* NotStarted -> Array.Empty */ }
[Fact] public async Task Pause_inside_a_period_freezes_length() { /* paused days don't add to Length, run not split */ }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false --filter AbstinenceAggregatorTests`
Expected: FAIL.

- [ ] **Step 3: Add the dependency to the ctor**

In `SchedulingAwareStreakAggregator` primary constructor (line 9-23), add a parameter before `IClock clock`:

```csharp
    IAbstinenceService abstinence,
    IClock clock) : IStreakAggregator
```

- [ ] **Step 4: Replace `ComputeAbstinenceAsync` (lines 165-217)**

```csharp
    // Abstinence (Resistance): clean runs come from abstinence_period rows. Each period [start, end]
    // (or [start, today] if open) is a clean run; the gap between a period's end and the next start is
    // "relapsed, not resumed" and is not counted. Pauses freeze a run (neither clean nor break). The
    // open period is the current run. Schedule/mode/recovery do not apply. See spec 2026-06-28.
    private async ValueTask<IReadOnlyList<StreakRun>> ComputeAbstinenceAsync(
        HabitNode habitNode, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var createdDate = DateOnly.FromDateTime(habitNode.CreatedAt);
        var lifeStart = createdDate < fromDate ? createdDate : fromDate; // full life for true lengths (CR11)
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var periods = await abstinence.ListPeriodsAsync(habitNode.Id, lifeStart, toDate, ct).ConfigureAwait(false);
        if (periods.Count == 0) return Array.Empty<StreakRun>();

        var pauseList = await pauses.ListByHabitAsync(habitNode.Id, ct).ConfigureAwait(false);
        bool IsPaused(DateOnly d) => pauseList.Any(p => d >= p.PauseFrom && d <= p.PauseTo);

        int CleanDays(DateOnly s, DateOnly e)
        {
            var n = 0;
            for (var d = s; d <= e; d = d.AddDays(1))
                if (!IsPaused(d)) n++;
            return n;
        }

        var runs = new List<StreakRun>();
        foreach (var p in periods)
        {
            var startDay = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(p.StartedAtMs).UtcDateTime);
            var isOpen = p.EndedAtMs is null;
            var endDay = isOpen
                ? today
                : DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(p.EndedAtMs!.Value).UtcDateTime);
            if (startDay > toDate) continue;
            if (endDay < startDay) endDay = startDay;

            var length = CleanDays(startDay, endDay);
            runs.Add(new StreakRun(startDay, endDay, length, isOpen, 0, isOpen ? null : StreakBreakReason.Lapse));
        }

        return runs.Where(r => r.End >= fromDate).ToList();
    }
```

- [ ] **Step 5: Run to verify pass + no regression**

Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false`
Expected: PASS (new tests green; existing streak tests unchanged — the non-Resistance path is untouched).

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Data/Services/Aggregators/SchedulingAwareStreakAggregator.cs tests/Recordum.Data.Tests/Integration/Evaluators/AbstinenceAggregatorTests.cs
git commit -m "feat(data): abstinence streak from periods (gap excluded, pause freezes)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: WPF — `Brush.Danger` token (Dark + Light)

**Files:**
- Modify: `src/Recordum.WPF/Resources/Dark.xaml` (after `Brush.Success`)
- Modify: `src/Recordum.WPF/Resources/Light.xaml` (matching key)

**Interfaces:**
- Produces: `DynamicResource Brush.Danger` (consumed by Tasks 8/10).

- [ ] **Step 1: Add the brush to both themes**

In `Dark.xaml`, after the `Brush.Success` line:
```xml
    <SolidColorBrush x:Key="Brush.Danger"             Color="#FFF87171"/>
```
In `Light.xaml`, after its `Brush.Success` line (pick the light-theme red used elsewhere; if none, use):
```xml
    <SolidColorBrush x:Key="Brush.Danger"             Color="#FFDC2626"/>
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Recordum.WPF -p:UseAppHost=false`
Expected: `Errori: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/Recordum.WPF/Resources/Dark.xaml src/Recordum.WPF/Resources/Light.xaml
git commit -m "feat(wpf): Brush.Danger token (relapse/emergency color)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: WPF — user strings (.resx ×3)

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`

**Interfaces:**
- Produces: `Strings.Abstinence_Start`, `Abstinence_Lapse`, `Abstinence_CleanFor`, `Abstinence_RelapsedOn`, `Abstinence_NotStarted`, `Abstinence_Lapses90` (note: a `Detail_Streak_Lapses90` already exists and is reused by StreakSummary — only add what's missing).

- [ ] **Step 1: Add the entries**

In `Strings.resx` (English) add data nodes:
| name | value (EN) |
|---|---|
| `Abstinence_Start` | `Start` |
| `Abstinence_Lapse` | `Relapse` |
| `Abstinence_CleanFor` | `Clean for` |
| `Abstinence_RelapsedOn` | `Relapsed on {0}` |
| `Abstinence_NotStarted` | `Not started` |

In `Strings.it.resx` (Italian):
| name | value (IT) |
|---|---|
| `Abstinence_Start` | `Inizio` |
| `Abstinence_Lapse` | `Ricaduta` |
| `Abstinence_CleanFor` | `Pulito da` |
| `Abstinence_RelapsedOn` | `Ricaduto il {0}` |
| `Abstinence_NotStarted` | `Non avviata` |

- [ ] **Step 2: Add the Designer properties**

In `Strings.Designer.cs`, add one static property per key (mirror an existing string property exactly):

```csharp
public static string Abstinence_Start => ResourceManager.GetString("Abstinence_Start", resourceCulture);
public static string Abstinence_Lapse => ResourceManager.GetString("Abstinence_Lapse", resourceCulture);
public static string Abstinence_CleanFor => ResourceManager.GetString("Abstinence_CleanFor", resourceCulture);
public static string Abstinence_RelapsedOn => ResourceManager.GetString("Abstinence_RelapsedOn", resourceCulture);
public static string Abstinence_NotStarted => ResourceManager.GetString("Abstinence_NotStarted", resourceCulture);
```
(Match the exact accessor shape used by neighboring properties in the file.)

- [ ] **Step 3: Build**

Run: `dotnet build src/Recordum.WPF -p:UseAppHost=false`
Expected: `Errori: 0`.

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "i18n(wpf): abstinence strings (EN+IT)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 7: WPF — `StreakSummaryViewModel` reads abstinence state (TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs:12-13` (ctor + `IAbstinenceService`) and `:45-59` (Resistance branch)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Detail/StreakSummaryViewModelTests.cs` (extend or create)

**Interfaces:**
- Consumes: `IAbstinenceService.GetStateAsync` + `ListPeriodsAsync`, `DurationFormat.FormatElapsed`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Resistance, Abstaining: CurrentDisplay == FormatElapsed(now - currentStart); RunsCaption == Lapses90.
[Fact] public async Task Resistance_abstaining_shows_elapsed_from_current_start() { }
// Resistance, Relapsed/NotStarted: CurrentDisplay == Strings.Abstinence_NotStarted (or "non in astinenza" choice).
[Fact] public async Task Resistance_not_abstaining_shows_not_started_state() { }
// Lapses(90d) == number of periods closed in the last 90 days.
[Fact] public async Task Resistance_lapses90_counts_closed_periods_in_window() { }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter StreakSummaryViewModelTests`
Expected: FAIL (compile — ctor arity changed / new behavior).

- [ ] **Step 3: Edit the ViewModel**

Change the primary ctor (line 12-13) to inject the service:
```csharp
public sealed partial class StreakSummaryViewModel(
    IStreakAggregator aggregator, ICompletionService completions, IAbstinenceService abstinence, IClock clock) : ObservableObject
```
Replace the Resistance branch (lines 45-59) with:
```csharp
        if (node.HabitType == HabitType.Resistance)
        {
            RunsCaption = Strings.Detail_Streak_Lapses90;
            var state = await abstinence.GetStateAsync(node.Id, ct).ConfigureAwait(true);
            CurrentDisplay = state.Status == AbstinenceStatus.Abstaining && state.CurrentStartedAtMs is { } sMs
                ? DurationFormat.FormatElapsed(clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(sMs))
                : Strings.Abstinence_NotStarted;

            var periods90 = await abstinence.ListPeriodsAsync(node.Id, today.AddDays(-89), today, ct).ConfigureAwait(true);
            RunsLast90Days = periods90.Count(p => p.EndedAtMs is { } e
                && DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(e).UtcDateTime) >= today.AddDays(-89));
        }
```
(Add `using Recordum.Core.Abstractions.Services;` if not present.)

- [ ] **Step 4: Run to verify pass + fix other call sites**

Run: `dotnet build src/Recordum.WPF -p:UseAppHost=false` — fix any place that constructs `StreakSummaryViewModel` (DI registration / tests) to pass a fake/real `IAbstinenceService`.
Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter StreakSummaryViewModelTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Detail/StreakSummaryViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Detail/StreakSummaryViewModelTests.cs
git commit -m "feat(wpf): streak summary reads abstinence state (3 states + lapses 90d)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 8: WPF — `AbstinenceQuickAddViewModel` + view + routing (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs:518` (routing) — and any quick-add VM factory deps
- Create: a view/`DataTemplate` for the VM where the quick-add `ContentControl` resolves its templates (find the existing per-type quick-add `DataTemplate`s — they live with the other QuickAdd VMs' views; add one keyed to `AbstinenceQuickAddViewModel`)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModelTests.cs`

**Interfaces:**
- Consumes: `IAbstinenceService`, `IClock`, `DurationFormat.FormatElapsed`.
- Produces: `AbstinenceQuickAddViewModel(IAbstinenceService, IClock)` with `LoadAsync(Guid habitNodeId)`, `Status`, `ElapsedDisplay`, `StartCommand`, `LapseCommand`, and an `event EventHandler? Changed` raised after an action (for host refresh).

- [ ] **Step 1: Write the failing tests**

```csharp
// LoadAsync on a NotStarted habit -> Status NotStarted, StartCommand can execute, LapseCommand cannot.
[Fact] public async Task NotStarted_enables_start_only() { }
// After StartCommand -> Status Abstaining, ElapsedDisplay non-empty, Changed raised.
[Fact] public async Task Start_transitions_to_abstaining_and_raises_changed() { }
// While Abstaining, LapseCommand -> Status Relapsed, Changed raised.
[Fact] public async Task Lapse_transitions_to_relapsed() { }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter AbstinenceQuickAddViewModelTests`
Expected: FAIL.

- [ ] **Step 3: Implement the ViewModel**

```csharp
// src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModel.cs
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.Formatting;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

public sealed partial class AbstinenceQuickAddViewModel : ObservableObject
{
    private readonly IAbstinenceService _abstinence;
    private readonly IClock _clock;
    private readonly DispatcherTimer? _tick;
    private Guid _habitNodeId;
    private long? _startedMs;

    public AbstinenceQuickAddViewModel(IAbstinenceService abstinence, IClock clock)
    {
        _abstinence = abstinence;
        _clock = clock;
        // 1s tick keeps the elapsed alive; FormatElapsed shows two units so long durations stay calm.
        if (System.Windows.Application.Current is not null)
        {
            _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _tick.Tick += (_, _) => RefreshElapsed();
        }
    }

    [ObservableProperty] private AbstinenceStatus _status = AbstinenceStatus.NotStarted;
    [ObservableProperty] private string _elapsedDisplay = string.Empty;
    [ObservableProperty] private DateOnly? _relapsedOn;

    public bool CanStart => Status != AbstinenceStatus.Abstaining;
    public bool CanLapse => Status == AbstinenceStatus.Abstaining;

    public event EventHandler? Changed;

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        _habitNodeId = habitNodeId;
        await ReloadAsync(ct).ConfigureAwait(true);
    }

    private async Task ReloadAsync(CancellationToken ct = default)
    {
        var state = await _abstinence.GetStateAsync(_habitNodeId, ct).ConfigureAwait(true);
        Status = state.Status;
        _startedMs = state.CurrentStartedAtMs;
        RelapsedOn = state.LastEndedAtMs is { } e
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(e).UtcDateTime)
            : null;
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanLapse));
        RefreshElapsed();
        if (Status == AbstinenceStatus.Abstaining) _tick?.Start(); else _tick?.Stop();
    }

    private void RefreshElapsed()
        => ElapsedDisplay = _startedMs is { } s
            ? DurationFormat.FormatElapsed(_clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(s))
            : string.Empty;

    [RelayCommand]
    private async Task StartAsync()
    {
        await _abstinence.StartAsync(_habitNodeId).ConfigureAwait(true);
        await ReloadAsync().ConfigureAwait(true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task LapseAsync()
    {
        await _abstinence.LapseAsync(_habitNodeId).ConfigureAwait(true);
        await ReloadAsync().ConfigureAwait(true);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 4: Route Resistance to the new VM**

In `HabitScreenViewModel.cs` (line ~518), replace:
```csharp
HabitType.Resistance => new MeasuredQuickAddViewModel(_completions, _metrics, _refData, _clock, _timers),
```
with:
```csharp
HabitType.Resistance => new AbstinenceQuickAddViewModel(_abstinence, _clock),
```
Add `IAbstinenceService _abstinence` to `HabitScreenViewModel`'s constructor + field (follow how `_completions` etc. are injected). Subscribe to the new VM's `Changed` to trigger the same post-mutation refresh used after a completion (calendar/summary/Home), mirroring the existing refresh hook.

- [ ] **Step 5: Add the view DataTemplate**

Find the resource dictionary / view file where the other quick-add VMs get their `DataTemplate` (search: `MeasuredQuickAddViewModel` in `**/*.xaml`). Add a `DataTemplate DataType="{x:Type vm:AbstinenceQuickAddViewModel}"` with: a large `ElapsedDisplay` caption (prefixed with `Strings.Abstinence_CleanFor` when Abstaining), a primary `Button` bound to `LapseCommand` (`Strings.Abstinence_Lapse`, visible when `CanLapse`) and a `Button` bound to `StartCommand` (`Strings.Abstinence_Start`, visible when `CanStart`); when Relapsed show `Strings.Abstinence_RelapsedOn` formatted with `RelapsedOn`. Use `BoolToVisible` converter (already registered).

- [ ] **Step 6: Run tests + build**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter AbstinenceQuickAddViewModelTests`
Expected: PASS. Then `dotnet build src/Recordum.WPF -p:UseAppHost=false` → `Errori: 0`.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/AbstinenceQuickAddViewModelTests.cs src/Recordum.WPF
git commit -m "feat(wpf): abstinence quick-add (Inizio/Ricaduta + live timer)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 9: WPF — Home row quick action + DI registration (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/Quick/AbstinenceQuickActionHandler.cs`
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (register `IAbstinenceService` + the new handler)
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs:~668` (Resistance Home magnitude → abstinence state/time)
- Test: `tests/Recordum.WPF.Tests/ViewModels/Quick/AbstinenceQuickActionHandlerTests.cs`

**Interfaces:**
- Consumes: `IAbstinenceService`, `IQuickActionHandler`.
- Produces: contextual Home action (Start when not abstaining, Lapse when abstaining).

- [ ] **Step 1: Write the failing test**

```csharp
// Not abstaining -> ExecuteAsync calls StartAsync, returns true.
[Fact] public async Task Execute_when_not_abstaining_starts() { }
// Abstaining -> ExecuteAsync calls LapseAsync, returns true.
[Fact] public async Task Execute_when_abstaining_lapses() { }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter AbstinenceQuickActionHandlerTests`
Expected: FAIL.

- [ ] **Step 3: Implement the handler**

```csharp
// src/Recordum.WPF/ViewModels/Quick/AbstinenceQuickActionHandler.cs
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels.Quick;

public sealed class AbstinenceQuickActionHandler(IAbstinenceService abstinence) : IQuickActionHandler
{
    public HabitType HabitType => HabitType.Resistance;

    public async ValueTask<bool> ExecuteAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        var state = await abstinence.GetStateAsync(habitNodeId, ct).ConfigureAwait(false);
        if (state.Status == AbstinenceStatus.Abstaining)
            await abstinence.LapseAsync(habitNodeId, ct).ConfigureAwait(false);
        else
            await abstinence.StartAsync(habitNodeId, ct).ConfigureAwait(false);
        return true;
    }
}
```

- [ ] **Step 4: Register DI**

In `ServiceRegistrar.cs`, register the service (scoped, with the other Data services) and the handler (alongside the other `IQuickActionHandler` registrations — search `CounterQuickActionHandler`):
```csharp
services.AddScoped<IAbstinenceService, AbstinenceService>();
services.AddSingleton<IQuickActionHandler, AbstinenceQuickActionHandler>();
```
(Match the lifetime the sibling handlers/services use — copy the exact `Add...` call style next to them.)

- [ ] **Step 5: Home row magnitude → abstinence**

In `HabitScreenViewModel.cs` (~line 668), the Resistance arm of the magnitude/summary formatting must show the abstinence elapsed/state instead of a completion count. Replace the `HabitType.Resistance => mag > 1 ? FormatMag(mag) : null,` arm so a Resistance row shows the elapsed clean time (via `IAbstinenceService.GetStateAsync` + `DurationFormat.FormatElapsed`) or empty when not abstaining. (Keep it async-safe with the existing row-load path.)

- [ ] **Step 6: Run tests + build**

Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false --filter AbstinenceQuickActionHandlerTests`
Expected: PASS. Then `dotnet build src/Recordum.WPF -p:UseAppHost=false`.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Quick/AbstinenceQuickActionHandler.cs src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Quick/AbstinenceQuickActionHandlerTests.cs
git commit -m "feat(wpf): abstinence Home quick action + DI wiring

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 10: WPF — calendar colorizer for Resistance

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml.cs` (or the VM that fills the day cells — find where `CalendarDayCellViewModel.TintColorHex`/`TintStep`/`Number` are computed from completion data)
- Modify: `src/Recordum.WPF/Views/HabitScreen/Calendar/CalendarNavigator.xaml` only if a new state needs a brush binding

**Interfaces:**
- Consumes: `IAbstinenceService.ListPeriodsAsync`, `Brush.Danger` (Task 5).

- [ ] **Step 1: Locate the day-fill source**

Find where the calendar builds day cells for a habit (search `CalendarDayCellViewModel` construction / `GetHeatmapAsync` usage in the calendar VM). Confirm the per-day tint pipeline.

- [ ] **Step 2: Add a Resistance branch**

When the habit is `Resistance`, build the visible-month day fills from `ListPeriodsAsync(from, to)` instead of the completion heatmap:
- day inside a period (`start <= day <= end/today`, not paused) → clean tint (existing habit-color "on" treatment),
- day == a period's end (lapse) → `Brush.Danger`,
- day in a gap (after an end, before the next start) → `Brush.Danger` at low opacity,
- day before the first start → empty (`Brush.HeatmapEmpty`).
Keep the non-Resistance path untouched (branch on `HabitType`). Isolate the Resistance mapping in its own private method.

- [ ] **Step 3: Build + visual check**

Run: `dotnet build src/Recordum.WPF -p:UseAppHost=false` → `Errori: 0`. (Visual verification deferred to Task 11 launch.)

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Views/HabitScreen/Calendar/
git commit -m "feat(wpf): calendar colors Resistance days from abstinence periods

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 11: Demo seeder realignment + full suite + visual launch

**Files:**
- Modify: the demo seeder (`DemoDataSeeder`) "Niente fumo" Resistance path — search `Niente fumo` / `DemoDataSeeder`
- No test file (seeder is dev tooling); covered by the suite + manual launch.

- [ ] **Step 1: Realign the demo seed**

Replace the old "ricadute-as-completions" seeding for "Niente fumo" with `abstinence_period` rows: a couple of closed periods + one **open** period started ~3 weeks ago, so a `--seed-demo` run shows a live clean timer (~3 weeks) + a few relapse markers. Use the injected `IClock` for timestamps.

- [ ] **Step 2: Full suite**

Close the app if running, then:
Run: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false`
Run: `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false`
Expected: all green (new tests included; baseline was Data 356 / WPF 651 + the new tests).

- [ ] **Step 3: Visual launch (human-in-the-loop)**

Use the `run` skill with `--clean` (the local DB has pre-period Resistance data; a clean slate verifies the new flow). Verify: create a "Stop smoking" → NotStarted shows **Inizio**; press Inizio → live timer + **Ricaduta**; press Ricaduta → "Ricaduto il …" + **Inizio**; calendar shows a danger relapse day; Home row shows contextual button + elapsed.

- [ ] **Step 4: Commit + update tracker**

```bash
git add -A
git commit -m "feat: realign Niente fumo demo seed to abstinence periods + close U9

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```
Then mark U9 done in `docs/phase-4-issues.md` (status `verified-fixed` after the visual launch passes) and commit the tracker.

---

## Notes for the executor

- **Read-before-edit** the integration files this plan points at by `file:line` — the line numbers are from 2026-06-28 and may drift. Match the surrounding pattern (DI registration style, quick-add `DataTemplate` location, calendar day-fill pipeline) rather than the literal line.
- **Out of scope** (do not build): episode data on a relapse, sub-daily pause subtraction in the elapsed display, production backfill of old Resistance completions into periods.
