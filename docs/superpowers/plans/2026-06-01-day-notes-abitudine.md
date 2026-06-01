# Unified day-notes in Abitudine (Piano 2b) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere alla tab **Abitudine** un editor **nota-del-giorno** breve, legato al giorno selezionato nel calendario, che crea/aggiorna una `Note` ancorata (`TargetDate`); far comparire un **badge data** nella timeline della colonna Note; e far aggiornare i marcatori-nota del calendario quando una nota cambia.

**Architecture:** Le note sono già un sistema master-detail multi-nota (`NotesViewModel` + `NoteRowViewModel` + `NotesTabView`, editor markdown ricco) con eventi bus `NoteCreated/Updated/Deleted`. Riusiamo `INoteService` (con i metodi per-data del Piano 1: `ListByHabitAndDateAsync`). Un nuovo `DayNoteViewModel` (editor breve, TextBox) vive nella tab Abitudine, guidato da `Calendar.SelectedDateChanged`. Salvare pubblica gli eventi nota esistenti → la colonna Note si riaggancia da sé; aggiungiamo in `HabitScreenViewModel` una sottoscrizione agli eventi nota che ricarica i marcatori del calendario (`Calendar.JumpAsync`). Il markdown ricco resta nella colonna Note (decisione spec §7: editor breve in Abitudine, ricco nella colonna).

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Riferimento spec:** `docs/superpowers/specs/2026-06-01-abitudine-column-calendar-design.md` §6, §7. Dipende dal Piano 1 (`Note.TargetDate`, `INoteService.ListByHabitAndDateAsync`) e dal Piano 2a (`CalendarNavigatorViewModel`, tab Abitudine).

---

## File Structure

- **Create** `src/Recordum.WPF/ViewModels/HabitScreen/Notes/DayNoteViewModel.cs` — editor breve della nota del giorno selezionato (load per data, create/update con `TargetDate`).
- **Create** `src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml` (+ `.xaml.cs`) — header "Nota · <data>" + TextBox + Save.
- **Modify** `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` — costruisce `DayNote`, lo guida da `Calendar.SelectedDateChanged`, sottoscrive gli eventi nota → `Calendar.JumpAsync`, lo carica per il giorno pre-selezionato in `LoadAsync`, lo dispone.
- **Modify** `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` — inserisce `DayNoteView` nella tab Abitudine (sotto il quick-add/timer).
- **Modify** `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteRowViewModel.cs` — espone `DateBadge` (TargetDate formattata o vuoto).
- **Modify** `src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml` — mostra il badge data nella riga lista.
- **Modify** `src/Recordum.WPF/Resources/Strings.resx` + `Strings.it.resx` — `HabitScreen.DayNote.Title`, `HabitScreen.DayNote.Save`, `HabitScreen.DayNote.Placeholder`, `Notes.Badge.General`.
- **Tests**: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/DayNoteViewModelTests.cs` (new); `NoteRowViewModelTests` (new or extend) for the badge; a HabitScreen wiring test for calendar-day → DayNote.

---

## Task 1: `DayNoteViewModel` (load/save day note, TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/DayNoteViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/DayNoteViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/DayNoteViewModelTests.cs`:

```csharp
using FluentAssertions;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class DayNoteViewModelTests
{
    private static DayNoteViewModel NewSut(FakeNoteService notes)
        => new(notes, new FakeClock());

    [Fact]
    public async Task LoadDayAsync_with_no_existing_note_is_empty_and_cannot_save()
    {
        var sut = NewSut(new FakeNoteService());
        sut.SetContext(Guid.NewGuid());

        await sut.LoadDayAsync(new DateOnly(2026, 6, 10));

        sut.Text.Should().BeEmpty();
        sut.HasDate.Should().BeTrue();
        sut.CanSave.Should().BeFalse(); // empty new note → nothing to save
    }

    [Fact]
    public async Task Save_creates_a_note_anchored_to_the_day()
    {
        var notes = new FakeNoteService();
        var sut = NewSut(notes);
        var habitId = Guid.NewGuid();
        sut.SetContext(habitId);
        await sut.LoadDayAsync(new DateOnly(2026, 6, 10));

        sut.Text = "Good session";
        sut.CanSave.Should().BeTrue();
        await sut.SaveCommand.ExecuteAsync(null);

        var persisted = await notes.ListByHabitAndDateAsync(habitId, new DateOnly(2026, 6, 10));
        persisted.Should().ContainSingle();
        persisted[0].BodyMd.Should().Be("Good session");
        persisted[0].TargetDate.Should().Be(new DateOnly(2026, 6, 10));
        persisted[0].HabitNodeId.Should().Be(habitId);
    }

    [Fact]
    public async Task LoadDayAsync_with_existing_note_loads_its_text_and_update_persists()
    {
        var notes = new FakeNoteService();
        var habitId = Guid.NewGuid();
        await notes.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "orig", TargetDate = new DateOnly(2026, 6, 10) });
        var sut = NewSut(notes);
        sut.SetContext(habitId);

        await sut.LoadDayAsync(new DateOnly(2026, 6, 10));
        sut.Text.Should().Be("orig");

        sut.Text = "edited";
        await sut.SaveCommand.ExecuteAsync(null);

        var persisted = await notes.ListByHabitAndDateAsync(habitId, new DateOnly(2026, 6, 10));
        persisted.Should().ContainSingle();          // updated, not a second note
        persisted[0].BodyMd.Should().Be("edited");
    }
}
```

- [ ] **Step 2: Run to verify FAIL** (`DayNoteViewModel` missing)

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~DayNoteViewModelTests" --nologo`
Expected: FAIL (compile).

> Before Step 3, open `tests/Recordum.WPF.Tests/Fixtures/FakeNoteService.cs` and confirm `CreateAsync`, `UpdateAsync`, and `ListByHabitAndDateAsync` behave so that: CreateAsync stores the note with its `TargetDate`; UpdateAsync mutates in place; ListByHabitAndDateAsync filters by habit + date. (These exist from Piano 1.) Also confirm `FakeClock` has a parameterless ctor (used by `NewSut`).

- [ ] **Step 3: Implement `DayNoteViewModel`**

Create `src/Recordum.WPF/ViewModels/HabitScreen/Notes/DayNoteViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

/// <summary>
/// Brief editor for the note attached to a single calendar day (the unified Note with a TargetDate).
/// Lives in the Abitudine tab; the rich markdown editor for the same notes is the Notes column.
/// </summary>
public sealed partial class DayNoteViewModel : ObservableObject
{
    private readonly INoteService _notes;
    private readonly IClock _clock;
    private Guid _habitNodeId;
    private Note? _current;

    public DayNoteViewModel(INoteService notes, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(clock);
        _notes = notes;
        _clock = clock;
    }

    [ObservableProperty] private DateOnly? _date;
    [ObservableProperty] private bool _hasDate;
    [ObservableProperty] private string _text = string.Empty;

    public void SetContext(Guid habitNodeId)
    {
        _habitNodeId = habitNodeId;
        _current = null;
        Date = null;
        HasDate = false;
        Text = string.Empty;
    }

    /// <summary>Loads the (most recent) note anchored to <paramref name="date"/> into the editor, or an empty editor.</summary>
    public async Task LoadDayAsync(DateOnly date, CancellationToken ct = default)
    {
        Date = date;
        HasDate = true;
        var list = await _notes.ListByHabitAndDateAsync(_habitNodeId, date, ct).ConfigureAwait(true);
        _current = list.Count > 0 ? list[0] : null; // service returns most-recent first
        Text = _current?.BodyMd ?? string.Empty;
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Save is enabled only when a day is selected and the text differs from what's stored
    /// (and, for a brand-new note, is non-empty).</summary>
    public bool CanSave =>
        HasDate && (_current is null ? !string.IsNullOrWhiteSpace(Text) : Text != _current.BodyMd);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (Date is not { } d) return;
        if (_current is null)
        {
            var draft = new Note
            {
                HabitNodeId = _habitNodeId,
                BodyMd = Text,
                TargetDate = d,
                UpdatedAtMs = _clock.UtcNowMs,
            };
            await _notes.CreateAsync(draft, ct).ConfigureAwait(true);
            _current = draft;
        }
        else
        {
            var body = Text;
            await _notes.UpdateAsync(_current.Id, n => n.BodyMd = body, ct).ConfigureAwait(true);
            _current.BodyMd = body;
        }
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasDateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }
}
```

- [ ] **Step 4: Run to verify PASS** (3 tests)

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~DayNoteViewModelTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Notes/DayNoteViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/DayNoteViewModelTests.cs
git commit -m "feat(wpf): DayNoteViewModel — brief per-day note editor (TargetDate)"
```

---

## Task 2: Wire DayNote into the Abitudine tab + calendar refresh on note change

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Create: `src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml` (+ `.xaml.cs`)
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`
- Modify: `src/Recordum.WPF/Resources/Strings.resx` + `Strings.it.resx`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenDayNoteWiringTests.cs` (new)

- [ ] **Step 1: Write the failing wiring test**

Create `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenDayNoteWiringTests.cs`:

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitScreenDayNoteWiringTests
{
    [Fact]
    public async Task Selecting_a_calendar_day_loads_that_days_note_into_the_day_editor()
    {
        var (vm, nodes) = HabitScreenViewModelFactory.Create(out var notes);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };
        await notes.CreateAsync(new Note { HabitNodeId = id, BodyMd = "day note", TargetDate = new DateOnly(2020, 1, 10) });

        await vm.LoadAsync(id);
        await vm.Calendar.LoadAsync(2020, 1, selected: null);
        vm.Calendar.SelectDateCommand.Execute(vm.Calendar.Cells.Single(c => c.Date == new DateOnly(2020, 1, 10)));

        vm.DayNote.Date.Should().Be(new DateOnly(2020, 1, 10));
        vm.DayNote.Text.Should().Be("day note");
    }
}
```

> This uses an overload `HabitScreenViewModelFactory.Create(out var notes)` returning the `FakeNoteService` too. If the Plan-2a factory only returns `(vm, nodes)`, ADD an overload `Create(out FakeNoteService notes)` (or change the existing one to also expose notes) in `tests/Recordum.WPF.Tests/Fixtures/HabitScreenViewModelFactory.cs`, keeping the existing signature working. The factory must pass the SAME `FakeNoteService` instance to the VM that it returns.

- [ ] **Step 2: Run to verify FAIL** (`DayNote` missing on VM)

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~HabitScreenDayNoteWiringTests" --nologo`
Expected: FAIL.

- [ ] **Step 3: Add `DayNote` to `HabitScreenViewModel`**

In `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`:

Add the property after `public CalendarNavigatorViewModel Calendar { get; }`:
```csharp
    public DayNoteViewModel DayNote { get; }
```

In the ctor, after the `Calendar = new ...; Calendar.SelectedDateChanged += OnCalendarDateSelected;` lines, add:
```csharp
        DayNote = new DayNoteViewModel(noteService, clock);
        _subscriptions.Add(events.Subscribe(WellKnownEvents.NoteCreated, OnNoteEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.NoteUpdated, OnNoteEvent));
        _subscriptions.Add(events.Subscribe(WellKnownEvents.NoteDeleted, OnNoteEvent));
```
> `Recordum.WPF.ViewModels.HabitScreen.Notes` is already imported (NotesViewModel is there). If not, add `using Recordum.WPF.ViewModels.HabitScreen.Notes;`.

Extend `OnCalendarDateSelected` to also load the day's note:
```csharp
    private async void OnCalendarDateSelected(object? sender, DateOnly date)
    {
        if (QuickAdd is not null)
            QuickAdd.CompletionDate = date.ToDateTime(TimeOnly.MinValue);
        await DayNote.LoadDayAsync(date).ConfigureAwait(true);
    }
```
> Changing this handler to `async void` is acceptable for an event handler (matches `OnCompletionEvent`'s `async void` pattern in this class). If it was a plain `void` before, convert it.

Add the note-event handler (mirrors `OnCompletionEvent` — refresh the calendar's note markers for the current month):
```csharp
    private async void OnNoteEvent(EventEnvelope envelope)
    {
        if (Node is null) return;
        var habitId = envelope.Payload switch
        {
            NoteCreatedPayload c => c.HabitNodeId,
            NoteUpdatedPayload u => u.HabitNodeId,
            NoteDeletedPayload d => d.HabitNodeId,
            _ => Guid.Empty,
        };
        if (habitId != Node.Id) return;
        await Calendar.JumpAsync().ConfigureAwait(true);
    }
```
> Confirm the payload type names by checking how `NotesViewModel.OnNoteEvent` switches them (`NoteCreatedPayload`/`NoteUpdatedPayload`/`NoteDeletedPayload`) and the `WellKnownEvents.Note*` keys — reuse the exact same types/keys.

In `LoadAsync`, after the calendar initial load + the QuickAdd date mirror, set the DayNote context and load the pre-selected day:
```csharp
        DayNote.SetContext(Node.Id);
        if (Calendar.SelectedDate is { } sel)
            await DayNote.LoadDayAsync(sel, ct).ConfigureAwait(true);
```

- [ ] **Step 4: Run the wiring test to verify PASS**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~HabitScreenDayNoteWiringTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Add the resx strings**

In `src/Recordum.WPF/Resources/Strings.resx` (follow the dot-named `name=` convention used by the file, e.g. `HabitScreen.Configure`):
```xml
  <data name="HabitScreen.DayNote.Title" xml:space="preserve">
    <value>Note for this day</value>
  </data>
  <data name="HabitScreen.DayNote.Save" xml:space="preserve">
    <value>Save note</value>
  </data>
  <data name="HabitScreen.DayNote.Placeholder" xml:space="preserve">
    <value>Pick a day on the calendar to add a note.</value>
  </data>
  <data name="Notes.Badge.General" xml:space="preserve">
    <value>General</value>
  </data>
```
In `src/Recordum.WPF/Resources/Strings.it.resx`:
```xml
  <data name="HabitScreen.DayNote.Title" xml:space="preserve">
    <value>Nota del giorno</value>
  </data>
  <data name="HabitScreen.DayNote.Save" xml:space="preserve">
    <value>Salva nota</value>
  </data>
  <data name="HabitScreen.DayNote.Placeholder" xml:space="preserve">
    <value>Scegli un giorno sul calendario per aggiungere una nota.</value>
  </data>
  <data name="Notes.Badge.General" xml:space="preserve">
    <value>Generale</value>
  </data>
```
If `Strings.Designer.cs` does not regenerate on build, add the four matching static properties (`HabitScreen_DayNote_Title` etc.) mirroring how existing dot-keys are declared.

- [ ] **Step 6: Create `DayNoteView`**

Create `src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml`:
```xml
<UserControl x:Class="Recordum.WPF.Views.HabitScreen.DayNoteView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Recordum.WPF.ViewModels.HabitScreen.Notes"
             xmlns:res="clr-namespace:Recordum.WPF.Resources"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance vm:DayNoteViewModel}">
    <Border Padding="12" BorderThickness="0,0,0,1" BorderBrush="{DynamicResource Brush.Border}">
        <StackPanel>
            <StackPanel Orientation="Horizontal" Margin="0,0,0,6">
                <TextBlock Text="{x:Static res:Strings.HabitScreen_DayNote_Title}"
                           Foreground="{DynamicResource Brush.TextMuted}"
                           FontSize="{StaticResource FontSize.Small}"/>
                <TextBlock Text="{Binding Date}" Margin="6,0,0,0"
                           Foreground="{DynamicResource Brush.TextMuted}"
                           FontSize="{StaticResource FontSize.Small}"
                           Visibility="{Binding HasDate, Converter={StaticResource BoolToVisible}}"/>
            </StackPanel>
            <TextBlock Text="{x:Static res:Strings.HabitScreen_DayNote_Placeholder}"
                       Foreground="{DynamicResource Brush.TextMuted}"
                       TextWrapping="Wrap"
                       Visibility="{Binding HasDate, Converter={StaticResource InvBoolToVisible}}"/>
            <TextBox Text="{Binding Text, UpdateSourceTrigger=PropertyChanged}"
                     AcceptsReturn="True" MinHeight="56" TextWrapping="Wrap"
                     Visibility="{Binding HasDate, Converter={StaticResource BoolToVisible}}"/>
            <Button Content="{x:Static res:Strings.HabitScreen_DayNote_Save}"
                    Command="{Binding SaveCommand}"
                    HorizontalAlignment="Left" Padding="10,4" Margin="0,6,0,0"
                    Visibility="{Binding HasDate, Converter={StaticResource BoolToVisible}}"/>
        </StackPanel>
    </Border>
</UserControl>
```
Create `src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml.cs`:
```csharp
using System.Windows.Controls;

namespace Recordum.WPF.Views.HabitScreen;

public partial class DayNoteView : UserControl
{
    public DayNoteView() => InitializeComponent();
}
```

- [ ] **Step 7: Place `DayNoteView` in the Abitudine tab**

In `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`, the first TabItem's inner `Grid` currently has rows: 0=calendar, 1=QuickAdd, 2=TimerControl, 3=CompletionList(`*`). Add a row for the day note between TimerControl and CompletionList.

Change the `Grid.RowDefinitions` of that inner Grid from:
```xml
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
```
to (add one Auto row before the `*`):
```xml
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
```
Insert the day-note view at Grid.Row="3" (before the CompletionList, which moves to Row 4). Add a namespace `xmlns:v="clr-namespace:Recordum.WPF.Views.HabitScreen"` to the UserControl tag if not already present (the file already uses `v:` for `NotesTabView` — confirm, reuse it). Then:
```xml
                    <v:DayNoteView Grid.Row="3" DataContext="{Binding DayNote}"/>
```
and change the CompletionList line from `Grid.Row="3"` to `Grid.Row="4"`:
```xml
                    <ContentControl Grid.Row="4" Content="{Binding CompletionList}"/>
```

- [ ] **Step 8: Build + full WPF suite**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj --nologo` (expect OK)
Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --nologo` (expect ALL pass)

- [ ] **Step 9: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml src/Recordum.WPF/Views/HabitScreen/DayNoteView.xaml.cs src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml src/Recordum.WPF/Resources/ tests/Recordum.WPF.Tests/
git commit -m "feat(wpf): per-day note editor in Abitudine + calendar refresh on note change"
```

---

## Task 3: Date badge in the Notes timeline

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteRowViewModel.cs`
- Modify: `src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteRowViewModelTests.cs` (new)

- [ ] **Step 1: Write the failing tests**

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
    [Fact]
    public void DateBadge_shows_general_label_when_no_target_date()
    {
        var row = new NoteRowViewModel(new Note { BodyMd = "x", TargetDate = null });
        row.DateBadge.Should().Be(Recordum.WPF.Resources.Strings.Notes_Badge_General);
    }

    [Fact]
    public void DateBadge_shows_the_day_when_target_date_set()
    {
        var date = new DateOnly(2026, 6, 1);
        var row = new NoteRowViewModel(new Note { BodyMd = "x", TargetDate = date });
        row.DateBadge.Should().Be(date.ToString("d MMM", CultureInfo.CurrentCulture));
    }
}
```

- [ ] **Step 2: Run to verify FAIL** (`DateBadge` missing)

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~NoteRowViewModelTests" --nologo`
Expected: FAIL.

- [ ] **Step 3: Add `DateBadge` to `NoteRowViewModel`**

In `src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteRowViewModel.cs`, add a property (and include it in `Refresh()`):
```csharp
    public string DateBadge =>
        Note.TargetDate is { } d
            ? d.ToString("d MMM", CultureInfo.CurrentCulture)
            : Recordum.WPF.Resources.Strings.Notes_Badge_General;
```
And in `Refresh()` add `OnPropertyChanged(nameof(DateBadge));`.
> `using System.Globalization;` is already present in the file.

- [ ] **Step 4: Run to verify PASS**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~NoteRowViewModelTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Show the badge in the list**

In `src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml`, in the `NoteRowViewModel` `DataTemplate`, add the badge above/next to the date. Replace the existing template's inner `StackPanel`:
```xml
                        <StackPanel Margin="0,4">
                            <TextBlock Text="{Binding UpdatedAtDisplay}"
                                       FontSize="{StaticResource FontSize.Small}"
                                       Foreground="{DynamicResource Brush.TextMuted}"/>
                            <TextBlock Text="{Binding Summary}" TextTrimming="CharacterEllipsis"/>
                        </StackPanel>
```
with (add the badge chip on top):
```xml
                        <StackPanel Margin="0,4">
                            <Border HorizontalAlignment="Left" Margin="0,0,0,2"
                                    Padding="5,1" CornerRadius="3"
                                    Background="{DynamicResource Brush.SurfaceElevated}">
                                <TextBlock Text="{Binding DateBadge}"
                                           FontSize="{StaticResource FontSize.Small}"
                                           Foreground="{DynamicResource Brush.TextMuted}"/>
                            </Border>
                            <TextBlock Text="{Binding UpdatedAtDisplay}"
                                       FontSize="{StaticResource FontSize.Small}"
                                       Foreground="{DynamicResource Brush.TextMuted}"/>
                            <TextBlock Text="{Binding Summary}" TextTrimming="CharacterEllipsis"/>
                        </StackPanel>
```
> Confirm `Brush.SurfaceElevated` is a real resource key (grep `Resources/*.xaml`); if not, use `Brush.Surface` or `Brush.Border` — whichever exists and reads as a subtle chip background.

- [ ] **Step 6: Build + commit**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj --nologo` (expect OK)
```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/Notes/NoteRowViewModel.cs src/Recordum.WPF/Views/HabitScreen/NotesTabView.xaml tests/Recordum.WPF.Tests/ViewModels/HabitScreen/Notes/NoteRowViewModelTests.cs
git commit -m "feat(wpf): date badge in Notes timeline (anchored vs general)"
```

---

## Task 4: Final verification

- [ ] **Step 1: Full suites green**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --nologo` (expect ALL pass)
Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --nologo` (expect unchanged green)

- [ ] **Step 2: Update tracker**

In `docs/phase-4-issues.md`, under the 2026-06-01 status, note Piano 2b done (per-day note editor + calendar note-refresh + Notes date badge). Commit:
```bash
git add docs/phase-4-issues.md
git commit -m "docs(phase-4): Piano 2b done — unified day-notes in Abitudine"
```

- [ ] **Step 3: Visual verification (skill `run`)**

Launch the app, open a habit, go to the Abitudine tab. Confirm: selecting a calendar day loads/empties the day-note editor; typing + Save persists; the saved day gets a note dot on the calendar; the same note appears in the Notes tab with a date badge; notes without a date show "General".

---

## Self-Review

- **Spec coverage:** §7 unified notes — same `Note` shown two ways: per-day brief editor in Abitudine (Task 1-2) + full timeline in Notes column with date badge (Task 3) ✓. §6 day panel includes the day note ✓. Calendar note-marker refresh on save (Task 2 `OnNoteEvent → Calendar.JumpAsync`) ✓. Decision "brief editor in Abitudine, rich in Notes column" honored (TextBox vs Toast UI) ✓.
- **Deferred (NOT gaps):** the note-color **picker** in Configura → Piano 3 (here `NoteMarkerColor` is consumed only). Multiple notes per same day: the day editor edits the most-recent and creates when none — extra same-day notes remain visible/editable in the Notes column (acceptable; not a multi-note-per-day UI in Abitudine).
- **Placeholder scan:** every code step has complete code. Codebase-adaptation notes (FakeNoteService/FakeClock API in Task 1; factory `out notes` overload + payload type names + `Brush.SurfaceElevated`/`v:` namespace in Tasks 2-3) state exactly what to verify and the fallback — not placeholders.
- **Type consistency:** `DayNoteViewModel` (`SetContext`, `LoadDayAsync(DateOnly,ct)`, `Date`/`HasDate`/`Text`/`CanSave`/`SaveCommand`); `HabitScreenViewModel.DayNote`; `OnCalendarDateSelected` async; `OnNoteEvent` with `NoteCreatedPayload`/`NoteUpdatedPayload`/`NoteDeletedPayload`; `NoteRowViewModel.DateBadge`. `INoteService.ListByHabitAndDateAsync`/`CreateAsync`/`UpdateAsync` match Piano 1 + existing code. Resx dot-keys `HabitScreen.DayNote.*`/`Notes.Badge.General` → properties `HabitScreen_DayNote_*`/`Notes_Badge_General`.
