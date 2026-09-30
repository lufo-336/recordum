# Note Version History + Stable Date Anchor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Una nota resta ancorata al suo giorno (display + ordinamento stabili, l'edit non la sposta) e ogni salvataggio che cambia il testo viene versionato, con cronologia consultabile (click destro → Cronologia) e ripristino non-distruttivo.

**Architecture:** Tabella `note_history` (schema v2.9) che rispecchia `HabitConfigHistory`: snapshot del corpo ad ogni Create/Update che cambia `BodyMd`, nella stessa `SaveChangesAsync`. `NoteService` espone `ListVersionsAsync`/`RestoreVersionAsync` (il ripristino passa per `UpdateAsync` → crea una nuova versione). La tab Note guadagna un pannello cronologia inline (commuta con l'editor) e mostra come àncora `TargetDate ?? data(CreatedAt)`.

**Tech Stack:** C#/.NET 10, EF Core + SQLite, CommunityToolkit.Mvvm, WPF, xUnit + FluentAssertions. Spec: `docs/superpowers/archive/specs/2026-06-05-note-version-history-design.md`.

---

## File structure

**Core (`src/Recordum.Core`):**
- Create `Models/NoteHistory.cs` — entità versione (immutabile).
- Create `Abstractions/Services/Dtos/NoteVersion.cs` — DTO di query.
- Modify `Models/Note.cs` — navigazione `History`.
- Modify `Abstractions/Services/INoteService.cs` — `ListVersionsAsync` + `RestoreVersionAsync`.

**Data (`src/Recordum.Data`):**
- Create `Configurations/NoteHistoryConfiguration.cs` — mapping + cascade + indice.
- Modify `RecordumDbContext.cs` — `DbSet<NoteHistory>`.
- Modify `SeedData/AppSettingSeed.cs` — `schema_version` → "2.9".
- Modify `Services/NoteService.cs` — snapshot, list versions, restore, ordering, delete history.
- Create `Migrations/<ts>_NoteHistoryV29.cs` (generata via EF, poi ritoccata).

**WPF (`src/Recordum.WPF`):**
- Modify `ViewModels/HabitScreen/Notes/NoteRowViewModel.cs` — àncora + "modificata il".
- Create `ViewModels/HabitScreen/Notes/NoteVersionRowViewModel.cs`.
- Create `ViewModels/HabitScreen/Notes/NoteHistoryViewModel.cs`.
- Modify `ViewModels/HabitScreen/Notes/NotesViewModel.cs` — show/hide history.
- Modify `Views/HabitScreen/NotesTabView.xaml` — context menu + pannello cronologia.
- Modify `Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs` — nuove stringhe.

**Test:**
- Modify `tests/Recordum.Data.Tests/Integration/NoteServiceTests.cs` — versioning + ordering.
- Modify `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs` + `Schema/StreakBrokenAtV26Tests.cs` + `Migrations/ActiveTimerV25MigrationTests.cs` + `Migrations/PinScopeMigrationTests.cs` — bump "2.8"→"2.9".
- Modify `tests/Recordum.WPF.Tests/Fixtures/FakeNoteService.cs` — store versioni + nuovi metodi.
- Create `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteHistoryViewModelTests.cs`.
- Create `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteRowViewModelTests.cs`.
- Modify `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NotesViewModelTests.cs` — show/hide history.

---

## Task 1: Core — modello `NoteHistory`, DTO `NoteVersion`, navigazione

**Files:**
- Create: `src/Recordum.Core/Models/NoteHistory.cs`
- Create: `src/Recordum.Core/Abstractions/Services/Dtos/NoteVersion.cs`
- Modify: `src/Recordum.Core/Models/Note.cs`

- [ ] **Step 1: Create `NoteHistory.cs`**

```csharp
namespace Recordum.Core.Models;

/// <summary>Immutable snapshot of a <see cref="Note"/>'s body at a point in time.</summary>
public sealed class NoteHistory
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string BodyMd { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Note Note { get; set; } = null!;
}
```

- [ ] **Step 2: Create `NoteVersion.cs`**

```csharp
namespace Recordum.Core.Abstractions.Services.Dtos;

/// <summary>A single saved version of a note, newest-first in history listings.</summary>
public sealed record NoteVersion(Guid Id, string BodyMd, DateTime CreatedAt);
```

- [ ] **Step 3: Add navigation to `Note.cs`**

In `src/Recordum.Core/Models/Note.cs`, after the existing `Attachments` line:

```csharp
    public List<Attachment> Attachments { get; set; } = [];
    public List<NoteHistory> History { get; set; } = [];
```

- [ ] **Step 4: Build**

Run: `dotnet build src/Recordum.Core/Recordum.Core.csproj --nologo`
Expected: 0 errori.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.Core/Models/NoteHistory.cs src/Recordum.Core/Abstractions/Services/Dtos/NoteVersion.cs src/Recordum.Core/Models/Note.cs
git commit -m "feat(core): NoteHistory model + NoteVersion DTO + Note.History nav"
```

---

## Task 2: Data — configurazione EF + DbSet

**Files:**
- Create: `src/Recordum.Data/Configurations/NoteHistoryConfiguration.cs`
- Modify: `src/Recordum.Data/RecordumDbContext.cs`

- [ ] **Step 1: Create `NoteHistoryConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class NoteHistoryConfiguration : IEntityTypeConfiguration<NoteHistory>
{
    public void Configure(EntityTypeBuilder<NoteHistory> builder)
    {
        builder.HasKey(h => h.Id);
        // BodyMd has no max length (TEXT column)

        builder.HasOne(h => h.Note)
            .WithMany(n => n.History)
            .HasForeignKey(h => h.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => new { h.NoteId, h.CreatedAt });
    }
}
```

- [ ] **Step 2: Add `DbSet` to `RecordumDbContext.cs`**

After the `public DbSet<Note> Notes => Set<Note>();` line:

```csharp
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteHistory> NoteHistories => Set<NoteHistory>();
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Recordum.Data/Recordum.Data.csproj --nologo`
Expected: 0 errori.

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.Data/Configurations/NoteHistoryConfiguration.cs src/Recordum.Data/RecordumDbContext.cs
git commit -m "feat(data): note_history EF config (cascade + index) + DbSet"
```

---

## Task 3: Data — seed v2.9 + migrazione `NoteHistoryV29`

**Files:**
- Modify: `src/Recordum.Data/SeedData/AppSettingSeed.cs`
- Create: `src/Recordum.Data/Migrations/<ts>_NoteHistoryV29.cs` (via EF)
- Modify: `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs`
- Modify: `tests/Recordum.Data.Tests/Schema/StreakBrokenAtV26Tests.cs`
- Modify: `tests/Recordum.Data.Tests/Migrations/ActiveTimerV25MigrationTests.cs`
- Modify: `tests/Recordum.Data.Tests/Migrations/PinScopeMigrationTests.cs`

- [ ] **Step 1: Bump the seed in `AppSettingSeed.cs`**

Open `src/Recordum.Data/SeedData/AppSettingSeed.cs`. Near the top there is a set of `SeedDate...` constants (e.g. `SeedDateV28`). Add a v2.9 constant next to them:

```csharp
    private static readonly DateTime SeedDateV29 = new(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc);
```

Change the `schema_version` seed row (currently `Value = "2.8", UpdatedAt = SeedDateV28`):

```csharp
        new AppSetting { Key = "schema_version",              Value = "2.9",      UpdatedAt = SeedDateV29 },
```

- [ ] **Step 2: Generate the migration**

Run:
```bash
dotnet ef migrations add NoteHistoryV29 --project src/Recordum.Data --startup-project src/Recordum.WPF
```
Expected: crea `src/Recordum.Data/Migrations/<ts>_NoteHistoryV29.cs` (+ `.Designer.cs`) e aggiorna `RecordumDbContextModelSnapshot.cs`. La `Up` deve contenere `CreateTable("note_history", …)` con colonne `id, note_id, body_md, created_at`, FK cascade su `notes.id`, e `CreateIndex ix_note_history_note_id_created_at`.

- [ ] **Step 3: Verify generated SQL matches the spec**

Run: `dotnet ef migrations script BackupLogV27 NoteHistoryV29 --project src/Recordum.Data --startup-project src/Recordum.WPF` (oppure apri il file generato).
Verifica: tabella `note_history` con `note_id` FK `ON DELETE CASCADE` verso `notes(id)`, indice su `(note_id, created_at)`. La generazione include già la `UpdateData` di `schema_version` → "2.9" (perché il seed model è cambiato allo Step 1). Se NON la include, aggiungila a mano nella `Up` (mirror di `AbitudineCalendarV28`):

```csharp
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 6, 5, 0, 0, 0, 0, DateTimeKind.Utc), "2.9" });
```
e nella `Down`:
```csharp
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "2.8" });
```

- [ ] **Step 4: Update the existing schema-version assertions to "2.9"**

These tests assert the post-all-migrations / seed value and will now fail:

- `tests/Recordum.Data.Tests/Schema/SeedDataTests.cs`: rename `AppSetting_schema_version_is_2_8` → `AppSetting_schema_version_is_2_9` and change `Assert.Equal("2.8", schemaVersion);` → `Assert.Equal("2.9", schemaVersion);`.
- `tests/Recordum.Data.Tests/Schema/StreakBrokenAtV26Tests.cs`: the comment + `Assert.Equal("2.8", version);` → `Assert.Equal("2.9", version);`.
- `tests/Recordum.Data.Tests/Migrations/ActiveTimerV25MigrationTests.cs`: `Assert.Equal("2.8", setting.Value);` → `Assert.Equal("2.9", setting.Value);` (and the comment).
- `tests/Recordum.Data.Tests/Migrations/PinScopeMigrationTests.cs`: if it asserts schema_version after all migrations (search for `"2.8"`), bump to `"2.9"`.

To find any others: `git grep -n '"2.8"' tests/Recordum.Data.Tests` — update every assertion that checks schema_version *after all migrations*. (Do NOT change BackupLog/manifest tests that assert "2.7" — those are unrelated `SchemaVersion` fields.)

- [ ] **Step 5: Run the Data schema + migration tests**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~Schema|FullyQualifiedName~Migrations" --nologo`
Expected: PASS (schema_version ora "2.9" ovunque).

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Data/SeedData/AppSettingSeed.cs src/Recordum.Data/Migrations tests/Recordum.Data.Tests/Schema tests/Recordum.Data.Tests/Migrations
git commit -m "feat(data): NoteHistoryV29 migration + schema_version 2.9"
```

---

## Task 4: Data — snapshot, list versions, restore, delete

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/INoteService.cs`
- Modify: `src/Recordum.Data/Services/NoteService.cs`
- Test: `tests/Recordum.Data.Tests/Integration/NoteServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `NoteServiceTests.cs` (reuses the existing `SeedHabitAsync` / `NewService` helpers in that file):

```csharp
    [Fact]
    public async Task CreateAsync_with_body_records_first_version()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var sut = NewService(fx, out _);
        var id = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "v1" });

        var versions = await sut.ListVersionsAsync(id);
        versions.Should().ContainSingle().Which.BodyMd.Should().Be("v1");
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_with_empty_body_records_no_version()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var sut = NewService(fx, out _);
        var id = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "" });

        (await sut.ListVersionsAsync(id)).Should().BeEmpty();
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_records_version_only_when_body_changes()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var clock = (FakeClock)fx.Clock;
        var sut = NewService(fx, out _);
        var id = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "v1" });

        clock.Advance(TimeSpan.FromMinutes(1));
        await sut.UpdateAsync(id, n => n.BodyMd = "v2");            // body changed -> +1
        clock.Advance(TimeSpan.FromMinutes(1));
        await sut.UpdateAsync(id, n => n.TargetDate = new DateOnly(2026, 6, 5)); // no body change -> +0

        var versions = await sut.ListVersionsAsync(id);
        versions.Select(v => v.BodyMd).Should().Equal("v2", "v1"); // newest first
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task RestoreVersionAsync_sets_body_and_appends_new_version()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var clock = (FakeClock)fx.Clock;
        var sut = NewService(fx, out _);
        var id = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "v1" });
        clock.Advance(TimeSpan.FromMinutes(1));
        await sut.UpdateAsync(id, n => n.BodyMd = "v2");

        var v1 = (await sut.ListVersionsAsync(id)).Single(v => v.BodyMd == "v1");
        clock.Advance(TimeSpan.FromMinutes(1));
        await sut.RestoreVersionAsync(id, v1.Id);

        (await sut.GetAsync(id))!.BodyMd.Should().Be("v1");           // body reverted
        var versions = await sut.ListVersionsAsync(id);
        versions.Select(v => v.BodyMd).Should().Equal("v1", "v2", "v1"); // restore appended, nothing lost
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteAsync_removes_note_versions()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var sut = NewService(fx, out _);
        var id = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "v1" });

        await sut.DeleteAsync(id);

        (await sut.ListVersionsAsync(id)).Should().BeEmpty();
        await fx.DisposeAsync();
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~NoteServiceTests" --nologo`
Expected: FAIL (no `ListVersionsAsync`/`RestoreVersionAsync` — won't compile).

- [ ] **Step 3: Add interface members**

In `INoteService.cs` add:

```csharp
    ValueTask<IReadOnlyList<NoteVersion>> ListVersionsAsync(Guid noteId, CancellationToken ct = default);
    ValueTask RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken ct = default);
```

- [ ] **Step 4: Implement in `NoteService.cs`**

Add a private helper (uses the injected `clock` — remove the existing `_ = clock;` discard in `CreateAsync`):

```csharp
    private void AddVersion(RecordumDbContext db, Guid noteId, string bodyMd)
        => db.NoteHistories.Add(new NoteHistory
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            BodyMd = bodyMd,
            CreatedAt = clock.UtcNow.UtcDateTime,
        });
```

In `CreateAsync(Note draft, IReadOnlyList<Attachment>? attachments, …)`, replace the body around `SaveChangesAsync` so the version is added before saving (and drop `_ = clock;`):

```csharp
        if (!string.IsNullOrEmpty(draft.BodyMd)) AddVersion(db, draft.Id, draft.BodyMd);
        await db.SaveChangesAsync(ct);
        LogCreated(draft.Id, draft.HabitNodeId);
        eventBus.Publish(new NoteCreatedPayload(draft.Id, draft.HabitNodeId));
        return draft.Id;
```

In `UpdateAsync`:

```csharp
        var n = await db.Notes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return;
        var oldBody = n.BodyMd;
        mutate(n);
        if (n.BodyMd != oldBody) AddVersion(db, n.Id, n.BodyMd);
        await db.SaveChangesAsync(ct);
        eventBus.Publish(new NoteUpdatedPayload(n.Id, n.HabitNodeId));
```

In `DeleteAsync`, delete history rows explicitly before the note (the existing `ExecuteDeleteAsync` bypasses tracked cascade):

```csharp
        if (n is null) return;
        await db.NoteHistories.Where(h => h.NoteId == id).ExecuteDeleteAsync(ct);
        await db.Notes.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        eventBus.Publish(new NoteDeletedPayload(n.Id, n.HabitNodeId));
```

Add the two new methods at the end of the class:

```csharp
    public async ValueTask<IReadOnlyList<NoteVersion>> ListVersionsAsync(Guid noteId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.NoteHistories.AsNoTracking()
            .Where(h => h.NoteId == noteId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new NoteVersion(h.Id, h.BodyMd, h.CreatedAt))
            .ToListAsync(ct);
    }

    public async ValueTask RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var version = await db.NoteHistories.AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == versionId && h.NoteId == noteId, ct);
        if (version is null) return;
        await UpdateAsync(noteId, n => n.BodyMd = version.BodyMd, ct);
    }
```

Ensure `using Recordum.Core.Abstractions.Services.Dtos;` is present (it is, for `DayNoteCount`).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~NoteServiceTests" --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Core/Abstractions/Services/INoteService.cs src/Recordum.Data/Services/NoteService.cs tests/Recordum.Data.Tests/Integration/NoteServiceTests.cs
git commit -m "feat(data): note version snapshots + ListVersions/RestoreVersion"
```

---

## Task 5: Data — ordinamento `ListByHabitAsync` per àncora stabile

**Files:**
- Modify: `src/Recordum.Data/Services/NoteService.cs:72-79`
- Test: `tests/Recordum.Data.Tests/Integration/NoteServiceTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public async Task ListByHabitAsync_orders_by_anchor_not_updated_time()
    {
        var (fx, habitId) = await SeedHabitAsync();
        var clock = (FakeClock)fx.Clock;
        var sut = NewService(fx, out _);
        // older anchor day, but edited most recently
        var older = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "older", TargetDate = new DateOnly(2026, 6, 1) });
        var newer = await sut.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "newer", TargetDate = new DateOnly(2026, 6, 3) });
        clock.Advance(TimeSpan.FromHours(1));
        await sut.UpdateAsync(older, n => n.BodyMd = "older-edited"); // bumps UpdatedAtMs of the older-day note

        var list = await sut.ListByHabitAsync(habitId);
        list.Select(n => n.Id).Should().Equal(newer, older); // anchored by TargetDate, not by edit time
        await fx.DisposeAsync();
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~ListByHabitAsync_orders_by_anchor" --nologo`
Expected: FAIL (current order is by `UpdatedAtMs` → `older` first).

- [ ] **Step 3: Change the ordering**

Replace `ListByHabitAsync`:

```csharp
    public async ValueTask<IReadOnlyList<Note>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.HabitNodeId == habitNodeId)
            .ToListAsync(ct);
        // Stable anchor: assigned calendar day, else creation day. Client-side to avoid
        // translating the DateOnly null-coalesce into SQLite (note sets per habit are small).
        return notes
            .OrderByDescending(n => n.TargetDate ?? DateOnly.FromDateTime(n.CreatedAt))
            .ThenByDescending(n => n.UpdatedAtMs)
            .ToList();
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~NoteServiceTests" --nologo`
Expected: PASS (all NoteService tests).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.Data/Services/NoteService.cs tests/Recordum.Data.Tests/Integration/NoteServiceTests.cs
git commit -m "fix(data): order notes by stable anchor date, not last-modified"
```

---

## Task 6: WPF — estendere `FakeNoteService`

**Files:**
- Modify: `tests/Recordum.WPF.Tests/Fixtures/FakeNoteService.cs`

- [ ] **Step 1: Add a version store + the two new methods**

Add fields near the top of the class:

```csharp
    public Dictionary<Guid, List<NoteVersion>> Versions { get; } = new();
    public List<(Guid NoteId, Guid VersionId)> RestoreCalls { get; } = [];
```

Add the interface methods (place before `private void Throw()`):

```csharp
    public ValueTask<IReadOnlyList<NoteVersion>> ListVersionsAsync(Guid noteId, CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<NoteVersion> r = Versions.TryGetValue(noteId, out var v)
            ? v.OrderByDescending(x => x.CreatedAt).ToList()
            : [];
        return new(r);
    }

    public ValueTask RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken ct = default)
    {
        Throw();
        RestoreCalls.Add((noteId, versionId));
        if (Versions.TryGetValue(noteId, out var v)
            && v.FirstOrDefault(x => x.Id == versionId) is { } ver
            && Store.TryGetValue(noteId, out var n))
        {
            n.BodyMd = ver.BodyMd;
        }
        return ValueTask.CompletedTask;
    }
```

- [ ] **Step 2: Build the test project**

Run: `dotnet build tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --nologo`
Expected: 0 errori (FakeNoteService implementa di nuovo l'intera interfaccia).

- [ ] **Step 3: Commit**

```bash
git add tests/Recordum.WPF.Tests/Fixtures/FakeNoteService.cs
git commit -m "test(wpf): FakeNoteService version store + ListVersions/Restore"
```

---

## Task 7: WPF — `NoteRowViewModel` àncora + "modificata il"

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteRowViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteRowViewModelTests.cs` (new)

- [ ] **Step 1: Add resx strings**

In `src/Recordum.WPF/Resources/Strings.resx` add:
```xml
  <data name="Notes_ModifiedAt" xml:space="preserve">
    <value>Modified {0}</value>
  </data>
```
In `src/Recordum.WPF/Resources/Strings.it.resx` add:
```xml
  <data name="Notes_ModifiedAt" xml:space="preserve">
    <value>Modificata il {0}</value>
  </data>
```
In `src/Recordum.WPF/Resources/Strings.Designer.cs` add a property (mirror an existing one, e.g. `Notes_New`):
```csharp
        public static string Notes_ModifiedAt {
            get {
                return ResourceManager.GetString("Notes_ModifiedAt", resourceCulture);
            }
        }
```

- [ ] **Step 2: Write the failing test**

Create `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteRowViewModelTests.cs`:

```csharp
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class NoteRowViewModelTests
{
    private static long Ms(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();

    [Fact]
    public void AnchorDateDisplay_uses_TargetDate_when_present()
    {
        var created = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var row = new NoteRowViewModel(new Note
        {
            CreatedAt = created, UpdatedAtMs = Ms(created),
            TargetDate = new DateOnly(2026, 5, 20),
        });
        row.AnchorDateDisplay.Should().Be(new DateOnly(2026, 5, 20).ToString("d MMM yyyy", CultureInfo.CurrentCulture));
    }

    [Fact]
    public void AnchorDateDisplay_falls_back_to_CreatedAt_when_TargetDate_null()
    {
        var created = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var row = new NoteRowViewModel(new Note { CreatedAt = created, UpdatedAtMs = Ms(created), TargetDate = null });
        row.AnchorDateDisplay.Should().Be(new DateOnly(2026, 6, 1).ToString("d MMM yyyy", CultureInfo.CurrentCulture));
    }

    [Fact]
    public void WasModified_is_false_for_a_never_edited_note()
    {
        var created = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var row = new NoteRowViewModel(new Note { CreatedAt = created, UpdatedAtMs = Ms(created) });
        row.WasModified.Should().BeFalse();
    }

    [Fact]
    public void WasModified_is_true_after_an_edit()
    {
        var created = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var row = new NoteRowViewModel(new Note { CreatedAt = created, UpdatedAtMs = Ms(created.AddMinutes(5)) });
        row.WasModified.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~NoteRowViewModelTests" --nologo`
Expected: FAIL (no `AnchorDateDisplay`/`WasModified`).

- [ ] **Step 4: Implement in `NoteRowViewModel.cs`**

Replace the `DateBadge` property (and keep `UpdatedAtDisplay`) with:

```csharp
    private long CreatedAtMs =>
        new DateTimeOffset(DateTime.SpecifyKind(Note.CreatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    /// <summary>Stable anchor: assigned calendar day, else creation day. Never changes on edit.</summary>
    public string AnchorDateDisplay =>
        (Note.TargetDate ?? DateOnly.FromDateTime(Note.CreatedAt))
            .ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>True once the note has been edited after creation (1s tolerance for clock granularity).</summary>
    public bool WasModified => Note.UpdatedAtMs > CreatedAtMs + 1000;

    public string ModifiedAtDisplay => string.Format(
        CultureInfo.CurrentCulture,
        Recordum.WPF.Resources.Strings.Notes_ModifiedAt,
        DateTimeOffset.FromUnixTimeMilliseconds(Note.UpdatedAtMs).UtcDateTime
            .ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture));
```

Update `Refresh()`:

```csharp
    public void Refresh()
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(AnchorDateDisplay));
        OnPropertyChanged(nameof(WasModified));
        OnPropertyChanged(nameof(ModifiedAtDisplay));
        OnPropertyChanged(nameof(UpdatedAtDisplay));
    }
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~NoteRowViewModelTests" --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteRowViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteRowViewModelTests.cs
git commit -m "feat(wpf): note row stable anchor date + modified-at display"
```

---

## Task 8: WPF — `NoteVersionRowViewModel` + `NoteHistoryViewModel`

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteVersionRowViewModel.cs`
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteHistoryViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteHistoryViewModelTests.cs` (new)

- [ ] **Step 1: Add resx string `Notes_History_Current`**

Strings.resx: `<data name="Notes_History_Current" xml:space="preserve"><value>current</value></data>`
Strings.it.resx: `<data name="Notes_History_Current" xml:space="preserve"><value>corrente</value></data>`
Strings.Designer.cs: add a `Notes_History_Current` property (mirror Step 1 of Task 7).

- [ ] **Step 2: Create `NoteVersionRowViewModel.cs`**

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services.Dtos;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

public sealed partial class NoteVersionRowViewModel(NoteVersion version, bool isCurrent) : ObservableObject
{
    public NoteVersion Version { get; } = version;
    public Guid Id => Version.Id;
    public string BodyMd => Version.BodyMd;
    public bool IsCurrent { get; } = isCurrent;

    public string TimestampDisplay
    {
        get
        {
            var ts = Version.CreatedAt.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);
            return IsCurrent ? $"{ts} · {Recordum.WPF.Resources.Strings.Notes_History_Current}" : ts;
        }
    }

    public string Summary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BodyMd)) return Recordum.WPF.Resources.Strings.Notes_Untitled;
            var firstLine = BodyMd.Split('\n').Select(l => l.Trim())
                .FirstOrDefault(l => !string.IsNullOrEmpty(l)) ?? Recordum.WPF.Resources.Strings.Notes_Untitled;
            return firstLine.Length <= 40 ? firstLine : firstLine[..40] + "…";
        }
    }
}
```

- [ ] **Step 3: Write the failing test**

Create `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteHistoryViewModelTests.cs`:

```csharp
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class NoteHistoryViewModelTests
{
    private static (NoteHistoryViewModel vm, FakeNoteService svc, Guid noteId) CreateSut()
    {
        var svc = new FakeNoteService();
        var noteId = Guid.NewGuid();
        svc.Versions[noteId] =
        [
            new NoteVersion(Guid.NewGuid(), "v1", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)),
            new NoteVersion(Guid.NewGuid(), "v2", new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc)),
        ];
        return (new NoteHistoryViewModel(svc), svc, noteId);
    }

    [Fact]
    public async Task LoadAsync_orders_newest_first_and_marks_current()
    {
        var (vm, _, noteId) = CreateSut();
        await vm.LoadAsync(noteId);

        vm.Versions.Select(v => v.BodyMd).Should().Equal("v2", "v1");
        vm.Versions[0].IsCurrent.Should().BeTrue();
        vm.Versions[1].IsCurrent.Should().BeFalse();
        vm.SelectedVersion.Should().Be(vm.Versions[0]);
        vm.PreviewMarkdown.Should().Be("v2");
    }

    [Fact]
    public async Task RestoreCommand_restores_selected_and_raises_event()
    {
        var (vm, svc, noteId) = CreateSut();
        await vm.LoadAsync(noteId);
        vm.SelectedVersion = vm.Versions[1]; // "v1"
        var raised = false;
        vm.Restored += (_, _) => raised = true;

        await vm.RestoreCommand.ExecuteAsync(null);

        svc.RestoreCalls.Should().ContainSingle()
            .Which.Should().Be((noteId, vm.Versions[1].Id));
        raised.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreCommand_disabled_when_no_selection()
    {
        var (vm, _, noteId) = CreateSut();
        await vm.LoadAsync(noteId);
        vm.SelectedVersion = null;

        vm.RestoreCommand.CanExecute(null).Should().BeFalse();
    }
}
```

- [ ] **Step 4: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~NoteHistoryViewModelTests" --nologo`
Expected: FAIL (no `NoteHistoryViewModel`).

- [ ] **Step 5: Create `NoteHistoryViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

/// <summary>Inline version-history panel for one note: list of saved versions + read-only
/// preview + non-destructive restore. Raises <see cref="Restored"/> so the host can leave the panel.</summary>
public sealed partial class NoteHistoryViewModel(INoteService notes) : ObservableObject
{
    private Guid _noteId;

    public ObservableCollection<NoteVersionRowViewModel> Versions { get; } = [];

    [ObservableProperty] private NoteVersionRowViewModel? _selectedVersion;

    public string PreviewMarkdown => SelectedVersion?.BodyMd ?? string.Empty;

    public event EventHandler? Restored;

    public async Task LoadAsync(Guid noteId, CancellationToken ct = default)
    {
        _noteId = noteId;
        var versions = await notes.ListVersionsAsync(noteId, ct).ConfigureAwait(true);
        Versions.Clear();
        for (var i = 0; i < versions.Count; i++)
            Versions.Add(new NoteVersionRowViewModel(versions[i], isCurrent: i == 0));
        SelectedVersion = Versions.FirstOrDefault();
    }

    partial void OnSelectedVersionChanged(NoteVersionRowViewModel? value)
    {
        OnPropertyChanged(nameof(PreviewMarkdown));
        RestoreCommand.NotifyCanExecuteChanged();
    }

    private bool CanRestore() => SelectedVersion is not null;

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task Restore(CancellationToken ct)
    {
        if (SelectedVersion is not { } v) return;
        await notes.RestoreVersionAsync(_noteId, v.Id, ct).ConfigureAwait(true);
        Restored?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~NoteHistoryViewModelTests" --nologo`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteVersionRowViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteHistoryViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteHistoryViewModelTests.cs
git commit -m "feat(wpf): NoteHistoryViewModel + version row (preview + restore)"
```

---

## Task 9: WPF — `NotesViewModel` show/hide history

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NotesViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NotesViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `NotesViewModelTests.cs`:

```csharp
    [Fact]
    public async Task ShowHistoryCommand_loads_versions_and_enters_history()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "v2", UpdatedAtMs = 5 };
        svc.Store[note.Id] = note;
        svc.Versions[note.Id] =
        [
            new Recordum.Core.Abstractions.Services.Dtos.NoteVersion(Guid.NewGuid(), "v2", new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc)),
        ];
        await sut.LoadAsync(habitId);
        var row = sut.Notes.Single();

        await sut.ShowHistoryCommand.ExecuteAsync(row);

        sut.IsShowingHistory.Should().BeTrue();
        sut.SelectedNote.Should().Be(row);
        sut.History.Versions.Should().ContainSingle();
    }

    [Fact]
    public async Task History_restore_leaves_history_panel()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "v2", UpdatedAtMs = 5 };
        svc.Store[note.Id] = note;
        svc.Versions[note.Id] = [ new Recordum.Core.Abstractions.Services.Dtos.NoteVersion(Guid.NewGuid(), "v1", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)) ];
        await sut.LoadAsync(habitId);
        await sut.ShowHistoryCommand.ExecuteAsync(sut.Notes.Single());

        sut.History.SelectedVersion = sut.History.Versions[0];
        await sut.History.RestoreCommand.ExecuteAsync(null);

        sut.IsShowingHistory.Should().BeFalse();
    }

    [Fact]
    public async Task HideHistoryCommand_returns_to_editor()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "x", UpdatedAtMs = 1 };
        svc.Store[note.Id] = note;
        svc.Versions[note.Id] = [];
        await sut.LoadAsync(habitId);
        await sut.ShowHistoryCommand.ExecuteAsync(sut.Notes.Single());

        sut.HideHistoryCommand.Execute(null);

        sut.IsShowingHistory.Should().BeFalse();
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~NotesViewModelTests" --nologo`
Expected: FAIL (no `History`/`IsShowingHistory`/`ShowHistoryCommand`).

- [ ] **Step 3: Implement in `NotesViewModel.cs`**

Add the field and observable property (near the other `[ObservableProperty]` fields):

```csharp
    [ObservableProperty] private bool _isShowingHistory;

    public NoteHistoryViewModel History { get; }
```

In the constructor, after `Availability = availability;`, create the child and wire the event:

```csharp
        History = new NoteHistoryViewModel(notes);
        History.Restored += (_, _) => IsShowingHistory = false;
```

Add the commands (anywhere among the other `[RelayCommand]` methods):

```csharp
    [RelayCommand]
    private async Task ShowHistory(NoteRowViewModel? row, CancellationToken ct)
    {
        if (row is null) return;
        SelectedNote = row;
        await History.LoadAsync(row.Id, ct).ConfigureAwait(true);
        IsShowingHistory = true;
    }

    [RelayCommand]
    private void HideHistory() => IsShowingHistory = false;
```

(`notes` is the constructor's `INoteService notes` parameter, already stored as `_notes`; pass the field: `new NoteHistoryViewModel(_notes)`. Ensure the assignment to `_notes` happens before `History = …`.)

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~NotesViewModelTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Notes/NotesViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NotesViewModelTests.cs
git commit -m "feat(wpf): NotesViewModel show/hide version history"
```

---

## Task 10: WPF — XAML context menu + pannello cronologia inline

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml`

> XAML non è coperto da unit test → verifica con build + verifica visiva (Task 11).

- [ ] **Step 1: Add resx strings**

| key | en | it |
|---|---|---|
| `Notes_History` | History | Cronologia |
| `Notes_History_Back` | ‹ Back | ‹ Indietro |
| `Notes_History_Restore` | Restore | Ripristina |

Aggiungi i tre `<data>` in `Strings.resx` (en) e `Strings.it.resx` (it), e le tre proprietà in `Strings.Designer.cs` (mirror del template in Task 7 Step 1).

- [ ] **Step 2: Add a context menu to the note list item**

In `NotesTabView.xaml`, sul `StackPanel` radice dell'`ItemTemplate` della ListBox note (quello con `Margin="0,4"`), imposta `Tag` (per raggiungere il VM dal ContextMenu) e aggiungi il menu. Sostituisci l'apertura dello `StackPanel`:

```xml
                        <StackPanel Margin="0,4"
                                    Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType=ListBox}}">
                            <StackPanel.ContextMenu>
                                <ContextMenu>
                                    <MenuItem Header="{x:Static res:Strings.Notes_History}"
                                              Command="{Binding PlacementTarget.Tag.ShowHistoryCommand,
                                                        RelativeSource={RelativeSource AncestorType=ContextMenu}}"
                                              CommandParameter="{Binding PlacementTarget.DataContext,
                                                                 RelativeSource={RelativeSource AncestorType=ContextMenu}}"/>
                                </ContextMenu>
                            </StackPanel.ContextMenu>
```
Aggiorna anche i binding data dentro l'item: `DateBadge` non esiste più → usa `AnchorDateDisplay`; aggiungi la riga "modificata il" legata a `ModifiedAtDisplay` con `Visibility="{Binding WasModified, Converter={StaticResource BoolToVisible}}"`. Esempio della parte testuale dell'item:

```xml
                            <Border HorizontalAlignment="Left" Margin="0,0,0,2"
                                    Padding="5,1" CornerRadius="3"
                                    Background="{DynamicResource Brush.SurfaceElevated}">
                                <TextBlock Text="{Binding AnchorDateDisplay}"
                                           FontSize="{StaticResource FontSize.Small}"
                                           Foreground="{DynamicResource Brush.TextMuted}"/>
                            </Border>
                            <TextBlock Text="{Binding ModifiedAtDisplay}"
                                       FontSize="{StaticResource FontSize.Small}"
                                       Foreground="{DynamicResource Brush.TextMuted}"
                                       Visibility="{Binding WasModified, Converter={StaticResource BoolToVisible}}"/>
                            <TextBlock Text="{Binding Summary}" TextTrimming="CharacterEllipsis"/>
                        </StackPanel>
```

- [ ] **Step 3: Wrap the right column to toggle editor vs history**

La colonna destra è il `Grid Grid.Column="2" Margin="8"` con l'editor (Row 0) e la DockPanel dei bottoni (Row 1). Avvolgi l'attuale contenuto in un `Grid` esterno con due figli sovrapposti, commutati da `IsShowingHistory`. Struttura risultante:

```xml
        <Grid Grid.Column="2" Margin="8">
            <!-- Editor (stato normale) -->
            <Grid Visibility="{Binding IsShowingHistory, Converter={StaticResource InvBoolToVisible}}">
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <!-- ... CONTENUTO EDITOR ESISTENTE INVARIATO (MarkdownEditorControl + DockPanel bottoni) ... -->
            </Grid>

            <!-- Cronologia versioni (stato history) -->
            <Grid Visibility="{Binding IsShowingHistory, Converter={StaticResource BoolToVisible}}">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>
                <Button Grid.Row="0"
                        Content="{x:Static res:Strings.Notes_History_Back}"
                        Command="{Binding HideHistoryCommand}"
                        HorizontalAlignment="Left" Padding="8,4" Margin="0,0,0,8"/>
                <Grid Grid.Row="1" DataContext="{Binding History}">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="200"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <ListBox Grid.Column="0" BorderThickness="0"
                             ItemsSource="{Binding Versions}"
                             SelectedItem="{Binding SelectedVersion, Mode=TwoWay}">
                        <ListBox.ItemTemplate>
                            <DataTemplate DataType="{x:Type vm:NoteVersionRowViewModel}">
                                <StackPanel Margin="0,4">
                                    <TextBlock Text="{Binding TimestampDisplay}"
                                               FontSize="{StaticResource FontSize.Small}"
                                               Foreground="{DynamicResource Brush.TextMuted}"/>
                                    <TextBlock Text="{Binding Summary}" TextTrimming="CharacterEllipsis"/>
                                </StackPanel>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                    <DockPanel Grid.Column="1" Margin="8,0,0,0">
                        <Button DockPanel.Dock="Bottom"
                                Content="{x:Static res:Strings.Notes_History_Restore}"
                                Command="{Binding RestoreCommand}"
                                HorizontalAlignment="Left" Padding="10,4" Margin="0,8,0,0"/>
                        <ctrl:MarkdownEditorControl IsReadOnly="True"
                                                    Markdown="{Binding PreviewMarkdown, Mode=OneWay}"
                                                    Availability="{Binding DataContext.Availability,
                                                                   RelativeSource={RelativeSource AncestorType=UserControl}}"/>
                    </DockPanel>
                </Grid>
            </Grid>
        </Grid>
```

Assicurati che il root `UserControl` dichiari già `xmlns:ctrl="clr-namespace:Recordum.WPF.Controls"` (sì) e che `vm:` punti al namespace dei VM note (`xmlns:vm="clr-namespace:Recordum.WPF.ViewModels.HabitScreen.Notes"`, già presente).

- [ ] **Step 4: Build**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj --nologo`
Expected: 0 errori (no XAML parse errors).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml
git commit -m "feat(wpf): notes context menu + inline version-history panel"
```

---

## Task 11: Verifica finale, visiva, tracker

**Files:**
- Modify: `docs/phase-4-issues.md`

- [ ] **Step 1: Full suite**

Run:
```bash
dotnet test tests/Recordum.Core.Tests/Recordum.Core.Tests.csproj --nologo
dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --nologo
dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --nologo
```
Expected: tutto verde, build 0/0.

- [ ] **Step 2: Verifica visiva (skill `run`)**

Lancia l'app. Controlla:
1. Le note non saltano più in cima dopo una modifica; ogni nota mostra una data d'àncora + "modificata il …" solo se modificata.
2. Click destro su una nota → "Cronologia" → pannello inline con la lista versioni.
3. Selezionando una versione vedi l'anteprima read-only; "Ripristina" riporta il contenuto e torna all'editor; una nuova versione appare in cima.
4. Eliminando una nota e ricreandone una, nessun residuo di versioni.

- [ ] **Step 3: Update tracker**

In `docs/phase-4-issues.md`, aggiungi sotto "Stato al 2026-06-05" una riga: feature cronologia note + àncora data → implementata (schema v2.9 `note_history`), con i commit, e nota la verifica visiva utente.

- [ ] **Step 4: Commit**

```bash
git add docs/phase-4-issues.md
git commit -m "docs(phase-4): note version history implemented (schema v2.9)"
```

---

## Self-review notes

- **Spec coverage**: §3 schema → Task 2/3; §4 servizio (snapshot/list/restore/ordering) → Task 4/5; §5 date/display → Task 7; §6 UI (context menu, pannello inline, anteprima read-only OneWay, back/restore) → Task 8/9/10; §7 test → distribuiti + Task 11; §8 fuori scope rispettato (no pruning/diff/cronologia-da-DayNote).
- **Cascade**: la spec dice cascade DB; poiché `DeleteAsync` usa `ExecuteDeleteAsync` (bypassa il tracker) e il `DbFixture` usa `EnsureCreated` con FK non garantite, Task 4 elimina le righe `note_history` esplicitamente **e** dichiara `DeleteBehavior.Cascade` in Fluent (correttezza schema). Test di cascade in Task 4.
- **Tipi/firme coerenti**: `NoteVersion(Guid Id, string BodyMd, DateTime CreatedAt)`, `ListVersionsAsync`, `RestoreVersionAsync`, `History`, `IsShowingHistory`, `ShowHistoryCommand`, `HideHistoryCommand`, `AnchorDateDisplay`, `WasModified`, `ModifiedAtDisplay`, `NoteHistoryViewModel.Restored` — usati con gli stessi nomi in tutti i task.
- **schema_version**: bump del seed (model, per `EnsureCreated`) **e** della migration (per i DB esistenti) + aggiornamento di tutti i test che asseriscono "2.8" dopo le migrazioni.
