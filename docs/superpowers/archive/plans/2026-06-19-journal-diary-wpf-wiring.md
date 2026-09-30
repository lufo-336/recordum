# Journal/Diary WPF Wiring Implementation Plan (Plan 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the journal diary feature into the WPF UI on top of the Plan 1 data foundation — route every journal write/delete through `IDiaryService`, surface the note category (Diario/Veloce) and day+time in the Note column with a Day/Period toggle, and repopulate demo data via the diary path.

**Architecture:** The journal's QuickAdd markdown editor becomes the single on-screen diary editor (`JournalQuickAddViewModel` → `IDiaryService.SaveEntryAsync`); the brief "day note" editor (`DayNoteView`) is hidden for journals so there is no second diary editor. The Cronologia list shows each journal completion's paired note body (resolved by a batch `IDiaryService` lookup) and deletes the pair through `IDiaryService`. The Note column (Feature A) gains a category chip + day/time and a Day/Period filter mirroring `HabitRecordsViewModel`. The note↔completion pair invariant stays sealed inside `IDiaryService` — no WPF code writes the journal `Note`/`Completion` pair directly.

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`), xUnit + FluentAssertions, EF Core + SQLite (Data layer, for the one new `IDiaryService` method).

**Source spec:** `docs/superpowers/archive/specs/2026-06-19-journal-diary-feature-design.md` (Model B, approved 2026-06-19). Sections covered here: §6 (WPF entry points), §7 (Note column / Feature A), §9 (seeder repopulation), §10 (WPF testing). Sections §2/§3/§4/§5 were delivered by Plan 1 (`docs/superpowers/archive/plans/2026-06-19-journal-diary-data-foundation.md`).

**Design decision (2026-06-19, author):** For journal habits the DayNote brief editor is **hidden** — the QuickAdd markdown editor IS the diary entry editor. DayNote stays as the quick-note editor for non-journal habits (creates `Note(Veloce)`, which is already the column default after Plan 1). This supersedes the literal reading of spec §6 bullet 2 ("DayNote in journal context writes a diary entry").

**Already satisfied (verify on screen, no task):** "+ from Home on a journal opens the diary editor" (spec §6 bullet 4) is already implemented — `OpenScreenQuickActionHandler(HabitType.Journal)` is registered in `ServiceRegistrar.cs:154`, so the journal "+" opens the habit screen (whose Abitudine column hosts the diary QuickAdd) instead of quick-completing.

## Global Constraints

- **English-first user strings** in `.resx` (`Strings.resx`), Italian secondary (`Strings.it.resx`). Never hardcode user-facing text. The `Strings.Designer.cs` accessor is generated from the `.resx` — do not hand-edit it; adding a `<data>` entry regenerates the accessor on build (accessor name = key with `.` → `_`).
- **Never** `DateTime.UtcNow`/`Now`; all time via injected `IClock`. The diary write timestamp logic lives entirely in `DiaryService.SaveEntryAsync` (Plan 1) — WPF only passes a `DateOnly day`.
- **MVVM** with CommunityToolkit.Mvvm. UI-thread continuations use `.ConfigureAwait(true)` (match the surrounding files).
- **The note↔completion pair is owned solely by `IDiaryService`.** No WPF code may create/delete a journal `Note` or `Completion` directly, nor set `Completion.Notes` for a journal.
- **Reuse, don't reinvent:** the Day/Period toggle reuses `EntriesRecordsMode` + `EntriesPeriod` (Core) + `EntriesPeriodRange.Resolve` exactly as `HabitRecordsViewModel` does. The period UI resources (`EntriesPeriodValues`, `EntriesPeriodLabel`, `EnumToBool`, `EnumToVisibility`, `DateOnlyToDateTime`, `BoolToVisible`) are app-global in `App.xaml` and usable from any view.
- **`IDiaryService` is already registered** in DI (`ServiceRegistrar.cs`, Plan 1 Task 6, `AddScoped<IDiaryService, DiaryService>()`). New consumers just take it as a constructor dependency.
- Existing test baselines to keep green: **Data 342**, **WPF 567** (per the Plan 1 completion memory). This plan raises both.

## Interfaces from Plan 1 (consumed here, do not redefine)

```csharp
// Recordum.Core.Abstractions.Services.IDiaryService (Plan 1)
ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default);
ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default);
ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default);
ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default);
// enum Recordum.Core.Enums.NoteCategory { Diario = 0, Veloce = 1 }
// Note.Category (NoteCategory, default Veloce), Note.CompletionId (Guid?)
```

This plan **adds one method** to `IDiaryService` (Task 5): `GetEntryBodiesByCompletionsAsync`.

---

### Task 1: Category chip resource strings

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`
- Modify: `src/Recordum.WPF/Resources/Strings.it.resx`

**Interfaces:**
- Produces: `Strings.Notes_Category_Diary`, `Strings.Notes_Category_Quick` (generated accessors).
- Consumes: nothing.

- [ ] **Step 1: Add the English entries**

In `src/Recordum.WPF/Resources/Strings.resx`, add next to the other `Notes_*` / `Notes.*` entries (e.g. after the `Notes_ModifiedAt` data node):

```xml
  <data name="Notes.Category.Diary" xml:space="preserve">
    <value>Diary</value>
  </data>
  <data name="Notes.Category.Quick" xml:space="preserve">
    <value>Quick note</value>
  </data>
```

- [ ] **Step 2: Add the Italian entries**

In `src/Recordum.WPF/Resources/Strings.it.resx`, add the same keys with Italian values:

```xml
  <data name="Notes.Category.Diary" xml:space="preserve">
    <value>Diario</value>
  </data>
  <data name="Notes.Category.Quick" xml:space="preserve">
    <value>Nota veloce</value>
  </data>
```

- [ ] **Step 3: Build to regenerate the Strings accessor**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: Build succeeded. `Strings.Notes_Category_Diary` and `Strings.Notes_Category_Quick` now exist (verify via the generated `Strings.Designer.cs`, do not hand-edit it).

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "feat(wpf): note category chip strings (Diary/Quick, EN+IT)"
```

---

### Task 2: NoteRowViewModel exposes category + time-of-day

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteRowViewModel.cs`
- Create: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteRowViewModelTests.cs`

**Interfaces:**
- Consumes: `Note.Category` (Plan 1), `Strings.Notes_Category_Diary/Quick` (Task 1).
- Produces: `NoteRowViewModel.IsDiario` (bool), `.CategoryLabel` (string), `.CreatedAtTimeDisplay` (string `HH:mm`).

- [ ] **Step 1: Write the failing test**

Create `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteRowViewModelTests.cs`:

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class NoteRowViewModelTests
{
    [Fact]
    public void Diario_note_reports_IsDiario_and_diary_label()
    {
        var row = new NoteRowViewModel(new Note
        {
            Id = Guid.NewGuid(),
            HabitNodeId = Guid.NewGuid(),
            BodyMd = "entry",
            Category = NoteCategory.Diario,
        });

        row.IsDiario.Should().BeTrue();
        row.CategoryLabel.Should().Be(Recordum.WPF.Resources.Strings.Notes_Category_Diary);
    }

    [Fact]
    public void Veloce_note_reports_not_diario_and_quick_label()
    {
        var row = new NoteRowViewModel(new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" });

        row.IsDiario.Should().BeFalse();
        row.CategoryLabel.Should().Be(Recordum.WPF.Resources.Strings.Notes_Category_Quick);
    }

    [Fact]
    public void CreatedAtTimeDisplay_formats_hour_and_minute()
    {
        var row = new NoteRowViewModel(new Note
        {
            Id = Guid.NewGuid(),
            HabitNodeId = Guid.NewGuid(),
            BodyMd = "x",
            CreatedAt = new DateTime(2026, 6, 19, 8, 5, 0, DateTimeKind.Utc),
        });

        row.CreatedAtTimeDisplay.Should().Be("08:05");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~NoteRowViewModelTests`
Expected: FAIL / does not compile — `IsDiario`/`CategoryLabel`/`CreatedAtTimeDisplay` do not exist.

- [ ] **Step 3: Add the members**

In `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteRowViewModel.cs`, add `using Recordum.Core.Enums;` at the top and these members inside the class (after `AnchorDateDisplay`):

```csharp
    public bool IsDiario => Note.Category == NoteCategory.Diario;

    /// <summary>Chip label: "Diary" for paired diary entries, "Quick note" for free notes.</summary>
    public string CategoryLabel => IsDiario
        ? Recordum.WPF.Resources.Strings.Notes_Category_Diary
        : Recordum.WPF.Resources.Strings.Notes_Category_Quick;

    /// <summary>Time-of-day the note was written (HH:mm), from CreatedAt.</summary>
    public string CreatedAtTimeDisplay =>
        DateTime.SpecifyKind(Note.CreatedAt, DateTimeKind.Utc)
            .ToString("HH:mm", CultureInfo.InvariantCulture);
```

(`CultureInfo` is already imported in this file.)

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~NoteRowViewModelTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteRowViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteRowViewModelTests.cs
git commit -m "feat(wpf): NoteRowViewModel exposes category chip + time-of-day"
```

---

### Task 3: JournalQuickAddViewModel writes via IDiaryService

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/JournalQuickAddViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Create: `tests/Recordum.WPF.Tests/Fixtures/FakeDiaryService.cs`
- Modify: `tests/Recordum.WPF.Tests/Fixtures/HabitScreenViewModelFactory.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelTests.cs`

**Interfaces:**
- Consumes: `IDiaryService.SaveEntryAsync` (Plan 1).
- Produces: `JournalQuickAddViewModel(ICompletionService, IClock, IDiaryService)` ctor; `HabitScreenViewModel` ctor gains a trailing `IDiaryService diary` parameter; `FakeDiaryService` test fake.

- [ ] **Step 1: Create the test fake**

Create `tests/Recordum.WPF.Tests/Fixtures/FakeDiaryService.cs`:

```csharp
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

/// <summary>In-memory IDiaryService for VM tests. Models the note↔completion pair: SaveEntryAsync
/// stores a Diario note + a paired completion; DeleteEntryAsync removes both by either id.</summary>
public sealed class FakeDiaryService : IDiaryService
{
    public Dictionary<Guid, Note> Notes { get; } = new();
    public Dictionary<Guid, Completion> Completions { get; } = new();
    public List<(Guid HabitId, DateOnly Day, string Text)> SaveCalls { get; } = [];
    public List<Guid> DeleteCalls { get; } = [];
    public List<(Guid NoteId, string Text)> EditCalls { get; } = [];

    public ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default)
    {
        SaveCalls.Add((habitNodeId, day, text));
        var completionId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        Completions[completionId] = new Completion
        {
            Id = completionId,
            HabitNodeId = habitNodeId,
            CompletedAt = day.ToDateTime(TimeOnly.MinValue),
            CompletedAtMs = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds(),
        };
        Notes[noteId] = new Note
        {
            Id = noteId,
            HabitNodeId = habitNodeId,
            BodyMd = text,
            Category = NoteCategory.Diario,
            TargetDate = day,
            CompletionId = completionId,
        };
        return new(noteId);
    }

    public ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default)
    {
        DeleteCalls.Add(noteOrCompletionId);
        var note = Notes.Values.FirstOrDefault(n => n.Id == noteOrCompletionId || n.CompletionId == noteOrCompletionId);
        if (note is null) return ValueTask.CompletedTask;
        Notes.Remove(note.Id);
        if (note.CompletionId is { } cid) Completions.Remove(cid);
        return ValueTask.CompletedTask;
    }

    public ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default)
    {
        EditCalls.Add((noteId, text));
        if (Notes.TryGetValue(noteId, out var n)) n.BodyMd = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default)
        => new(Notes.Values.FirstOrDefault(n => n.CompletionId == completionId));

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetEntryBodiesByCompletionsAsync(
        IReadOnlyCollection<Guid> completionIds, CancellationToken ct = default)
    {
        var set = completionIds.ToHashSet();
        IReadOnlyDictionary<Guid, string> map = Notes.Values
            .Where(n => n.CompletionId is { } c && set.Contains(c))
            .ToDictionary(n => n.CompletionId!.Value, n => n.BodyMd);
        return new(map);
    }
}
```

(The `GetEntryBodiesByCompletionsAsync` member is defined on the interface in Task 5; this fake implements it up front so the fake compiles after Task 5 lands. Until Task 5 adds it to the interface, the build error here is expected and resolved by Task 5 — keep the order: Task 3 → 5 → 6. If executing strictly task-by-task with a green build between each, temporarily omit this last method and add it in Task 5 Step 4.)

- [ ] **Step 2: Update the JournalQuickAddViewModelTests to expect the diary path**

In `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelTests.cs`, replace the `JournalQuickAddViewModelTests` class body with:

```csharp
public class JournalQuickAddViewModelTests
{
    [Fact]
    public async Task SaveAsync_empty_notes_sets_ErrorMessage_and_skips_diary()
    {
        var diary = new FakeDiaryService();
        var sut = new JournalQuickAddViewModel(new FakeCompletionService(), new FakeClock(), diary)
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Journal },
            Notes = "",
        };

        await sut.SaveAsync();

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
        diary.SaveCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_with_notes_saves_a_diary_entry_for_the_completion_day()
    {
        var diary = new FakeDiaryService();
        var habitId = Guid.NewGuid();
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero));
        var sut = new JournalQuickAddViewModel(new FakeCompletionService(), clock, diary)
        {
            Node = new HabitNode { Id = habitId, HabitType = HabitType.Journal },
            Notes = "Hello.",
            CompletionDate = new DateTime(2026, 6, 17, 0, 0, 0, DateTimeKind.Utc),
        };

        await sut.SaveAsync();

        diary.SaveCalls.Should().ContainSingle();
        diary.SaveCalls[0].HabitId.Should().Be(habitId);
        diary.SaveCalls[0].Day.Should().Be(new DateOnly(2026, 6, 17));
        diary.SaveCalls[0].Text.Should().Be("Hello.");
    }
}
```

Ensure the file's usings include `using Recordum.WPF.Tests.Fixtures;` (it already references `FakeCompletionService`/`FakeClock`, so the namespace is present).

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~JournalQuickAddViewModelTests`
Expected: FAIL / does not compile — `JournalQuickAddViewModel` has no 3-arg constructor.

- [ ] **Step 4: Rewrite JournalQuickAddViewModel**

Replace `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/JournalQuickAddViewModel.cs` with:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Journal QuickAdd: writing the note IS what completes the habit. Routes through
/// IDiaryService so the Note(Diario)+Completion pair is created together (spec §6); never touches
/// Completion.Notes directly.</summary>
public sealed partial class JournalQuickAddViewModel(ICompletionService completions, IClock clock, IDiaryService diary)
    : QuickAddViewModelBase(completions, clock)
{
    [ObservableProperty] private string? _notes;

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        if (string.IsNullOrWhiteSpace(Notes))
        {
            ErrorMessage = Strings.QuickAdd_Error_NotesRequired;
            return;
        }
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var day = DateOnly.FromDateTime(CompletionDate.Date);
            var noteId = await diary.SaveEntryAsync(Node.Id, day, Notes!, ct).ConfigureAwait(true);
            OnSaved(noteId);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
```

Note: `OnSaved` raises the (currently unsubscribed) `CompletionSaved` event; the on-screen refresh is driven by the `CompletionRecorded`/`NoteCreated` events that `DiaryService` publishes and `HabitScreenViewModel` already subscribes to. Passing the note id is harmless.

- [ ] **Step 5: Thread IDiaryService into HabitScreenViewModel**

In `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`:

(a) Add a field near the other injected services (next to `_completions`):

```csharp
    private readonly IDiaryService _diary;
```

(b) Add `IDiaryService diary` as the **last** constructor parameter (after `IHabitTreeQueryService tree`), add a null check next to the others, and assign it:

```csharp
        ArgumentNullException.ThrowIfNull(diary);
        // ... (existing assignments) ...
        _diary = diary;
```

(c) In `BuildQuickAddAsync`, change the Journal arm:

```csharp
            HabitType.Journal => new JournalQuickAddViewModel(_completions, _clock, _diary),
```

- [ ] **Step 6: Update the HabitScreenViewModelFactory**

In `tests/Recordum.WPF.Tests/Fixtures/HabitScreenViewModelFactory.cs`, pass a `FakeDiaryService` as the new last argument of the `new HabitScreenViewModel(...)` call:

```csharp
        var vm = new HabitScreenViewModel(
            nodes, completions, timers, metrics, new FakeReferenceData(), configService,
            noteService, goals, new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            clock, bus, new FakeWebView2Availability(), settings, editor, new FakeHabitTreeQueryService(),
            new FakeDiaryService());
```

- [ ] **Step 7: Build and run the affected suites**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj` then
`dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~JournalQuickAddViewModelTests|FullyQualifiedName~HabitScreenViewModelTests"`
Expected: Build succeeded; the journal QuickAdd tests pass and the HabitScreen tests still pass.

(If the `FakeDiaryService.GetEntryBodiesByCompletionsAsync` method causes a compile error because Task 5 hasn't run yet, either run Task 5 first or temporarily comment that one method out — see Task 3 Step 1 note.)

- [ ] **Step 8: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/JournalQuickAddViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs tests/Recordum.WPF.Tests/Fixtures/FakeDiaryService.cs tests/Recordum.WPF.Tests/Fixtures/HabitScreenViewModelFactory.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/QuickAddViewModelTests.cs
git commit -m "feat(wpf): journal QuickAdd writes diary entries via IDiaryService"
```

---

### Task 4: Hide the DayNote editor for journals

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/DayNoteViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/DayNoteViewModelTests.cs`

**Interfaces:**
- Consumes: `HabitType.Journal`.
- Produces: `DayNoteViewModel.SetContext(Guid habitNodeId, bool isJournal)` (overload); `DayNoteViewModel.ShowEditor` (bool, false for journals).

- [ ] **Step 1: Write the failing test**

In `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/DayNoteViewModelTests.cs`, add these tests inside the class:

```csharp
    [Fact]
    public void SetContext_journal_hides_the_editor()
    {
        var sut = new DayNoteViewModel(new FakeNoteService(), new FakeClock());

        sut.SetContext(Guid.NewGuid(), isJournal: true);

        sut.ShowEditor.Should().BeFalse();
    }

    [Fact]
    public void SetContext_non_journal_shows_the_editor()
    {
        var sut = new DayNoteViewModel(new FakeNoteService(), new FakeClock());

        sut.SetContext(Guid.NewGuid(), isJournal: false);

        sut.ShowEditor.Should().BeTrue();
    }
```

If the file lacks them, add `using FluentAssertions;` and `using Recordum.WPF.Tests.Fixtures;` (the existing tests already use them).

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~DayNoteViewModelTests`
Expected: FAIL / does not compile — no 2-arg `SetContext`, no `ShowEditor`.

- [ ] **Step 3: Add ShowEditor + journal-aware SetContext**

In `src/Recordum.WPF/ViewModels/HabitScreen/Notes/DayNoteViewModel.cs`:

(a) Add an observable field next to the others:

```csharp
    [ObservableProperty] private bool _showEditor = true;
```

(b) Replace the existing `SetContext(Guid habitNodeId)` method with an overload pair (keep the old signature working for any caller that doesn't pass the flag):

```csharp
    public void SetContext(Guid habitNodeId) => SetContext(habitNodeId, isJournal: false);

    /// <summary>For journals the QuickAdd markdown editor is the diary editor, so the brief day-note
    /// editor is hidden (ShowEditor = false). For other types it stays as the quick-note editor.</summary>
    public void SetContext(Guid habitNodeId, bool isJournal)
    {
        _habitNodeId = habitNodeId;
        _current = null;
        Date = null;
        HasDate = false;
        Text = string.Empty;
        SummaryText = string.Empty;
        SummaryIconKey = null;
        ShowEditor = !isJournal;
    }
```

- [ ] **Step 4: Pass the journal flag from HabitScreenViewModel**

In `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`, in `LoadAsync`, change the `DayNote.SetContext(node.Id);` call (around line 393) to:

```csharp
        DayNote.SetContext(node.Id, node.HabitType == HabitType.Journal);
```

- [ ] **Step 5: Gate the DayNoteView visibility**

In `src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml`, add a `Visibility` to the root `<Border>` (its DataContext is the `DayNoteViewModel`, so `ShowEditor` binds directly):

```xml
    <Border Padding="12" BorderThickness="0,0,0,1" BorderBrush="{DynamicResource Brush.Border}"
            Visibility="{Binding ShowEditor, Converter={StaticResource BoolToVisible}}">
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~DayNoteViewModelTests`
Expected: all DayNote tests pass (new + existing).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Notes/DayNoteViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/DayNoteViewModelTests.cs
git commit -m "feat(wpf): hide DayNote editor for journals (QuickAdd is the diary editor)"
```

---

### Task 5: IDiaryService batch body lookup (Data)

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/IDiaryService.cs`
- Modify: `src/Recordum.Data/Services/DiaryService.cs`
- Modify: `tests/Recordum.Data.Tests/Integration/DiaryServiceTests.cs`

**Interfaces:**
- Consumes: `Note.CompletionId`, `Note.BodyMd`.
- Produces: `IDiaryService.GetEntryBodiesByCompletionsAsync(IReadOnlyCollection<Guid> completionIds, CancellationToken ct = default)` → `ValueTask<IReadOnlyDictionary<Guid, string>>` keyed by completion id.

- [ ] **Step 1: Add the method to the interface**

In `src/Recordum.Core/Abstractions/Services/IDiaryService.cs`, add inside the interface:

```csharp
    /// <summary>Maps each given completion id to its paired Diario note body, for joining the
    /// Cronologia list to diary text in one query (omits ids with no paired note).</summary>
    ValueTask<IReadOnlyDictionary<Guid, string>> GetEntryBodiesByCompletionsAsync(
        IReadOnlyCollection<Guid> completionIds, CancellationToken ct = default);
```

- [ ] **Step 2: Write the failing test**

In `tests/Recordum.Data.Tests/Integration/DiaryServiceTests.cs`, add inside `DiaryServiceTests`:

```csharp
    [Fact]
    public async Task GetEntryBodiesByCompletionsAsync_maps_completion_ids_to_bodies()
    {
        var (fx, habitId) = await SeedJournalAsync();
        var sut = NewService(fx, out _);
        var today = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);
        var n1 = await sut.SaveEntryAsync(habitId, today, "first");
        var n2 = await sut.SaveEntryAsync(habitId, today, "second");
        Guid c1, c2;
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            c1 = (await db.Notes.SingleAsync(n => n.Id == n1)).CompletionId!.Value;
            c2 = (await db.Notes.SingleAsync(n => n.Id == n2)).CompletionId!.Value;
        }

        var map = await sut.GetEntryBodiesByCompletionsAsync(new[] { c1, c2, Guid.NewGuid() });

        map.Should().HaveCount(2);
        map[c1].Should().Be("first");
        map[c2].Should().Be("second");
        await fx.DisposeAsync();
    }
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/Recordum.Data.Tests --filter FullyQualifiedName~DiaryServiceTests.GetEntryBodiesByCompletionsAsync_maps_completion_ids_to_bodies`
Expected: FAIL / does not compile — method not implemented.

- [ ] **Step 4: Implement it**

In `src/Recordum.Data/Services/DiaryService.cs`, add the method to the class:

```csharp
    public async ValueTask<IReadOnlyDictionary<Guid, string>> GetEntryBodiesByCompletionsAsync(
        IReadOnlyCollection<Guid> completionIds, CancellationToken ct = default)
    {
        if (completionIds.Count == 0) return new Dictionary<Guid, string>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = completionIds.ToHashSet();
        return await db.Notes.AsNoTracking()
            .Where(n => n.CompletionId != null && ids.Contains(n.CompletionId.Value))
            .ToDictionaryAsync(n => n.CompletionId!.Value, n => n.BodyMd, ct);
    }
```

If `FakeDiaryService` (Task 3) had that method commented out, restore it now so the WPF tests compile.

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/Recordum.Data.Tests --filter FullyQualifiedName~DiaryServiceTests`
Expected: all DiaryService tests pass (the new one + the Plan 1 ones).

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Core/Abstractions/Services/IDiaryService.cs src/Recordum.Data/Services/DiaryService.cs tests/Recordum.Data.Tests/Integration/DiaryServiceTests.cs tests/Recordum.WPF.Tests/Fixtures/FakeDiaryService.cs
git commit -m "feat(data): IDiaryService.GetEntryBodiesByCompletionsAsync (Cronologia join)"
```

---

### Task 6: Cronologia shows paired note body + deletes via IDiaryService

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/CompletionRowViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitRecordsViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CompletionListTests.cs`

**Interfaces:**
- Consumes: `IDiaryService.GetEntryBodiesByCompletionsAsync` + `.DeleteEntryAsync` (Task 5 / Plan 1).
- Produces: `CompletionListViewModel(ICompletionService, IMetricDefinitionService, IReferenceDataQueryService, IDiaryService)` ctor; `CompletionListViewModel.IsJournal` (bool); `CompletionRowViewModel` optional ctor params `string? overrideNotes`, `Func<Guid, CancellationToken, Task>? deleteOverride`, `bool canInlineEdit`; `CompletionRowViewModel.CanInlineEdit` (bool).

- [ ] **Step 1: Extend CompletionRowViewModel for the journal path**

In `src/Recordum.WPF/ViewModels/HabitScreen/CompletionRowViewModel.cs`:

(a) Add two fields next to `_onDeleted`:

```csharp
    private readonly string? _overrideNotes;
    private readonly Func<Guid, CancellationToken, Task>? _deleteOverride;
```

(b) Add a public property near `Notes`:

```csharp
    /// <summary>False for journal rows: the diary text is edited in the Note column, not inline here.</summary>
    public bool CanInlineEdit { get; }
```

(c) Change `Notes` to prefer the override:

```csharp
    public string? Notes => _overrideNotes ?? Completion.Notes;
```

(d) Replace the constructor with one that accepts the new optional parameters (preserving the existing call sites that pass `values`, `onDeleted`, `fieldDisplays`):

```csharp
    public CompletionRowViewModel(
        Completion completion,
        ICompletionService completions,
        IReadOnlyList<CompletionValue>? values = null,
        Action<CompletionRowViewModel>? onDeleted = null,
        IReadOnlyList<string>? fieldDisplays = null,
        string? overrideNotes = null,
        Func<Guid, CancellationToken, Task>? deleteOverride = null,
        bool canInlineEdit = true)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(completions);
        Completion = completion;
        _completions = completions;
        _onDeleted = onDeleted;
        Values = values ?? [];
        FieldDisplays = fieldDisplays ?? [];
        _overrideNotes = overrideNotes;
        _deleteOverride = deleteOverride;
        CanInlineEdit = canInlineEdit;
        EditedNotes = completion.Notes;
        EditedDate = completion.CompletedAt;
    }
```

(e) Route delete through the override when present:

```csharp
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken ct)
    {
        if (_deleteOverride is not null)
            await _deleteOverride(Id, ct).ConfigureAwait(true);
        else
            await _completions.DeleteAsync(Id, ct).ConfigureAwait(true);
        _onDeleted?.Invoke(this);
    }
```

- [ ] **Step 2: Make CompletionListViewModel journal-aware**

In `src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs`:

(a) Add the field + property + ctor parameter:

```csharp
    private readonly IDiaryService _diary;

    public Guid? NodeId { get; private set; }
    public ObservableCollection<CompletionRowViewModel> Rows { get; } = [];
    public int Take { get; init; } = 20;

    /// <summary>When true (journal habit), rows show the paired diary note body and delete the pair
    /// via IDiaryService instead of the bare completion.</summary>
    public bool IsJournal { get; set; }

    public CompletionListViewModel(ICompletionService completions, IMetricDefinitionService metrics,
        IReferenceDataQueryService refData, IDiaryService diary)
    {
        _completions = completions;
        _metrics = metrics;
        _refData = refData;
        _diary = diary;
    }
```

(b) Make `Populate` async so it can batch-resolve diary bodies, and route delete. Replace `Populate` and update its two callers (`LoadAsync`, `LoadRangeAsync`) to `await`:

```csharp
    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var list = await _completions.ListByHabitAsync(habitNodeId, take: Take, ct: ct).ConfigureAwait(true);
        var lookup = await BuildLookupAsync(habitNodeId, ct).ConfigureAwait(true);
        await PopulateAsync(list, lookup, ct).ConfigureAwait(true);
    }

    public async Task LoadRangeAsync(Guid habitNodeId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var fromTs = from is { } f ? new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : (DateTimeOffset?)null;
        var toTs = to is { } t ? new DateTimeOffset(t.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero) : (DateTimeOffset?)null;
        var list = await _completions.ListByHabitAsync(habitNodeId, fromTs, toTs, ct: ct).ConfigureAwait(true);
        var lookup = await BuildLookupAsync(habitNodeId, ct).ConfigureAwait(true);
        await PopulateAsync(list, lookup, ct).ConfigureAwait(true);
    }

    private async Task PopulateAsync(IReadOnlyList<Completion> list, Dictionary<Guid, MetricDisplay> lookup, CancellationToken ct)
    {
        Rows.Clear();
        IReadOnlyDictionary<Guid, string> bodies = IsJournal
            ? await _diary.GetEntryBodiesByCompletionsAsync(list.Select(c => c.Id).ToList(), ct).ConfigureAwait(true)
            : new Dictionary<Guid, string>();
        foreach (var c in list.OrderByDescending(c => c.CompletedAtMs))
        {
            string? overrideNotes = IsJournal && bodies.TryGetValue(c.Id, out var body) ? body : null;
            Func<Guid, CancellationToken, Task>? del = IsJournal
                ? (id, token) => _diary.DeleteEntryAsync(id, token).AsTask()
                : null;
            Rows.Add(new CompletionRowViewModel(c, _completions, c.Values,
                onDeleted: row => Rows.Remove(row),
                fieldDisplays: BuildFieldDisplays(c.Values, lookup),
                overrideNotes: overrideNotes,
                deleteOverride: del,
                canInlineEdit: !IsJournal));
        }
    }
```

Delete the old synchronous `Populate` method (replaced by `PopulateAsync`). `LoadDayAsync` already delegates to `LoadRangeAsync`, so it needs no change.

(c) Add the using if missing: `using Recordum.Core.Abstractions.Services;` is already present (the ctor uses those services).

- [ ] **Step 3: Thread the journal flag through HabitRecordsViewModel**

In `src/Recordum.WPF/ViewModels/HabitScreen/HabitRecordsViewModel.cs`, change `SetContext` to set the inner list's flag:

```csharp
    public void SetContext(Guid nodeId, bool isJournal)
    {
        _nodeId = nodeId;
        Records.IsJournal = isJournal;
    }
```

(Update the single existing caller in `HabitScreenViewModel` in the next step. There is only one caller — `Records.SetContext(node.Id)`.)

- [ ] **Step 4: Wire the flags + diary in HabitScreenViewModel**

In `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`:

(a) Pass `_diary` to both `CompletionListViewModel` constructions (around lines 311–312):

```csharp
        CompletionList = new CompletionListViewModel(completions, metrics, refData, diary);
        Records = new HabitRecordsViewModel(new CompletionListViewModel(completions, metrics, refData, diary), clock);
```

(Note `diary` is the ctor parameter; this is in the constructor body where `_diary` may not be assigned yet — use the parameter `diary` directly.)

(b) In `LoadAsync`, set the journal flag on `CompletionList` before it loads, and update the `Records.SetContext` call. Set `CompletionList.IsJournal` right after `Node = node;` resolves (before the `Task.WhenAll` that includes `CompletionList.LoadAsync`):

```csharp
        CompletionList.IsJournal = node.HabitType == HabitType.Journal;
```

Place this line just before the `var configTask = ...` block (line ~374), so it is set before `CompletionList.LoadAsync(node.Id, ct)` runs in the `Task.WhenAll`.

(c) Change `Records.SetContext(node.Id);` (line ~394) to:

```csharp
        Records.SetContext(node.Id, node.HabitType == HabitType.Journal);
```

- [ ] **Step 5: Hide the inline-edit (✎) button for journal rows in XAML**

In `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`, in the `CompletionRowViewModel` DataTemplate, add a `Visibility` to the edit `Button` (the one with `Content="✎"`, around line 104):

```xml
                                    <Button Grid.Column="1"
                                            Content="✎"
                                            Command="{Binding BeginEditCommand}"
                                            Width="28" Height="28"
                                            Background="Transparent" BorderThickness="0"
                                            Visibility="{Binding CanInlineEdit, Converter={StaticResource BoolToVisible}}"/>
```

(The `Notes` TextBlock at line 99 already shows `{Binding Notes}` — for journals it now shows the paired diary body via the override, no XAML change there.)

- [ ] **Step 6: Write the failing CompletionList journal tests**

In `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CompletionListTests.cs`, first update the existing SUT factory to pass a `FakeDiaryService` to the `new CompletionListViewModel(...)` constructor (find the construction and add the 4th argument). Then add:

```csharp
    [Fact]
    public async Task Journal_rows_show_paired_note_body_and_hide_inline_edit()
    {
        var completions = new FakeCompletionService();
        var diary = new FakeDiaryService();
        var habitId = Guid.NewGuid();
        // Pair a completion with a diary note via the fake's store.
        var noteId = await diary.SaveEntryAsync(habitId, new DateOnly(2026, 6, 19), "diary body");
        var completionId = diary.Notes[noteId].CompletionId!.Value;
        completions.Store[completionId] = diary.Completions[completionId];

        var sut = new CompletionListViewModel(completions, new FakeMetricDefinitionService(), new FakeReferenceData(), diary)
        {
            IsJournal = true,
        };
        await sut.LoadAsync(habitId);

        var row = sut.Rows.Should().ContainSingle().Subject;
        row.Notes.Should().Be("diary body");
        row.CanInlineEdit.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_journal_row_routes_through_IDiaryService()
    {
        var completions = new FakeCompletionService();
        var diary = new FakeDiaryService();
        var habitId = Guid.NewGuid();
        var noteId = await diary.SaveEntryAsync(habitId, new DateOnly(2026, 6, 19), "x");
        var completionId = diary.Notes[noteId].CompletionId!.Value;
        completions.Store[completionId] = diary.Completions[completionId];

        var sut = new CompletionListViewModel(completions, new FakeMetricDefinitionService(), new FakeReferenceData(), diary)
        {
            IsJournal = true,
        };
        await sut.LoadAsync(habitId);
        await sut.Rows.Single().DeleteCommand.ExecuteAsync(null);

        diary.DeleteCalls.Should().Contain(completionId);
        completions.DeleteCalls.Should().BeEmpty(); // not deleted directly
        sut.Rows.Should().BeEmpty();
    }
```

Match the existing test file's class name and usings (`using Recordum.WPF.Tests.Fixtures;`, `using FluentAssertions;`). The exact names of `FakeMetricDefinitionService`/`FakeReferenceData` are taken from `HabitScreenViewModelFactory.cs`.

- [ ] **Step 7: Build and run**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj` then
`dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~CompletionListTests|FullyQualifiedName~HabitRecordsViewModelTests|FullyQualifiedName~HabitScreenViewModelTests"`
Expected: Build succeeded; all listed suites pass (including the non-journal regression tests — `IsJournal` defaults false, so existing behavior is unchanged).

- [ ] **Step 8: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/CompletionRowViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/CompletionListViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/HabitRecordsViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml tests/Recordum.WPF.Tests/ViewModels/HabitScreen/CompletionListTests.cs
git commit -m "feat(wpf): journal Cronologia shows paired diary body, deletes via IDiaryService"
```

---

### Task 7: NotesViewModel Day/Period filter (Feature A)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NotesViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NotesViewModelTests.cs`

**Interfaces:**
- Consumes: `EntriesRecordsMode`, `EntriesPeriod`, `EntriesPeriodRange.Resolve` (Core), `IClock`.
- Produces: `NotesViewModel.Mode` (`EntriesRecordsMode`), `.SelectedPeriod` (`EntriesPeriod`), `.CustomFrom`/`.CustomTo` (`DateOnly?`), `.SetCalendarDay(DateOnly)`. Default `Mode = Periodo`, `SelectedPeriod = Tutto` → shows all notes (current behavior preserved).

- [ ] **Step 1: Write the failing tests**

In `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NotesViewModelTests.cs`, add:

```csharp
    [Fact]
    public async Task Default_period_All_shows_every_note()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        svc.Store[Guid.NewGuid()] = new Note { HabitNodeId = habitId, BodyMd = "a", UpdatedAtMs = 1, TargetDate = new DateOnly(2026, 1, 1) };
        svc.Store[Guid.NewGuid()] = new Note { HabitNodeId = habitId, BodyMd = "b", UpdatedAtMs = 2, TargetDate = new DateOnly(2026, 6, 1) };

        await sut.LoadAsync(habitId);

        sut.Notes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Day_mode_filters_notes_to_the_selected_calendar_day()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        svc.Store[Guid.NewGuid()] = new Note { HabitNodeId = habitId, BodyMd = "kept", UpdatedAtMs = 2, TargetDate = new DateOnly(2026, 6, 19) };
        svc.Store[Guid.NewGuid()] = new Note { HabitNodeId = habitId, BodyMd = "other", UpdatedAtMs = 1, TargetDate = new DateOnly(2026, 6, 18) };
        await sut.LoadAsync(habitId);

        sut.SetCalendarDay(new DateOnly(2026, 6, 19));
        sut.Mode = EntriesRecordsMode.Giorno;
        await Task.Delay(20);

        sut.Notes.Should().ContainSingle().Which.Note.BodyMd.Should().Be("kept");
    }
```

Add `using Recordum.WPF.ViewModels.HabitScreen;` (for `EntriesRecordsMode`) to the test file's usings.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~NotesViewModelTests`
Expected: FAIL / does not compile — no `Mode`/`SetCalendarDay`.

- [ ] **Step 3: Add the filter machinery to NotesViewModel**

In `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NotesViewModel.cs`:

(a) Add usings:

```csharp
using Recordum.Core.Enums;
using Recordum.Core.Services;
```

(b) Add a backing snapshot field next to `_habitNodeId`:

```csharp
    private List<Note> _all = [];
    private DateOnly? _calendarDay;
```

(c) Add the observable filter properties next to the existing `[ObservableProperty]` block:

```csharp
    [ObservableProperty] private EntriesRecordsMode _mode = EntriesRecordsMode.Periodo;
    [ObservableProperty] private EntriesPeriod _selectedPeriod = EntriesPeriod.Tutto;
    [ObservableProperty] private DateOnly? _customFrom;
    [ObservableProperty] private DateOnly? _customTo;
```

(d) In `LoadAsync`, snapshot the full list, then apply the filter. Replace the existing `LoadAsync` body's list handling:

```csharp
    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        _habitNodeId = habitNodeId;
        var list = await _notes.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(true);
        _all = list.ToList();
        ApplyFilter();
        SelectedNote = null;
        EditorMarkdown = string.Empty;
    }
```

(e) Add the calendar-day feeder + filter logic + change handlers:

```csharp
    /// <summary>Mirrors the calendar selection into Day mode (parallels HabitRecordsViewModel).</summary>
    public void SetCalendarDay(DateOnly day)
    {
        _calendarDay = day;
        if (Mode == EntriesRecordsMode.Giorno) ApplyFilter();
    }

    private static DateOnly AnchorOf(Note n) => n.TargetDate ?? DateOnly.FromDateTime(n.CreatedAt);

    private void ApplyFilter()
    {
        IEnumerable<Note> filtered;
        if (Mode == EntriesRecordsMode.Giorno)
        {
            filtered = _calendarDay is { } d ? _all.Where(n => AnchorOf(n) == d) : [];
        }
        else
        {
            var today = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, _clock.LocalTimeZone));
            var (from, to) = EntriesPeriodRange.Resolve(SelectedPeriod, today, CustomFrom, CustomTo);
            filtered = _all.Where(n =>
            {
                var a = AnchorOf(n);
                return (from is null || a >= from) && (to is null || a <= to);
            });
        }

        var keptSelection = SelectedNote?.Id;
        Notes.Clear();
        foreach (var n in filtered.OrderByDescending(n => n.UpdatedAtMs)) Notes.Add(new NoteRowViewModel(n));
        if (keptSelection is { } sel) SelectedNote = Notes.FirstOrDefault(r => r.Id == sel);
    }

    partial void OnModeChanged(EntriesRecordsMode value) => ApplyFilter();
    partial void OnSelectedPeriodChanged(EntriesPeriod value) { if (Mode == EntriesRecordsMode.Periodo) ApplyFilter(); }
    partial void OnCustomFromChanged(DateOnly? value) => ReloadIfCustom();
    partial void OnCustomToChanged(DateOnly? value) => ReloadIfCustom();

    private void ReloadIfCustom()
    {
        if (Mode == EntriesRecordsMode.Periodo && SelectedPeriod == EntriesPeriod.Personalizzato) ApplyFilter();
    }
```

(f) In `OnNoteEvent` (the event-refresh handler), it calls `LoadAsync` which re-snapshots and re-filters — no change needed; it already preserves selection. In `New` (the quick-note create), the created `Note` has no `Category` set, so it defaults to `Veloce` (Plan 1) — correct, no change. After creating in `New`, also add the new row to `_all` so a subsequent filter pass keeps it:

```csharp
    [RelayCommand]
    private async Task New(CancellationToken ct)
    {
        var draft = new Note { HabitNodeId = _habitNodeId, BodyMd = string.Empty, UpdatedAtMs = _clock.UtcNowMs };
        await _notes.CreateAsync(draft, ct).ConfigureAwait(true);
        _all.Insert(0, draft);
        var row = new NoteRowViewModel(draft);
        Notes.Insert(0, row);
        SelectedNote = row;
    }
```

(g) In `ConfirmDelete`, also remove from `_all`:

```csharp
        await _notes.DeleteAsync(row.Id, ct).ConfigureAwait(true);
        _all.RemoveAll(n => n.Id == row.Id);
        var idx = Notes.IndexOf(row);
```

- [ ] **Step 4: Feed the calendar day from HabitScreenViewModel**

In `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`:

(a) In `LoadAsync`, where the selected day is pushed to DayNote/Records (line ~395 block), also feed Notes:

```csharp
        if (Calendar.SelectedDate is { } sel)
        {
            Notes.SetCalendarDay(sel);
            await DayNote.LoadDayAsync(sel, ct).ConfigureAwait(true);
            await PushDaySummaryAsync(sel, ct).ConfigureAwait(true);
            await Records.ShowDayAsync(sel, ct).ConfigureAwait(true);
        }
```

(b) In `OnCalendarDateSelected` handler (around line 559, where `QuickAdd.CompletionDate` and `DayNote.LoadDayAsync(date)` are set), add:

```csharp
        Notes.SetCalendarDay(date);
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~NotesViewModelTests`
Expected: all NotesViewModel tests pass (new + existing — note the existing `LoadAsync_populates_Notes_ordered_by_updated_desc` still passes because default Period=Tutto resolves to unbounded and keeps the updated-desc ordering).

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Notes/NotesViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NotesViewModelTests.cs
git commit -m "feat(wpf): Note column Day/Period filter (Feature A) reusing EntriesPeriodRange"
```

---

### Task 8: Note column UI — category chip, day/time, period toggle

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml`

**Interfaces:**
- Consumes: `NoteRowViewModel.CategoryLabel/IsDiario/AnchorDateDisplay/CreatedAtTimeDisplay` (Task 2), `NotesViewModel.Mode/SelectedPeriod/CustomFrom/CustomTo` (Task 7), app-global resources `EntriesPeriodValues`/`EntriesPeriodLabel`/`EnumToBool`/`EnumToVisibility`/`DateOnlyToDateTime`/`BoolToVisible` and `Strings.Records_Mode_Day`/`Records_Mode_Period`/`Period_From`/`Period_To`.
- Produces: UI only (no public surface).

- [ ] **Step 1: Add the period toggle header above the note list**

In `src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml`, the left column is a `<DockPanel Grid.Column="0">` containing a "New" button docked top and a `<ListBox>`. Add a toggle panel docked top **after** the New button and **before** the ListBox:

```xml
        <DockPanel Grid.Column="0">
            <Button DockPanel.Dock="Top"
                    Content="{x:Static res:Strings.Notes_New}"
                    Command="{Binding NewCommand}"
                    Padding="6,4" Margin="6"/>
            <StackPanel DockPanel.Dock="Top" Margin="6,0,6,6">
                <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                    <RadioButton Content="{x:Static res:Strings.Records_Mode_Day}"
                                 GroupName="NotesMode"
                                 IsChecked="{Binding Mode, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:EntriesRecordsModeProxy.Giorno}}"/>
                    <RadioButton Content="{x:Static res:Strings.Records_Mode_Period}"
                                 GroupName="NotesMode" Margin="8,0,0,0"
                                 IsChecked="{Binding Mode, Converter={StaticResource EnumToBool}, ConverterParameter={x:Static vm:EntriesRecordsModeProxy.Periodo}}"/>
                </StackPanel>
                <StackPanel Visibility="{Binding Mode, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static vm:EntriesRecordsModeProxy.Periodo}}">
                    <ComboBox ItemsSource="{Binding Source={StaticResource EntriesPeriodValues}}"
                              SelectedItem="{Binding SelectedPeriod}">
                        <ComboBox.ItemTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding Converter={StaticResource EntriesPeriodLabel}}"/>
                            </DataTemplate>
                        </ComboBox.ItemTemplate>
                    </ComboBox>
                    <StackPanel Orientation="Horizontal" Margin="0,4,0,0"
                                Visibility="{Binding SelectedPeriod, Converter={StaticResource EnumToVisibility}, ConverterParameter={x:Static enums:EntriesPeriod.Personalizzato}}">
                        <TextBlock Text="{x:Static res:Strings.Period_From}" VerticalAlignment="Center" Margin="0,0,4,0"/>
                        <DatePicker SelectedDate="{Binding CustomFrom, Converter={StaticResource DateOnlyToDateTime}}"/>
                        <TextBlock Text="{x:Static res:Strings.Period_To}" VerticalAlignment="Center" Margin="6,0,4,0"/>
                        <DatePicker SelectedDate="{Binding CustomTo, Converter={StaticResource DateOnlyToDateTime}}"/>
                    </StackPanel>
                </StackPanel>
            </StackPanel>
            <ListBox ItemsSource="{Binding Notes}" ... >
```

The `vm:` namespace currently points at `...ViewModels.HabitScreen.Notes`, but `EntriesRecordsMode` and `EntriesPeriod` live in `...ViewModels.HabitScreen` and `Recordum.Core.Enums`/`Recordum.Core.Services` respectively. Add the two xmlns at the root `<UserControl>`:

```xml
             xmlns:vmh="clr-namespace:Recordum.WPF.ViewModels.HabitScreen"
             xmlns:enums="clr-namespace:Recordum.Core.Enums;assembly=Recordum.Core"
```

Then use `vmh:EntriesRecordsMode.Giorno` / `vmh:EntriesRecordsMode.Periodo` in the `ConverterParameter`s above (replace the placeholder `vm:EntriesRecordsModeProxy.*` with `vmh:EntriesRecordsMode.*`). For `EntriesPeriod` use `enums:EntriesPeriod.Personalizzato` — **verify** the enum's namespace: check `src/Recordum.Core/Services/EntriesPeriodRange.cs` for where `EntriesPeriod` is declared and set the `enums` xmlns to that exact namespace/assembly (the HabitScreen.xaml `EnumToVisibility ... EntriesPeriod.Personalizzato` binding already references it as `enums:` — copy that exact xmlns declaration from `HabitScreen.xaml`).

- [ ] **Step 2: Add the category chip + day/time to the note row template**

In the same file, replace the note row `DataTemplate` (the `<StackPanel Margin="0,4" ...>` inside `ListBox.ItemTemplate`) so each row shows a category chip and the day + time. Keep the existing ContextMenu and Summary; add the chip row:

```xml
                    <DataTemplate DataType="{x:Type vm:NoteRowViewModel}">
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
                            <StackPanel Orientation="Horizontal" Margin="0,0,0,2">
                                <Border Padding="5,1" CornerRadius="3" Margin="0,0,4,0"
                                        Background="{DynamicResource Brush.SurfaceElevated}">
                                    <TextBlock Text="{Binding CategoryLabel}"
                                               FontSize="{StaticResource FontSize.Small}"
                                               Foreground="{DynamicResource Brush.TextMuted}"/>
                                </Border>
                                <Border Padding="5,1" CornerRadius="3"
                                        Background="{DynamicResource Brush.SurfaceElevated}">
                                    <TextBlock FontSize="{StaticResource FontSize.Small}"
                                               Foreground="{DynamicResource Brush.TextMuted}">
                                        <Run Text="{Binding AnchorDateDisplay, Mode=OneWay}"/>
                                        <Run Text="{Binding CreatedAtTimeDisplay, Mode=OneWay}"/>
                                    </TextBlock>
                                </Border>
                            </StackPanel>
                            <TextBlock Text="{Binding ModifiedAtDisplay}"
                                       FontSize="{StaticResource FontSize.Small}"
                                       Foreground="{DynamicResource Brush.TextMuted}"
                                       Visibility="{Binding WasModified, Converter={StaticResource BoolToVisible}}"/>
                            <TextBlock Text="{Binding Summary}" TextTrimming="CharacterEllipsis"/>
                        </StackPanel>
                    </DataTemplate>
```

- [ ] **Step 3: Build and verify XAML parses**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: Build succeeded. (XAML compile errors here usually mean a wrong `enums`/`vmh` xmlns — fix by copying the exact xmlns strings used in `HabitScreen.xaml`.)

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml
git commit -m "feat(wpf): Note column category chip + day/time + Day/Period toggle"
```

---

### Task 9: Seed demo journal entries via IDiaryService

**Files:**
- Modify: `src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs`

**Interfaces:**
- Consumes: `IDiaryService.SaveEntryAsync` (Plan 1).
- Produces: demo + `--seed-habit` journals populated as real diary entries (Note(Diario)+Completion pairs).

- [ ] **Step 1: Inject IDiaryService**

In `src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs`, add `IDiaryService diary` to the primary constructor parameter list (next to `INoteService notes`):

```csharp
public sealed class DemoDataSeeder(
    IHabitNodeService nodes,
    IHabitConfigService configs,
    IMetricDefinitionService metrics,
    IGoalService goals,
    ICompletionService completions,
    INoteService notes,
    IDiaryService diary,
    ITagService tags,
    IAppSettingService settings,
    IHabitTreeQueryService tree,
    IClock clock)
```

`DemoDataSeeder` is resolved from DI (constructed by the container) — `IDiaryService` is already registered, so no manual registration edit is needed. (Confirm by checking `ServiceRegistrar.cs` for how `DemoDataSeeder` is registered; if it is `new`-ed up anywhere manually, pass the diary there too.)

- [ ] **Step 2: Replace the demo journal completion with a diary entry**

In `SeedAsync`, replace the journal block (around line 140):

```csharp
            if (Chance(0.35))
                await completions.RecordAsync(
                    Draft(diario, day, 22, 23, notesBody: JournalEntries[_rng.Next(JournalEntries.Length)]),
                    ct: ct).ConfigureAwait(false);
```

with:

```csharp
            if (Chance(0.35))
                await diary.SaveEntryAsync(diario, day, JournalEntries[_rng.Next(JournalEntries.Length)], ct)
                    .ConfigureAwait(false);
```

- [ ] **Step 3: Replace the --seed-habit journal arm with a diary entry**

In `SeedHabitAsync`, replace the `case HabitType.Journal` arm (around line 198):

```csharp
                case HabitType.Journal when Chance(0.40):
                    await completions.RecordAsync(
                        Draft(row.HabitNodeId, day, 7, 21, notesBody: JournalEntries[_rng.Next(JournalEntries.Length)]),
                        ct: ct).ConfigureAwait(false);
                    break;
```

with:

```csharp
                case HabitType.Journal when Chance(0.40):
                    await diary.SaveEntryAsync(row.HabitNodeId, day, JournalEntries[_rng.Next(JournalEntries.Length)], ct)
                        .ConfigureAwait(false);
                    break;
```

- [ ] **Step 4: Build**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: Build succeeded. (If `Draft` is now unused, leave it — it is still used by counter/resistance arms.)

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Bootstrap/DemoDataSeeder.cs
git commit -m "feat(wpf): seed journal demo data as diary entries via IDiaryService"
```

---

### Task 10: Full-suite green + manual verification

**Files:** none (verification only).

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build Recordum.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Run the full Data + WPF suites**

Run: `dotnet test tests/Recordum.Data.Tests` then `dotnet test tests/Recordum.WPF.Tests`
Expected: all green. Data ≥ 343 (Plan 1's 342 + the batch-lookup test). WPF ≥ 567 + the new NoteRow/DayNote/Journal/CompletionList/Notes tests.

- [ ] **Step 3: Manual on-screen verification (author)**

Run the app over a fresh seeded DB (the spec §9 repopulation): use the `run` skill with a clean reset, then `--seed-demo`. Verify:
1. A journal habit ("Diario"): the Abitudine column shows the markdown QuickAdd as the diary editor and **no** brief DayNote editor.
2. Saving a journal entry marks the calendar day done and adds a Cronologia row showing the entry text; the row has a delete (✕) but no inline-edit (✎).
3. Deleting that Cronologia row removes the day's completion (calendar updates) and the paired note.
4. The Note column shows each entry with a "Diary" chip + day + time, and the Day/Period toggle filters as in Cronologia.
5. A quick note on a non-journal habit (DayNote or Note column "New") shows a "Quick note" chip and does **not** create a completion.

- [ ] **Step 4: Update the feature memory**

Update the memory file `recordum-journal-diary-feature.md`: mark Plan 2 (WPF) complete, note the DayNote-hidden-for-journals decision and the `GetEntryBodiesByCompletionsAsync` addition, and record the final test counts.

---

## Self-Review

**Spec coverage:**
- §6 bullet 1 (JournalQuickAdd → DiaryService) → Task 3. ✓
- §6 bullet 2 (DayNote in journal context) → Task 4, resolved per author decision: **hidden** for journals (supersedes "writes a diary entry"); DayNote stays the Veloce quick-note editor for other types (Veloce is the Plan 1 default). ✓
- §6 bullet 3 (Cronologia journal shows paired note text via completion_id join; delete via DiaryService) → Task 5 (batch join) + Task 6. ✓
- §6 bullet 4 (+ from Home opens diary editor) → already implemented (`OpenScreenQuickActionHandler(Journal)`); verified in header + Task 10 Step 3. ✓
- §6 bullet 5 (quick note → Note(Veloce), no completion) → already the Plan 1 default for `NoteService.CreateAsync`/`NotesViewModel.New`/`DayNoteViewModel.SaveAsync`; surfaced via the "Quick note" chip (Tasks 1–2) and verified in Task 10 Step 3. No write-path change needed. ✓
- §7 (Note column: category chip + day/time + Day/Period toggle reusing EntriesPeriodRange) → Task 2 (VM data) + Task 7 (filter VM) + Task 8 (XAML). ✓
- §8 (free impacts: streak/calendar/hourly/heatmap unchanged) → structurally preserved (journal completions still recorded, now via DiaryService which publishes the same events); regression covered by keeping the existing HabitScreen/Detail/streak suites green (Task 10). ✓
- §9 (seeder via DiaryService) → Task 9. ✓
- §10 WPF testing (JournalQuickAdd uses DiaryService; Cronologia journal paired text; quick note no completion; Note column chip + period toggle; streak/calendar regression) → Tasks 3/6/7 tests + Task 10 regression. ✓

**Placeholder scan:** One deliberate "verify the exact xmlns" instruction in Task 8 Step 1 (the `EntriesPeriod` enum namespace must be copied from `HabitScreen.xaml` rather than guessed) — this is a verification instruction with a concrete source, not a code placeholder. All code steps carry complete code. No "TBD"/"handle errors"/"similar to". ✓

**Type consistency:**
- `IDiaryService.GetEntryBodiesByCompletionsAsync(IReadOnlyCollection<Guid>) → ValueTask<IReadOnlyDictionary<Guid,string>>` identical across interface (Task 5), DiaryService impl (Task 5), and `FakeDiaryService` (Task 3). ✓
- `JournalQuickAddViewModel(ICompletionService, IClock, IDiaryService)` consistent across impl (Task 3 Step 4), HabitScreenViewModel call site (Task 3 Step 5c), and tests (Task 3 Step 2). ✓
- `HabitScreenViewModel` ctor gains exactly one trailing `IDiaryService diary` param; the only production call site is the factory (Task 3 Step 6) and the app's DI resolution (automatic). ✓
- `CompletionListViewModel(..., IDiaryService diary)` + `.IsJournal` consistent across impl (Task 6 Step 2), the two HabitScreenViewModel constructions (Task 6 Step 4a), and tests (Task 6 Step 6). ✓
- `HabitRecordsViewModel.SetContext(Guid, bool)` — impl (Task 6 Step 3) and the single caller (Task 6 Step 4c) match. ✓
- `CompletionRowViewModel` new optional params (`overrideNotes`, `deleteOverride`, `canInlineEdit`) are appended after the existing optional params, so the existing call in `PopulateAsync` and any other call sites stay valid. ✓
- `NotesViewModel.Mode/SelectedPeriod/CustomFrom/CustomTo/SetCalendarDay` consistent across VM (Task 7), HabitScreenViewModel feeders (Task 7 Step 4), XAML (Task 8), and tests (Task 7 Step 1). ✓

**Risks to watch during execution:**
- **Task 3/5/6 ordering:** `FakeDiaryService` implements `GetEntryBodiesByCompletionsAsync`, which is added to the interface only in Task 5. Execute Task 3 → 5 → 6 in order; if you need a green build strictly after Task 3, omit that one fake method until Task 5 (noted in Task 3 Step 1).
- **`Populate` → `PopulateAsync`:** ensure no other caller of the old synchronous `Populate` remains (grep `Populate(` in `CompletionListViewModel.cs`). Only `LoadAsync`/`LoadRangeAsync` call it.
- **Task 8 xmlns:** `EntriesPeriod`'s CLR namespace/assembly must be copied verbatim from `HabitScreen.xaml`'s `enums:` declaration; `EntriesRecordsMode` lives in `Recordum.WPF.ViewModels.HabitScreen` (add `vmh:`).
- **Default Note column behavior:** `Mode=Periodo`+`SelectedPeriod=Tutto` must resolve to an unbounded range so the column shows all notes by default (preserving today's UX). Confirm `EntriesPeriodRange.Resolve(Tutto, ...)` returns `(null, null)` — if `Tutto` is named differently in the enum, use the actual "all" member.
- **Calendar day feed:** `Notes.SetCalendarDay` is called from both initial load and `OnCalendarDateSelected`; Day mode only filters once a day has been fed, otherwise it shows empty (acceptable — matches Records' Day mode before a selection).
