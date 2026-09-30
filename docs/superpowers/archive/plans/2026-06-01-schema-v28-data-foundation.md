# Schema v2.8 + Data Foundation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere le fondamenta dati additive (schema v2.8) e le query che i piani UI (calendario/colonna Abitudine, timer ibrido) consumeranno: `Note.TargetDate`, `HabitConfig.NoteMarkerColor` + `SessionRepeatCount`, `SegmentDefinition.DurationSeconds`, la migration `AbitudineCalendarV28`, e le query note-per-giorno/mese su `INoteService`.

**Architecture:** Tutte modifiche **additive** (nuove colonne nullable). I modelli sono in `Recordum.Core`, le EF config + interceptor + migration in `Recordum.Data`. I test d'integrazione usano `DbFixture` (SQLite in-memory, `EnsureCreated()` dal modello), quindi i round-trip passano col solo cambio modello+config; la migration vera è un task separato testato via `schema_version`. Il versioning `HabitConfigHistory` rispecchia per nome: una colonna versionata richiede una colonna mirror sull'entità history.

**Tech Stack:** C# / .NET 10, EF Core + SQLite (EFCore.NamingConventions snake_case), xUnit + FluentAssertions.

**Riferimento spec:** `docs/superpowers/archive/specs/2026-06-01-abitudine-column-calendar-design.md` §7, §8, §10.

---

## File Structure

- **Modify** `src/Recordum.Core/Models/Note.cs` — aggiunge `TargetDate`.
- **Modify** `src/Recordum.Core/Models/HabitConfig.cs` — aggiunge `NoteMarkerColor`, `SessionRepeatCount`.
- **Modify** `src/Recordum.Core/Models/HabitConfigHistory.cs` — aggiunge mirror `SessionRepeatCount`.
- **Modify** `src/Recordum.Core/Models/SegmentDefinition.cs` — aggiunge `DurationSeconds`.
- **Modify** `src/Recordum.Data/Configurations/NoteConfiguration.cs` — indice `(HabitNodeId, TargetDate)`.
- **Modify** `src/Recordum.Data/Interceptors/HabitConfigHistoryColumns.cs` — `Versioned += SessionRepeatCount`, `Excluded += NoteMarkerColor`.
- **Modify** `src/Recordum.Core/Abstractions/Services/INoteService.cs` + `src/Recordum.Data/Services/NoteService.cs` — query per data/mese.
- **Modify** `tests/Recordum.WPF.Tests/Fixtures/FakeNoteService.cs` — implementa i nuovi metodi (compile).
- **Create** `src/Recordum.Data/Migrations/<ts>_AbitudineCalendarV28.cs` (via `dotnet ef`).
- **Modify** `src/Recordum.Data/SeedData/AppSettingSeed.cs` — `schema_version` → `2.8`.
- **Test (create/modify)**: `tests/Recordum.Data.Tests/Integration/NoteServiceTests.cs`, `tests/Recordum.Data.Tests/Interceptors/HabitConfigHistoryColumnsTests.cs`, una nuova classe interceptor-behavior, `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs`, e un round-trip segment.

---

## Task 1: `Note.TargetDate` + query note-per-giorno/mese

**Files:**
- Modify: `src/Recordum.Core/Models/Note.cs`
- Modify: `src/Recordum.Data/Configurations/NoteConfiguration.cs`
- Modify: `src/Recordum.Core/Abstractions/Services/INoteService.cs`
- Modify: `src/Recordum.Data/Services/NoteService.cs`
- Modify: `tests/Recordum.WPF.Tests/Fixtures/FakeNoteService.cs`
- Test: `tests/Recordum.Data.Tests/Integration/NoteServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

In `tests/Recordum.Data.Tests/Integration/NoteServiceTests.cs` aggiungi (dentro la classe, usa l'helper `SeedHabitAsync`/`NewService` già presenti):

```csharp
    [Fact]
    public async Task ListByHabitAndDateAsync_returns_only_notes_for_that_day()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var sut = NewService(fx, out _);
        var d = new DateOnly(2026, 6, 1);
        await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "today", TargetDate = d });
        await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "next", TargetDate = d.AddDays(1) });
        await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "free", TargetDate = null });

        var result = await sut.ListByHabitAndDateAsync(habitId, d);

        result.Should().ContainSingle().Which.BodyMd.Should().Be("today");
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task ListNoteDaysInMonthAsync_returns_distinct_days_with_notes()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var sut = NewService(fx, out _);
        await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "a", TargetDate = new DateOnly(2026, 6, 1) });
        await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "b", TargetDate = new DateOnly(2026, 6, 1) });
        await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "c", TargetDate = new DateOnly(2026, 6, 9) });
        await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "d", TargetDate = new DateOnly(2026, 7, 1) });

        var days = await sut.ListNoteDaysInMonthAsync(habitId, 2026, 6);

        days.Should().BeEquivalentTo(new[] { new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 9) });
        await fx.DisposeAsync();
    }
```

- [ ] **Step 2: Run tests to verify they fail (compile error: member missing)**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~NoteServiceTests" --nologo`
Expected: FAIL — `Note` non ha `TargetDate`, `INoteService` non ha i due metodi.

- [ ] **Step 3: Add the model property**

In `src/Recordum.Core/Models/Note.cs`, dopo `public bool IsSensitive { get; set; }`:

```csharp
    /// <summary>Optional logical day this note belongs to (calendar "note of the day"). Null = free/un-anchored note.</summary>
    public DateOnly? TargetDate { get; set; }
```

- [ ] **Step 4: Add the index**

In `src/Recordum.Data/Configurations/NoteConfiguration.cs`, sostituisci la riga dell'indice esistente:

```csharp
        builder.HasIndex(n => new { n.HabitNodeId, n.CreatedAt });
```

con (mantieni quello esistente e aggiungi il nuovo):

```csharp
        builder.HasIndex(n => new { n.HabitNodeId, n.CreatedAt });
        builder.HasIndex(n => new { n.HabitNodeId, n.TargetDate });
```

- [ ] **Step 5: Extend the interface**

In `src/Recordum.Core/Abstractions/Services/INoteService.cs`, dentro l'interfaccia:

```csharp
    ValueTask<IReadOnlyList<Note>> ListByHabitAndDateAsync(Guid habitNodeId, DateOnly date, CancellationToken ct = default);
    ValueTask<IReadOnlyList<DateOnly>> ListNoteDaysInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default);
```

- [ ] **Step 6: Implement in NoteService**

In `src/Recordum.Data/Services/NoteService.cs`, dopo `ListByHabitAsync`:

```csharp
    public async ValueTask<IReadOnlyList<Note>> ListByHabitAndDateAsync(Guid habitNodeId, DateOnly date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.AsNoTracking()
            .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate == date)
            .OrderByDescending(n => n.UpdatedAtMs)
            .ToListAsync(ct);
    }

    public async ValueTask<IReadOnlyList<DateOnly>> ListNoteDaysInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default)
    {
        var first = new DateOnly(year, month, 1);
        var next = first.AddMonths(1);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.AsNoTracking()
            .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate != null
                        && n.TargetDate >= first && n.TargetDate < next)
            .Select(n => n.TargetDate!.Value)
            .Distinct()
            .ToListAsync(ct);
    }
```

- [ ] **Step 7: Keep FakeNoteService compiling (WPF tests)**

In `tests/Recordum.WPF.Tests/Fixtures/FakeNoteService.cs` aggiungi l'implementazione dei due nuovi metodi, coerente con lo store interno del fake (adatta il nome dello store/lista esistente — apri il file per vederlo; se le note sono in una `List<Note> _notes`):

```csharp
    public ValueTask<IReadOnlyList<Note>> ListByHabitAndDateAsync(Guid habitNodeId, DateOnly date, CancellationToken ct = default)
    {
        IReadOnlyList<Note> r = _notes
            .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate == date)
            .ToList();
        return new(r);
    }

    public ValueTask<IReadOnlyList<DateOnly>> ListNoteDaysInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default)
    {
        IReadOnlyList<DateOnly> r = _notes
            .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate is { } t && t.Year == year && t.Month == month)
            .Select(n => n.TargetDate!.Value)
            .Distinct()
            .ToList();
        return new(r);
    }
```

> Se il fake non ha una lista interna, usa il campo già presente (es. `Store`); l'importante è replicare la semantica filtro. Verifica il nome reale aprendo il file prima di incollare.

- [ ] **Step 8: Run tests to verify they pass + full Data suite**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~NoteServiceTests" --nologo`
Expected: PASS
Run: `dotnet build tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --nologo`
Expected: build OK (FakeNoteService implementa l'interfaccia).

- [ ] **Step 9: Commit**

```bash
git add src/Recordum.Core/Models/Note.cs src/Recordum.Data/Configurations/NoteConfiguration.cs src/Recordum.Core/Abstractions/Services/INoteService.cs src/Recordum.Data/Services/NoteService.cs tests/Recordum.WPF.Tests/Fixtures/FakeNoteService.cs tests/Recordum.Data.Tests/Integration/NoteServiceTests.cs
git commit -m "feat(data): Note.TargetDate + note-per-day/month queries (schema v2.8)"
```

---

## Task 2: `HabitConfig.NoteMarkerColor` + `SessionRepeatCount` + versioning

**Files:**
- Modify: `src/Recordum.Core/Models/HabitConfig.cs`
- Modify: `src/Recordum.Core/Models/HabitConfigHistory.cs`
- Modify: `src/Recordum.Data/Interceptors/HabitConfigHistoryColumns.cs`
- Modify: `tests/Recordum.Data.Tests/Interceptors/HabitConfigHistoryColumnsTests.cs`
- Test: `tests/Recordum.Data.Tests/Interceptors/HabitConfigHistoryVersioningV28Tests.cs` (create)

- [ ] **Step 1: Write the failing behavior test**

Create `tests/Recordum.Data.Tests/Interceptors/HabitConfigHistoryVersioningV28Tests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Interceptors;

public class HabitConfigHistoryVersioningV28Tests
{
    private static async Task<(DbFixture fx, Guid habitId, Guid cfgId)> SeedAsync()
    {
        var fx = new DbFixture();
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "h" };
        db.HabitNodes.Add(h);
        var c = new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = h.Id, SessionRepeatCount = 1 };
        db.Set<HabitConfig>().Add(c);
        await db.SaveChangesAsync();
        return (fx, h.Id, c.Id);
    }

    [Fact]
    public async Task Changing_SessionRepeatCount_creates_history_snapshot()
    {
        var (fx, habitId, cfgId) = await SeedAsync();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var c = await db.Set<HabitConfig>().FirstAsync(x => x.Id == cfgId);
            c.SessionRepeatCount = 8;
            await db.SaveChangesAsync();
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Set<HabitConfigHistory>().CountAsync(x => x.HabitNodeId == habitId)).Should().Be(1);
        }
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Changing_NoteMarkerColor_does_not_create_history_snapshot()
    {
        var (fx, habitId, cfgId) = await SeedAsync();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var c = await db.Set<HabitConfig>().FirstAsync(x => x.Id == cfgId);
            c.NoteMarkerColor = "#e0b341";
            await db.SaveChangesAsync();
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Set<HabitConfigHistory>().CountAsync(x => x.HabitNodeId == habitId)).Should().Be(0);
        }
        await fx.DisposeAsync();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~HabitConfigHistoryVersioningV28Tests" --nologo`
Expected: FAIL — `HabitConfig` non ha `NoteMarkerColor`/`SessionRepeatCount`.

- [ ] **Step 3: Add the HabitConfig properties**

In `src/Recordum.Core/Models/HabitConfig.cs`, dopo `public int? SessionCountdownSeconds { get; set; }`:

```csharp
    public int? SessionRepeatCount { get; set; }
    public string? NoteMarkerColor { get; set; }
```

- [ ] **Step 4: Add the HabitConfigHistory mirror property (versioned only)**

In `src/Recordum.Core/Models/HabitConfigHistory.cs`, dopo `public int? SessionCountdownSeconds { get; set; }`:

```csharp
    public int? SessionRepeatCount { get; set; }
```

> `NoteMarkerColor` è **escluso** dal versioning → NON va su `HabitConfigHistory`.

- [ ] **Step 5: Update the versioning column lists**

In `src/Recordum.Data/Interceptors/HabitConfigHistoryColumns.cs`:

In `Excluded`, aggiungi `"NoteMarkerColor"`:

```csharp
    public static readonly IReadOnlyList<string> Excluded = new[]
    {
        "Id","HabitNodeId","CreatedAt","UpdatedAt","UpdatedAtMs",
        "QuickComplete","AiDataSharing","StreakBrokenAt","NoteMarkerColor"
    };
```

In `Versioned`, aggiungi `"SessionRepeatCount"`:

```csharp
    public static readonly IReadOnlyList<string> Versioned = new[]
    {
        "ScheduleType","ScheduleDaysJson","ScheduleWeeklyTarget","ScheduleEveryXDays","ScheduleMonthlyDaysJson",
        "StreakMode","StreakRecovery","StreakToleranceMinutes",
        "HasIntegratedTimer","TimerMode","TimerTargetSeconds","TimerAutoPrompt",
        "RestTimerSeconds","SessionBaseTimer","SessionCountdownSeconds","SessionRepeatCount"
    };
```

- [ ] **Step 6: Update the existing column-list test**

In `tests/Recordum.Data.Tests/Interceptors/HabitConfigHistoryColumnsTests.cs`, aggiorna `ExpectedExcluded`:

```csharp
    private static readonly string[] ExpectedExcluded =
    {
        "Id","HabitNodeId","CreatedAt","UpdatedAt","UpdatedAtMs",
        "QuickComplete","AiDataSharing","StreakBrokenAt","NoteMarkerColor"
    };
```

> Il test `Versioned_plus_excluded_covers_HabitConfig_writable_props` ora passa automaticamente perché entrambe le nuove proprietà sono coperte (una in Versioned, una in Excluded).

- [ ] **Step 7: Run to verify all pass**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~HabitConfigHistory" --nologo`
Expected: PASS (nuovi behavior test + i 3 esistenti su `HabitConfigHistoryColumns`).

- [ ] **Step 8: Commit**

```bash
git add src/Recordum.Core/Models/HabitConfig.cs src/Recordum.Core/Models/HabitConfigHistory.cs src/Recordum.Data/Interceptors/HabitConfigHistoryColumns.cs tests/Recordum.Data.Tests/Interceptors/HabitConfigHistoryColumnsTests.cs tests/Recordum.Data.Tests/Interceptors/HabitConfigHistoryVersioningV28Tests.cs
git commit -m "feat(data): HabitConfig.NoteMarkerColor (excluded) + SessionRepeatCount (versioned)"
```

---

## Task 3: `SegmentDefinition.DurationSeconds`

**Files:**
- Modify: `src/Recordum.Core/Models/SegmentDefinition.cs`
- Test: `tests/Recordum.Data.Tests/Integration/SegmentDefinitionV28Tests.cs` (create)

- [ ] **Step 1: Write the failing round-trip test**

Create `tests/Recordum.Data.Tests/Integration/SegmentDefinitionV28Tests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class SegmentDefinitionV28Tests
{
    [Fact]
    public async Task DurationSeconds_round_trips_and_defaults_null()
    {
        await using var fx = new DbFixture();
        var habitId = Guid.NewGuid();
        Guid timedId, manualId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "h" });
            var timed = new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "Work", DurationSeconds = 30, SortOrder = 0 };
            var manual = new SegmentDefinition { Id = Guid.NewGuid(), HabitNodeId = habitId, Name = "Lap", DurationSeconds = null, SortOrder = 1 };
            db.Set<SegmentDefinition>().AddRange(timed, manual);
            await db.SaveChangesAsync();
            timedId = timed.Id; manualId = manual.Id;
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Set<SegmentDefinition>().FirstAsync(s => s.Id == timedId)).DurationSeconds.Should().Be(30);
            (await db.Set<SegmentDefinition>().FirstAsync(s => s.Id == manualId)).DurationSeconds.Should().BeNull();
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~SegmentDefinitionV28Tests" --nologo`
Expected: FAIL — `SegmentDefinition` non ha `DurationSeconds`.

- [ ] **Step 3: Add the property**

In `src/Recordum.Core/Models/SegmentDefinition.cs`, dopo `public bool IsRest { get; set; }`:

```csharp
    /// <summary>Optional countdown length for the segment. Null = manual/lap (user advances). Set = auto-advancing interval.</summary>
    public int? DurationSeconds { get; set; }
```

> Nessuna EF config extra: è una colonna INTEGER nullable, mappata di default dalla naming convention come `duration_seconds`.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~SegmentDefinitionV28Tests" --nologo`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.Core/Models/SegmentDefinition.cs tests/Recordum.Data.Tests/Integration/SegmentDefinitionV28Tests.cs
git commit -m "feat(data): SegmentDefinition.DurationSeconds (optional per-segment countdown)"
```

---

## Task 4: Migration `AbitudineCalendarV28` + bump schema_version

**Files:**
- Create: `src/Recordum.Data/Migrations/<ts>_AbitudineCalendarV28.cs` (+ `.Designer.cs`, snapshot aggiornato) via `dotnet ef`
- Modify: `src/Recordum.Data/SeedData/AppSettingSeed.cs`
- Modify: `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs`

- [ ] **Step 1: Write/adjust the failing seed test**

Apri `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs` e trova l'asserzione che controlla `schema_version == "2.7"`. Cambiala in `"2.8"`. Se il file conta gli AppSetting, il totale resta **18** (nessun nuovo setting). Esempio di asserzione attesa:

```csharp
        AppSettingSeed.All.Single(s => s.Key == "schema_version").Value.Should().Be("2.8");
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~SeedDataTests" --nologo`
Expected: FAIL — seed dice ancora `2.7`.

- [ ] **Step 3: Bump the seed**

In `src/Recordum.Data/SeedData/AppSettingSeed.cs`:

Aggiungi la data di seed v2.8 dopo `SeedDateV27`:

```csharp
    private static readonly DateTime SeedDateV28 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
```

Cambia la riga `schema_version`:

```csharp
        new AppSetting { Key = "schema_version",              Value = "2.8",      UpdatedAt = SeedDateV28 },
```

- [ ] **Step 4: Generate the migration**

Run:
```bash
dotnet ef migrations add AbitudineCalendarV28 --project src/Recordum.Data --startup-project src/Recordum.WPF
```
Expected: crea `src/Recordum.Data/Migrations/<ts>_AbitudineCalendarV28.cs` + `.Designer.cs` e aggiorna `RecordumDbContextModelSnapshot.cs`.

- [ ] **Step 5: Verify the generated Up() and add the schema_version bump**

Apri il file `<ts>_AbitudineCalendarV28.cs`. **Verifica** che `Up()` contenga esattamente queste `AddColumn` additive (nomi tabella/colonna possono variare leggermente per la naming convention — verifica contro la doc schema v2.2 prima di procedere):
- `target_date` TEXT NULL su `notes`
- `note_marker_color` TEXT NULL su `habit_configs`
- `session_repeat_count` INTEGER NULL su `habit_configs`
- `session_repeat_count` INTEGER NULL su `habit_config_histories` (mirror)
- `duration_seconds` INTEGER NULL su `segment_definitions`
- `CreateIndex` su `notes (habit_node_id, target_date)`

Non deve esserci **nessun** `DropColumn`/`AlterColumn`. Poi aggiungi in fondo a `Up()` il bump:

```csharp
            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.8' WHERE key = 'schema_version';");
```

e in cima a `Down()` il rollback (prima dei `DropColumn` generati):

```csharp
            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.7' WHERE key = 'schema_version';");
```

- [ ] **Step 6: Build + full Data suite green**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj --nologo`
Expected: build OK (snapshot coerente col modello).
Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --nologo`
Expected: PASS (inclusi SeedDataTests, i nuovi V28, e gli eventuali migration test esistenti che applicano l'intera catena).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.Data/Migrations/ src/Recordum.Data/SeedData/AppSettingSeed.cs tests/Recordum.Data.Tests/Schema/SeedDataTests.cs
git commit -m "feat(data): AbitudineCalendarV28 migration — additive schema v2.8 + schema_version bump"
```

---

## Task 5: Verifica finale di fase

- [ ] **Step 1: Full suites green**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --nologo`
Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --nologo`
Expected: entrambe PASS, nessuna regressione (WPF compila col `FakeNoteService` aggiornato).

- [ ] **Step 2: Aggiorna il tracker**

In `docs/phase-4-issues.md`, nota sotto lo stato corrente che la fondazione dati v2.8 è completata (riferimento spec `2026-06-01-abitudine-column-calendar-design.md` §10). Commit:

```bash
git add docs/phase-4-issues.md
git commit -m "docs(phase-4): schema v2.8 data foundation done (Piano 1)"
```

---

## Self-Review

- **Spec coverage (§10 schema delta):** `note.target_date` → Task 1 ✓; `habit_config.note_marker_color` → Task 2 ✓; `habit_config.session_repeat_count` → Task 2 ✓; `segment_definition.duration_seconds` → Task 3 ✓; migration additiva + schema_version 2.8 → Task 4 ✓; versioning placement (SessionRepeatCount→Versioned, NoteMarkerColor→Excluded) → Task 2 ✓; mirror su HabitConfigHistory → Task 2 Step 4 ✓; indice note-mese → Task 1 Step 4 ✓; query note-per-giorno/mese (§11) → Task 1 ✓.
- **Fuori da questo piano (vanno nei Piani 2/3):** `CalendarNavigator`, colonna Abitudine, pannello-giorno, query marcatori completamento, UI timer ibrido + motore, picker colore-nota in Configura.
- **Placeholder scan:** ogni step ha codice/comandi concreti. Unico punto di adattamento dichiarato: nome dello store interno in `FakeNoteService` (Task 1 Step 7) — istruito a verificare il file.
- **Type consistency:** `TargetDate` `DateOnly?` usato coerente in modello, query, test, fake; `SessionRepeatCount` `int?` e `NoteMarkerColor` `string?` coerenti tra HabitConfig, liste versioning e test; `DurationSeconds` `int?` coerente.
