# Journal/Diary Data Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the persisted note category and the note↔completion coupling for journal habits — schema v2.11 plus a single `IDiaryService` write path — so a diary entry is one `Note(Diario)` paired 1:1 with one `Completion`, created and deleted together with no orphans.

**Architecture:** Two additive columns on `note` (`category`, `completion_id`) introduced by migration `NoteCategoryV211`. All diary entry writes/deletes/edits funnel through one new Data-layer service, `DiaryService`, which owns the single delicate invariant (the pair) inside one transaction and publishes the existing events so Home/calendar/streak refresh unchanged. This plan is the data foundation only; the WPF entry points (spec §6) and the Note column UI (spec §7) are a separate follow-up plan.

**Tech Stack:** C# / .NET 10, EF Core + SQLite (SQLCipher), xUnit + FluentAssertions, CommunityToolkit.Mvvm (downstream).

**Source spec:** `docs/superpowers/specs/2026-06-19-journal-diary-feature-design.md` (Model B, approved 2026-06-19). Sections covered here: §2 (1:1 model), §3 (domain model), §4 (migration v2.11), §5 (IDiaryService), §10 (testing — Core + Data). Deferred to Plan 2: §6 (WPF entry points), §7 (Note column / Feature A), §9 (seeder repopulation).

## Global Constraints

- **English-first user strings** in `.resx`; this plan adds no user-facing strings (pure data layer). Italian OK in comments.
- **Never** `DateTime.UtcNow`/`Now`; all time via injected `IClock`. Timestamp convention must mirror `QuickAddViewModelBase.ResolveCompletionTimestamp`: same-day entry → `clock.UtcNow.UtcDateTime`; past day → `DateTime.SpecifyKind(day-at-midnight, Utc)`; `CompletedAtMs = new DateTimeOffset(ts, TimeSpan.Zero).ToUnixTimeMilliseconds()`; `IsRetroactive = (day != today)`.
- **Cascade rules + indices explicit in Fluent API**, never EF defaults. `completion_id` is a **soft FK** (scalar `Guid?`, no EF relationship, no cascade) — the pairing is managed by `DiaryService` in a transaction.
- **snake_case** tables/columns via `EFCore.NamingConventions`; PascalCase C#.
- **Do not hand-edit** `*.Designer.cs` / `*ModelSnapshot.cs` (blocked by guardrail hook). Generate them via `dotnet ef migrations add`. The `schema_version` bump is hand-added only to the non-Designer migration `.cs`.
- **Migration is purely additive** (Q6): no data migration of legacy `Completion.Notes`. Existing rows default `category = 'Veloce'`, `completion_id = NULL`.
- Schema bumps from **2.10 → 2.11**. Down reverts to 2.10.
- `Note` keeps **only** `updated_at_ms` (no `updated_at`) — intentional; do not add one.

---

### Task 1: NoteCategory enum + Note model fields

**Files:**
- Create: `src/Recordum.Core/Enums/NoteCategory.cs`
- Modify: `src/Recordum.Core/Models/Note.cs`

**Interfaces:**
- Produces: `enum NoteCategory { Diario, Veloce }` (default first value `Diario` = 0, but the *column* default is `Veloce` — set in mapping, Task 2). `Note.Category` (`NoteCategory`), `Note.CompletionId` (`Guid?`).
- Consumes: nothing.

- [ ] **Step 1: Create the enum**

```csharp
namespace Recordum.Core.Enums;

/// <summary>How a note is classified. Diario = a journal diary entry (paired 1:1 with a Completion);
/// Veloce = a free quick note with no completion. Foundation for richer note types via templates (Future).</summary>
public enum NoteCategory
{
    Diario = 0,
    Veloce = 1,
}
```

- [ ] **Step 2: Add the two fields to Note**

In `src/Recordum.Core/Models/Note.cs`, add `using Recordum.Core.Enums;` at the top and these two properties after `HabitNodeId` (keep the rest of the class unchanged):

```csharp
    /// <summary>Diario (journal diary entry, paired with a Completion) vs Veloce (free quick note, no completion).
    /// Column default is Veloce; legacy rows are Veloce.</summary>
    public NoteCategory Category { get; set; } = NoteCategory.Veloce;

    /// <summary>Soft FK to the paired Completion (Diario notes only; null for Veloce). The note↔completion
    /// pairing is managed transactionally by IDiaryService — there is no EF relationship or cascade here.</summary>
    public Guid? CompletionId { get; set; }
```

- [ ] **Step 3: Build Core to verify it compiles**

Run: `dotnet build src/Recordum.Core/Recordum.Core.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.Core/Enums/NoteCategory.cs src/Recordum.Core/Models/Note.cs
git commit -m "feat(core): NoteCategory enum + Note.Category/CompletionId fields (journal diary v2.11)"
```

---

### Task 2: Note EF mapping for the new columns

**Files:**
- Modify: `src/Recordum.Data/Configurations/NoteConfiguration.cs`

**Interfaces:**
- Consumes: `Note.Category`, `Note.CompletionId` (Task 1).
- Produces: column `category TEXT NOT NULL DEFAULT 'Veloce'` + CHECK `ck_note_category`; column `completion_id TEXT NULL` + index `ix_notes_completion_id`.

- [ ] **Step 1: Add the mapping**

Replace the body of `NoteConfiguration.Configure` so it reads (keep existing key, BodyMd comment, HabitNode FK, and the two existing indices; add the three new lines for Category, the check constraint, and the completion_id index):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.HasKey(n => n.Id);
        // BodyMd has no max length (TEXT column)

        builder.Property(n => n.Category)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(NoteCategory.Veloce)
            .IsRequired();

        builder.HasOne(n => n.HabitNode)
            .WithMany(h => h.Notes)
            .HasForeignKey(n => n.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.HabitNodeId, n.CreatedAt });
        builder.HasIndex(n => new { n.HabitNodeId, n.TargetDate });
        // Soft FK to the paired Completion (managed by DiaryService); indexed for the Cronologia join.
        builder.HasIndex(n => n.CompletionId);

        builder.ToTable(t =>
            t.HasCheckConstraint("ck_note_category", "category IN ('Diario','Veloce')"));
    }
}
```

- [ ] **Step 2: Build Data to verify it compiles**

Run: `dotnet build src/Recordum.Data/Recordum.Data.csproj`
Expected: Build succeeded, 0 errors. (The model now differs from the snapshot — that is expected; Task 3 generates the migration.)

- [ ] **Step 3: Commit**

```bash
git add src/Recordum.Data/Configurations/NoteConfiguration.cs
git commit -m "feat(data): map Note.Category (check) + Note.CompletionId (soft FK index)"
```

---

### Task 3: Migration NoteCategoryV211 + migration test

**Files:**
- Generated: `src/Recordum.Data/Migrations/<timestamp>_NoteCategoryV211.cs` (+ `.Designer.cs`, snapshot — via tool, do not hand-edit)
- Modify (the generated non-Designer file only): add the `schema_version` bump
- Create: `tests/Recordum.Data.Tests/Migrations/NoteCategoryV211MigrationTests.cs`

**Interfaces:**
- Consumes: Task 2 mapping.
- Produces: applied schema with `schema_version = '2.11'`, `category`/`completion_id` columns, CHECK enforced.

- [ ] **Step 1: Scaffold the migration**

Run:
```bash
dotnet ef migrations add NoteCategoryV211 --project src/Recordum.Data --startup-project src/Recordum.WPF
```
Expected: creates `..._NoteCategoryV211.cs` + `.Designer.cs` and updates `RecordumDbContextModelSnapshot.cs`. The `Up` adds the `category` column (with default `'Veloce'`), the `completion_id` column, the `ix_notes_completion_id` index, and the `ck_note_category` check constraint (SQLite may express the check via a table rebuild — that is fine).

- [ ] **Step 2: Verify the generated SQL against the schema doc**

Run: `dotnet ef migrations script 2.10 NoteCategoryV211 --project src/Recordum.Data --startup-project src/Recordum.WPF` (or inspect the generated `Up`).
Expected: confirm (a) `category` added NOT NULL default `'Veloce'`, (b) `completion_id` added nullable, (c) index on `completion_id`, (d) check constraint `category IN ('Diario','Veloce')`. No drop/recreate of unrelated tables beyond the `notes` rebuild if SQLite needs one.

- [ ] **Step 3: Hand-add the schema_version bump to the generated migration**

In the generated non-Designer `..._NoteCategoryV211.cs`, append to the END of `Up(...)`:

```csharp
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 6, 19, 0, 0, 0, 0, DateTimeKind.Utc), "2.11" });
```

and append to the END of `Down(...)`:

```csharp
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 6, 13, 0, 0, 0, 0, DateTimeKind.Utc), "2.10" });
```

If the file lacks `using System;`, add it (the `DateTime` literal needs it).

- [ ] **Step 4: Write the failing migration test**

Create `tests/Recordum.Data.Tests/Migrations/NoteCategoryV211MigrationTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Migrations;

/// <summary>
/// Verifies NoteCategoryV211: schema_version bumps to 2.11; the category column defaults to Veloce
/// and its CHECK rejects unknown values; completion_id round-trips.
/// </summary>
public sealed class NoteCategoryV211MigrationTests
{
    [Fact]
    public async Task Schema_version_AppSetting_is_2_11_after_full_migration()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var setting = await db.AppSettings.SingleAsync(s => s.Key == "schema_version");
        Assert.Equal("2.11", setting.Value);
    }

    [Fact]
    public async Task Note_defaults_to_Veloce_and_round_trips_completion_id()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var habitId = Guid.NewGuid();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" });
        await db.SaveChangesAsync();

        var completionId = Guid.NewGuid();
        db.Completions.Add(new Completion { Id = completionId, HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 });
        var diario = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "x", Category = NoteCategory.Diario, CompletionId = completionId };
        var veloce = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "y" }; // default Veloce
        db.Notes.AddRange(diario, veloce);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var stored = await db.Notes.SingleAsync(n => n.Id == veloce.Id);
        Assert.Equal(NoteCategory.Veloce, stored.Category);
        var storedDiario = await db.Notes.SingleAsync(n => n.Id == diario.Id);
        Assert.Equal(completionId, storedDiario.CompletionId);
    }

    [Fact]
    public async Task Category_check_constraint_rejects_unknown_value()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = TestDbHelper.CreateFull(connection);
        await db.Database.MigrateAsync();

        var habitId = Guid.NewGuid();
        db.HabitNodes.Add(new HabitNode { Id = habitId, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" });
        await db.SaveChangesAsync();

        // Raw insert bypassing the enum mapping to hit the DB CHECK directly.
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO notes (id, habit_node_id, body_md, is_sensitive, created_at, updated_at_ms, category) " +
            "VALUES ($id, $h, 'z', 0, '2026-06-19T00:00:00', 0, 'Bogus');";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$h", habitId.ToString());
        await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
    }
}
```

- [ ] **Step 5: Run the migration tests**

Run: `dotnet test tests/Recordum.Data.Tests --filter FullyQualifiedName~NoteCategoryV211MigrationTests`
Expected: 3 passed. If the raw-insert column list differs (verify actual column names with the generated migration), adjust the `INSERT` column names to match snake_case exactly.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Data/Migrations tests/Recordum.Data.Tests/Migrations/NoteCategoryV211MigrationTests.cs
git commit -m "feat(data): NoteCategoryV211 migration (schema 2.11) + migration tests"
```

---

### Task 4: IDiaryService interface + SaveEntryAsync

**Files:**
- Create: `src/Recordum.Core/Abstractions/Services/IDiaryService.cs`
- Create: `src/Recordum.Data/Services/DiaryService.cs`
- Create: `tests/Recordum.Data.Tests/Integration/DiaryServiceTests.cs`

**Interfaces:**
- Consumes: `IClock`, `IEventBus`, `IDbContextFactory<RecordumDbContext>`, `Note`, `Completion`, `NoteHistory`, `NoteCategory`, payloads `CompletionRecordedPayload`/`NoteCreatedPayload`.
- Produces:
  - `ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default)` — returns the new note id.
  - `ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default)` (Task 5).
  - `ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default)` (Task 5).
  - `ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default)` (Task 5).

- [ ] **Step 1: Write the interface**

Create `src/Recordum.Core/Abstractions/Services/IDiaryService.cs`:

```csharp
using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

/// <summary>Single write path for journal diary entries. Each entry is a Note(Diario) paired 1:1
/// with a Completion, created and deleted together transactionally — no other code may write or
/// delete the journal Note/Completion pair directly.</summary>
public interface IDiaryService
{
    /// <summary>Creates a Diario note for <paramref name="day"/> plus its paired completion, in one
    /// transaction, and returns the note id. The completion lands on <paramref name="day"/> (at the
    /// current time-of-day) so calendar/streak see the right day; it is retroactive when day != today.</summary>
    ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default);

    /// <summary>Deletes a diary entry — the note and its paired completion together — given either the
    /// note id or the completion id. No-op if neither resolves to a Diario note.</summary>
    ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default);

    /// <summary>Updates only the note body (+ version history); the paired completion is untouched.</summary>
    ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default);

    /// <summary>The Diario note paired with <paramref name="completionId"/>, or null.</summary>
    ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write the failing SaveEntryAsync test**

Create `tests/Recordum.Data.Tests/Integration/DiaryServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Events;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class DiaryServiceTests
{
    private static async Task<(DbFixture fx, Guid habitId)> SeedJournalAsync(FakeClock? clock = null)
    {
        var fx = new DbFixture(clock: clock);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var h = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
        db.HabitNodes.Add(h);
        await db.SaveChangesAsync();
        return (fx, h.Id);
    }

    private static DiaryService NewService(DbFixture fx, out RecordingEventBus bus)
    {
        bus = new RecordingEventBus();
        return new DiaryService(fx.Factory, fx.Clock, NullLogger<DiaryService>.Instance, bus);
    }

    [Fact]
    public async Task SaveEntryAsync_creates_paired_note_and_completion_and_publishes_events()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out var bus);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);

        var noteId = await sut.SaveEntryAsync(habitId, today, "dear diary");

        await using var db = await fx.Factory.CreateDbContextAsync();
        var note = await db.Notes.SingleAsync(n => n.Id == noteId);
        note.Category.Should().Be(NoteCategory.Diario);
        note.TargetDate.Should().Be(today);
        note.BodyMd.Should().Be("dear diary");
        note.CompletionId.Should().NotBeNull();

        var completion = await db.Completions.SingleAsync(c => c.Id == note.CompletionId!.Value);
        completion.HabitNodeId.Should().Be(habitId);
        DateOnly.FromDateTime(completion.CompletedAt).Should().Be(today);
        completion.IsRetroactive.Should().BeFalse();

        // version history snapshot exists
        (await db.NoteHistories.CountAsync(h => h.NoteId == noteId)).Should().Be(1);

        bus.Published.Should().Contain(p => p is NoteCreatedPayload);
        bus.Published.Should().Contain(p => p is CompletionRecordedPayload);

        await fx.DisposeAsync();
    }

    [Fact]
    public async Task SaveEntryAsync_past_day_marks_completion_retroactive_on_that_day()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var pastDay = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime).AddDays(-3);

        var noteId = await sut.SaveEntryAsync(habitId, pastDay, "backfilled");

        await using var db = await fx.Factory.CreateDbContextAsync();
        var note = await db.Notes.SingleAsync(n => n.Id == noteId);
        var completion = await db.Completions.SingleAsync(c => c.Id == note.CompletionId!.Value);
        DateOnly.FromDateTime(completion.CompletedAt).Should().Be(pastDay);
        completion.IsRetroactive.Should().BeTrue();
        note.TargetDate.Should().Be(pastDay);

        await fx.DisposeAsync();
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/Recordum.Data.Tests --filter FullyQualifiedName~DiaryServiceTests`
Expected: FAIL / does not compile — `DiaryService` does not exist.

- [ ] **Step 4: Implement DiaryService with SaveEntryAsync**

Create `src/Recordum.Data/Services/DiaryService.cs` (DeleteEntryAsync/EditEntryAsync/GetEntryByCompletionAsync are stubbed here and filled in Task 5):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Services;

public sealed partial class DiaryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<DiaryService> logger,
    IEventBus eventBus) : IDiaryService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Diary entry {NoteId} saved for habit {HabitId}")]
    private partial void LogSaved(Guid noteId, Guid habitId);

    public async ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var habit = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == habitNodeId, ct)
            ?? throw new HabitNodeNotFoundException(habitNodeId);

        var now = clock.UtcNow.UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        bool retro = day != today;
        var ts = retro ? DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : now;
        var ms = new DateTimeOffset(ts, TimeSpan.Zero).ToUnixTimeMilliseconds();

        var completion = new Completion
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitNodeId,
            CompletedAt = ts,
            CompletedAtMs = ms,
            IsRetroactive = retro,
            // Notes intentionally null: for journals the text lives in Note.BodyMd (spec §3).
        };
        var note = new Note
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitNodeId,
            BodyMd = text,
            Category = NoteCategory.Diario,
            TargetDate = day,
            CompletionId = completion.Id,
            CreatedAt = now,
            UpdatedAtMs = clock.UtcNowMs,
        };

        db.Completions.Add(completion);
        db.Notes.Add(note);
        db.NoteHistories.Add(new NoteHistory { Id = Guid.NewGuid(), NoteId = note.Id, BodyMd = text, CreatedAt = now });
        // Single SaveChanges = single atomic transaction for the whole pair.
        await db.SaveChangesAsync(ct);

        LogSaved(note.Id, habitNodeId);
        eventBus.Publish(new CompletionRecordedPayload(completion.Id, habitNodeId, completion.CompletedAt));
        eventBus.Publish(new NoteCreatedPayload(note.Id, habitNodeId));
        return note.Id;
    }

    public ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
```

Note: verify `HabitNodeNotFoundException` lives in `Recordum.Core.Errors` (it is thrown by `CompletionService` from that namespace). If `clock.UtcNowMs` is not a member of `IClock`, use `new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeMilliseconds()` instead (NoteService uses `clock.UtcNowMs`, so it exists).

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Recordum.Data.Tests --filter FullyQualifiedName~DiaryServiceTests`
Expected: 2 passed.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Core/Abstractions/Services/IDiaryService.cs src/Recordum.Data/Services/DiaryService.cs tests/Recordum.Data.Tests/Integration/DiaryServiceTests.cs
git commit -m "feat(data): IDiaryService + DiaryService.SaveEntryAsync (paired note+completion)"
```

---

### Task 5: DeleteEntryAsync + EditEntryAsync + GetEntryByCompletionAsync

**Files:**
- Modify: `src/Recordum.Data/Services/DiaryService.cs`
- Modify: `tests/Recordum.Data.Tests/Integration/DiaryServiceTests.cs`

**Interfaces:**
- Consumes: `SaveEntryAsync` (Task 4), payloads `NoteDeletedPayload`/`CompletionDeletedPayload`/`NoteUpdatedPayload`.
- Produces: full `IDiaryService` implementation.

- [ ] **Step 1: Write the failing delete/edit/query tests**

Append these methods inside `DiaryServiceTests` (before the closing brace):

```csharp
    [Fact]
    public async Task DeleteEntryAsync_by_note_id_removes_both_and_publishes_both_events()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out var bus);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var noteId = await sut.SaveEntryAsync(habitId, today, "entry");
        Guid completionId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
            completionId = (await db.Notes.SingleAsync(n => n.Id == noteId)).CompletionId!.Value;
        bus.Published.Clear();

        await sut.DeleteEntryAsync(noteId);

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Notes.AnyAsync(n => n.Id == noteId)).Should().BeFalse();
            (await db.Completions.AnyAsync(c => c.Id == completionId)).Should().BeFalse();
            (await db.NoteHistories.AnyAsync(h => h.NoteId == noteId)).Should().BeFalse();
        }
        bus.Published.Should().Contain(p => p is NoteDeletedPayload);
        bus.Published.Should().Contain(p => p is CompletionDeletedPayload);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteEntryAsync_by_completion_id_removes_both()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var noteId = await sut.SaveEntryAsync(habitId, today, "entry");
        Guid completionId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
            completionId = (await db.Notes.SingleAsync(n => n.Id == noteId)).CompletionId!.Value;

        await sut.DeleteEntryAsync(completionId);

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            (await db.Notes.AnyAsync(n => n.Id == noteId)).Should().BeFalse();
            (await db.Completions.AnyAsync(c => c.Id == completionId)).Should().BeFalse();
        }
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteEntryAsync_one_of_two_same_day_keeps_the_day_complete()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var firstId = await sut.SaveEntryAsync(habitId, today, "first");
        await sut.SaveEntryAsync(habitId, today, "second");

        await sut.DeleteEntryAsync(firstId);

        await using var db = await fx.Factory.CreateDbContextAsync();
        // One note + one completion remain on the day → day stays "done".
        (await db.Notes.CountAsync(n => n.HabitNodeId == habitId)).Should().Be(1);
        (await db.Completions.CountAsync(c => c.HabitNodeId == habitId)).Should().Be(1);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task EditEntryAsync_updates_body_and_history_but_not_the_completion()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out var bus);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var noteId = await sut.SaveEntryAsync(habitId, today, "v1");
        Guid completionId;
        long completedMs;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var n = await db.Notes.SingleAsync(x => x.Id == noteId);
            completionId = n.CompletionId!.Value;
            completedMs = (await db.Completions.SingleAsync(c => c.Id == completionId)).CompletedAtMs;
        }
        bus.Published.Clear();

        await sut.EditEntryAsync(noteId, "v2");

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var n = await db.Notes.SingleAsync(x => x.Id == noteId);
            n.BodyMd.Should().Be("v2");
            n.CompletionId.Should().Be(completionId);
            (await db.Completions.SingleAsync(c => c.Id == completionId)).CompletedAtMs.Should().Be(completedMs);
            (await db.NoteHistories.CountAsync(h => h.NoteId == noteId)).Should().Be(2); // v1 + v2
        }
        bus.Published.Should().Contain(p => p is NoteUpdatedPayload);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task GetEntryByCompletionAsync_returns_paired_note()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var noteId = await sut.SaveEntryAsync(habitId, today, "find me");
        Guid completionId;
        await using (var db = await fx.Factory.CreateDbContextAsync())
            completionId = (await db.Notes.SingleAsync(n => n.Id == noteId)).CompletionId!.Value;

        var note = await sut.GetEntryByCompletionAsync(completionId);
        note.Should().NotBeNull();
        note!.Id.Should().Be(noteId);
        note.BodyMd.Should().Be("find me");
        await fx.DisposeAsync();
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Recordum.Data.Tests --filter FullyQualifiedName~DiaryServiceTests`
Expected: the five new tests FAIL with `NotImplementedException`.

- [ ] **Step 3: Implement the three methods**

In `src/Recordum.Data/Services/DiaryService.cs`, replace the three stubbed methods with:

```csharp
    public async ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Resolve the Diario note from either the note id or the paired completion id.
        var note = await db.Notes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteOrCompletionId || n.CompletionId == noteOrCompletionId, ct);
        if (note is null) return; // not a diary entry → nothing to do ("never delete the completion alone")

        DateTime? completedAt = null;
        if (note.CompletionId is { } cid)
            completedAt = await db.Completions.AsNoTracking()
                .Where(c => c.Id == cid).Select(c => (DateTime?)c.CompletedAt).FirstOrDefaultAsync(ct);

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.NoteHistories.Where(h => h.NoteId == note.Id).ExecuteDeleteAsync(ct);
            await db.Notes.Where(n => n.Id == note.Id).ExecuteDeleteAsync(ct);
            if (note.CompletionId is { } cid2)
                await db.Completions.Where(c => c.Id == cid2).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        }

        eventBus.Publish(new NoteDeletedPayload(note.Id, note.HabitNodeId));
        if (note.CompletionId is { } cid3)
            eventBus.Publish(new CompletionDeletedPayload(cid3, note.HabitNodeId, completedAt ?? default));
    }

    public async ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == noteId, ct);
        if (note is null) return;
        if (note.BodyMd == text) return; // no-op, no spurious version
        note.BodyMd = text;
        note.UpdatedAtMs = clock.UtcNowMs;
        db.NoteHistories.Add(new NoteHistory { Id = Guid.NewGuid(), NoteId = noteId, BodyMd = text, CreatedAt = clock.UtcNow.UtcDateTime });
        await db.SaveChangesAsync(ct);
        eventBus.Publish(new NoteUpdatedPayload(note.Id, note.HabitNodeId));
    }

    public async ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.CompletionId == completionId, ct);
    }
```

Add any missing `using` (`NoteUpdatedPayload`, `CompletionDeletedPayload`, `NoteDeletedPayload` are all in `Recordum.Plugins.Contracts.Events`, already imported).

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test tests/Recordum.Data.Tests --filter FullyQualifiedName~DiaryServiceTests`
Expected: all 7 DiaryService tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.Data/Services/DiaryService.cs tests/Recordum.Data.Tests/Integration/DiaryServiceTests.cs
git commit -m "feat(data): DiaryService delete/edit/query — transactional pair delete, no orphans"
```

---

### Task 6: DI registration + full-suite green

**Files:**
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`

**Interfaces:**
- Consumes: `IDiaryService`/`DiaryService` (Tasks 4–5).
- Produces: `IDiaryService` resolvable from the container (for Plan 2 WPF consumers).

- [ ] **Step 1: Register the service**

In `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`, next to the other scoped Data services (after the `INoteService` line ~87), add:

```csharp
        services.AddScoped<IDiaryService, Data.Services.DiaryService>();
```

Confirm the `using` for `Recordum.Core.Abstractions.Services` is already present (it is — `INoteService` resolves there).

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build Recordum.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run the full Data + WPF test suites**

Run: `dotnet test tests/Recordum.Data.Tests` and `dotnet test tests/Recordum.WPF.Tests`
Expected: all green (Data baseline 296+ plus the new DiaryService/migration tests; WPF 335 baseline unchanged — this plan adds no WPF behavior).

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs
git commit -m "feat(wpf): register IDiaryService in DI"
```

---

## Self-Review

**Spec coverage (data-foundation slice):**
- §2 1:1 model → Task 4/5 (paired create + paired delete, day stays done with ≥1 entry test). ✓
- §3 domain model (Category, CompletionId, CreatedAt time, retroactive completion on TargetDate, Completion.Notes null for journals) → Task 1 (fields) + Task 4 (timestamp + null Notes). ✓
- §4 migration v2.11 (additive, default Veloce, CHECK, soft FK index, no data migration, bump 2.11/Down 2.10) → Task 2 + Task 3. ✓
- §5 IDiaryService (Save/Delete/Edit, single path, events) → Task 4/5; `GetEntryByCompletionAsync` added for the Plan 2 Cronologia join. ✓
- §10 testing (Core enum; Data: save pair + completion_id, delete both, delete-one-of-two keeps day, edit leaves completion, migration Up/CHECK/version) → Task 3 + Task 4/5. ✓ (Transactionality is structural: create = one SaveChanges; delete = explicit BeginTransaction/Commit. A fault-injection rollback test is omitted as not cleanly expressible against the in-memory provider; the single-transaction structure is the guarantee.)
- **Deferred to Plan 2:** §6 WPF entry points (JournalQuickAdd→DiaryService, Cronologia paired-text + delete, Home "+", quick-note Veloce), §7 Note column Feature A (chips + period toggle), §9 seeder repopulation via DiaryService. Stated in the plan header.

**Placeholder scan:** No TBD/“handle errors”/“similar to” — all steps carry concrete code/commands. ✓

**Type consistency:** `SaveEntryAsync`/`DeleteEntryAsync`/`EditEntryAsync`/`GetEntryByCompletionAsync` signatures identical across interface (Task 4), impl (Task 4/5), and tests. `NoteCategory.{Diario,Veloce}`, `Note.Category`, `Note.CompletionId` consistent across Tasks 1/2/3/4/5. Payload types match the existing records (`NoteCreatedPayload(NoteId, HabitNodeId)`, `CompletionRecordedPayload(CompletionId, HabitNodeId, CompletedAt)`, `CompletionDeletedPayload(CompletionId, HabitNodeId, CompletedAt)`). ✓

**Risks to watch during execution:**
- The migration may rebuild the `notes` table (SQLite CHECK add). The migration test exercises a real `MigrateAsync` end-to-end, so a bad rebuild fails loudly.
- Verify `IClock.UtcNowMs` exists (NoteService uses it) before relying on it; fallback shown in Task 4 Step 4.
- The raw-insert CHECK test (Task 3 Step 4) hard-codes snake_case column names — adjust to the generated schema if they differ.
