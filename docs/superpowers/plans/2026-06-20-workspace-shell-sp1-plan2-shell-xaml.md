# Workspace Shell SP1 — Plan 2 of 3: Shell XAML + tree→tabs wiring

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the tabbed workspace real on screen — the rich habit tree becomes a collapsible left sidebar, the right side becomes a tab host that renders each open subject as its own `HabitScreen`, and selecting/activating in the tree drives the `WorkspaceViewModel` (Plan 1) preview/pin lifecycle.

**Architecture:** `MainWindowViewModel` stops owning a single rebinding `HabitScreenViewModel` and instead owns the Plan 1 `WorkspaceViewModel`; tree selection routes to `Workspace.OpenSubjectAsync` (preview) instead of `HabitScreen.LoadAsync`. Each tab already carries its own `HabitScreenViewModel` (Plan 1), so the existing `HabitScreen` view is reused verbatim per tab via a `ContentControl` + implicit `DataTemplate`. The per-screen event wiring that lived in `MainWindowViewModel` for one screen (Configure/close/export) moves onto each tab through `SubjectTabViewModel`. A new `WorkspaceView` renders the tab bar + active content; `MainWindow` swaps its Home-workspace layout to `[sidebar tree][splitter][WorkspaceView]`.

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm, xUnit + FluentAssertions.

**Source spec:** `docs/superpowers/specs/2026-06-20-workspace-shell-sp1-design.md` — Plan 2 covers §3.1 (sidebar), §3.2 (tab bar UI), §3.7 (close affordances/keyboard), and the `MainWindow`/wiring parts of §4. Per-tab pane *content* already exists (reused `HabitScreen`); the dedicated Diario pane + focus mode are Plan 3.

## SP1 staging (context)
- **Plan 1 (done):** `WorkspaceViewModel`/`SubjectTabViewModel` tab model + per-tab DI scope + `ui.workspace` persistence. No UI.
- **Plan 2 (this):** shell XAML + tree→tabs wiring + per-tab event relocation + session restore/save.
- **Plan 3:** dedicated `HabitColumn.Diario` pane extraction for journals + focus/expand mode.

## Key reuse decision (read first)
Each `SubjectTabViewModel.Screen` is a full `HabitScreenViewModel`. The existing `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (`vhs:HabitScreen`) binds to a `HabitScreenViewModel` DataContext and already renders every column (Cronologia/Dettaglio/Note/Configura, plus per-group Panoramica) with its chip toggles. **Plan 2 reuses that view unchanged**, hosted by a `ContentControl` whose `Content` is `ActiveSubjectTab.Screen`. No new pane rendering is written here.

## Default ("Panoramica") tab decision (deliberate, stated)
There is no standalone top-level Panoramica view today (the per-group Panoramica lives inside `HabitScreen`). For Plan 2, the **default state — `Workspace.IsPanoramicaActive` (no subject tab) — shows a simple empty/welcome panel** (a centered hint to pick a habit from the sidebar). A richer "Today/Overview" default tab is out of scope for SP1 and tracked for later. This keeps Plan 2 focused on the shell.

## Global Constraints
- **MVVM** with CommunityToolkit.Mvvm; UI-thread continuations `.ConfigureAwait(true)`.
- **User-facing strings** in `.resx` (English-first, Italian secondary). `Strings.Designer.cs` is hand-maintained — add accessors via shell (`awk`), never via Edit (guardrail blocks `*.Designer.cs`).
- **Never** `DateTime.UtcNow/Now`; time via `IClock`.
- **Persistence** only via `IAppSettingService`; the workspace state key is `ui.workspace` (Plan 1).
- **The note↔completion and tab-scope invariants stay in their owners** (`IDiaryService`, `WorkspaceViewModel`). No XAML writes tab state directly except through the VM's commands.
- Baseline to keep green: **WPF 604, Data 343**. This plan raises WPF.
- No `Recordum.sln`: build/test per project (`dotnet build src/Recordum.WPF/Recordum.WPF.csproj`, `dotnet test tests/Recordum.WPF.Tests`).

## Interfaces from Plan 1 (consumed; do not redefine)
```csharp
// Recordum.WPF.ViewModels.Workspace
WorkspaceViewModel(ISubjectScreenFactory screenFactory, IAppSettingService appSettings)
  ObservableCollection<SubjectTabViewModel> Tabs
  SubjectTabViewModel? ActiveSubjectTab        // observable; null => Panoramica
  bool IsPanoramicaActive                       // ActiveSubjectTab is null
  bool SidebarCollapsed                         // observable
  double SidebarWidth                           // observable
  Task OpenSubjectAsync(Guid id, WorkspaceTabKind kind, string title, bool pin = false, CancellationToken ct = default)
  void ActivatePanoramica(); void ActivateTab(SubjectTabViewModel); void CloseTab(SubjectTabViewModel)
  void MoveTab(SubjectTabViewModel, int newIndex); void PinActiveTab(); void NotifySubjectEdited(Guid)
  Task SaveStateAsync(CancellationToken = default)
  Task RestoreStateAsync(Func<Guid, WorkspaceTabKind, string> titleResolver, CancellationToken = default)
// SubjectTabViewModel : ObservableObject, IDisposable
//   Guid SubjectId; WorkspaceTabKind Kind; HabitScreenViewModel Screen; string Title; bool IsPreview; void Pin(); void Dispose()
// enum WorkspaceTabKind { Habit, Group }
```
This plan **adds** to Plan 1: per-tab event relocation (Task 2) and `WorkspaceViewModel : IDisposable` (Task 5, the deferred shutdown-disposal note).

---

### Task 1: MainWindowViewModel owns WorkspaceViewModel; tree selection drives it

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/MainWindowViewModelTests.cs`
- Modify: `tests/Recordum.WPF.Tests/Fixtures/MainWindowViewModelFactory.cs`

**Interfaces:**
- Consumes: `WorkspaceViewModel.OpenSubjectAsync`, `HomeRowViewModel.{HabitNodeId,IsGroup,HabitType,Name}` (existing; confirm the display-name property is `Name` — search `HomeRowViewModel.cs`; if it is named differently, use that exact name).
- Produces: `MainWindowViewModel.Workspace` property; selection→OpenSubject behavior.

> Context: today `MainWindowViewModel` injects a single `HabitScreenViewModel habitScreen` and, on `Home.SelectedRow` change, calls `HabitScreen.LoadAsync(...)` and rebinds the editor. Plan 2 replaces that with `WorkspaceViewModel`. Keep `Charts`/`BackupHistory`/`Home`/`TimerStatusBar` workspaces unchanged.

- [ ] **Step 1: Update the test factory to inject WorkspaceViewModel**

In `tests/Recordum.WPF.Tests/Fixtures/MainWindowViewModelFactory.cs`, the `new HabitScreenViewModel(...)` construction (used to pass `habitScreen`) is replaced by a `WorkspaceViewModel` built over a `FakeSubjectScreenFactory` + the same `FakeAppSettingService`. Find where `MainWindowViewModel` is constructed and change the `habitScreen` argument to a `workspace` argument. Concretely, replace the `habitScreen` local + its constructor argument with:

```csharp
        var workspace = new Recordum.WPF.ViewModels.Workspace.WorkspaceViewModel(
            new FakeSubjectScreenFactory(), settings);
```

and pass `workspace` where `habitScreen` was passed to `new MainWindowViewModel(...)`. (The exact MainWindowViewModel ctor argument list changes in Step 3 — keep this factory edit in sync with that signature.)

- [ ] **Step 2: Write the failing test**

In `tests/Recordum.WPF.Tests/ViewModels/MainWindowViewModelTests.cs`, add:

```csharp
    [Fact]
    public async Task Selecting_a_habit_row_opens_it_as_a_preview_tab()
    {
        var (vm, home) = MainWindowViewModelFactory.Create();   // adjust to the factory's actual accessor
        var habitId = Guid.NewGuid();
        home.SetRowsForTest(habitId, isGroup: false, name: "Corsa"); // see Step 2a

        home.SelectedRow = home.Rows.First(r => r.HabitNodeId == habitId);
        await Task.Yield();

        vm.Workspace.Tabs.Should().ContainSingle();
        vm.Workspace.Tabs[0].SubjectId.Should().Be(habitId);
        vm.Workspace.Tabs[0].IsPreview.Should().BeTrue();
        vm.Workspace.Tabs[0].Title.Should().Be("Corsa");
    }
```

**Step 2a — how to seed a Home row in tests:** Inspect `tests/Recordum.WPF.Tests` for the existing helper that populates `HomeViewModel.Rows` (e.g. via `FakeHabitNodeService` + `Home.LoadCommand`, or a direct test seam). Reuse that exact mechanism instead of `SetRowsForTest` if no such method exists — the pseudo-call above is a placeholder for "make a selectable row with this id/kind/name". If the existing `MainWindowViewModelFactory` already returns `(vm, home)` and exposes the node service, seed through it and run `Home.LoadCommand` so a real `HomeRowViewModel` exists. Do not invent a new production seam.

- [ ] **Step 3: Swap the dependency in MainWindowViewModel**

In `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs`:
(a) Replace the field/property `public HabitScreenViewModel HabitScreen { get; }` and the ctor param `HabitScreenViewModel habitScreen` with:

```csharp
    public Workspace.WorkspaceViewModel Workspace { get; }
```
and ctor param `Workspace.WorkspaceViewModel workspace`, with `ArgumentNullException.ThrowIfNull(workspace); Workspace = workspace;`.

(b) Remove the now-dead members that referenced the single screen: the `Editor` shortcut property (`HabitScreen.Editor`), and the event subscriptions in the ctor (`HabitScreen.ConfigureRequested`, `HabitScreen.AllColumnsClosed`, `HabitScreen.Panoramica.ChildSelected`, `HabitScreen.ExportRequested`) plus their handlers (`OnHabitScreenConfigureRequested`, `OnAllColumnsClosed`, `OnPanoramicaChildSelected`, `OnHabitScreenExportRequested`) and the `RestoreOpenColumnsAsync` call. These behaviors move per-tab in Task 2. Keep `Home.PropertyChanged`, `Home.HabitActivationRequested`, `Home.GroupCreated` subscriptions.

(c) Replace `OnHomePropertyChanged` body with selection→OpenSubject:

```csharp
    private async void OnHomePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.SelectedRow)) return;
        if (Home.SelectedRow is not { } row) { Workspace.ActivatePanoramica(); return; }
        await Workspace.OpenSubjectAsync(row.HabitNodeId, KindOf(row), row.Name).ConfigureAwait(true);
    }

    private static Workspace.WorkspaceTabKind KindOf(HomeRowViewModel row)
        => row.IsGroup ? Workspace.WorkspaceTabKind.Group : Workspace.WorkspaceTabKind.Habit;
```
(Use the actual display-name property on `HomeRowViewModel`; if it is not `Name`, use the correct one.)

(d) Replace `OnHabitActivationRequested` (the "+ on already-selected row" path) to pin/activate the subject:

```csharp
    private async void OnHabitActivationRequested(object? sender, Guid habitNodeId)
    {
        var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == habitNodeId);
        if (row is null) return;
        await Workspace.OpenSubjectAsync(habitNodeId, KindOf(row), row.Name, pin: true).ConfigureAwait(true);
    }
```

(e) `OnHomeGroupCreated` currently force-opens the Configura column on the single screen. Change it to open the new group as a pinned tab (its Configura column auto-opens via the per-tab ConfigureRequested path once Task 2 lands; for now just open/pin the tab):

```csharp
    private async void OnHomeGroupCreated(object? sender, Guid id)
    {
        var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == id);
        if (row is null) return;
        await Workspace.OpenSubjectAsync(id, KindOf(row), row.Name, pin: true).ConfigureAwait(true);
    }
```

(f) Delete the `HabitScreenWidth*` members and `CommitHabitScreenWidthAsync`/`LoadHabitScreenWidthAsync` ONLY IF they are no longer referenced after the XAML changes (Task 6 removes the single HabitScreen column). To keep this task self-contained and the build green, **leave them for now** and remove them in Task 6 when their last XAML reference is deleted.

- [ ] **Step 4: Update MainWindow.xaml.cs constructor references (compile-only)**

`MainWindow.xaml` still references `HabitScreen` in Task 1's state — DO NOT touch XAML yet (Task 6). But `MainWindowViewModel` no longer has a `HabitScreen` property, so the existing XAML binding `{Binding HabitScreen}` will break at runtime (not compile). To keep the app launchable between tasks, Task 1 is VM-only and the app's Home workspace is temporarily degraded until Task 6 wires `Workspace`. This is acceptable mid-plan (tests gate each task; the full manual run is Task 8). Note it in the commit message.

- [ ] **Step 5: Run the tests**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj` (expect: the project builds; if `MainWindow.xaml` binding to `HabitScreen` is a compile error rather than a runtime one because of `d:DataContext`, temporarily change that one binding to `{Binding Workspace}` placeholder — Task 6 finalizes it). Then `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~MainWindowViewModelTests"`.
Expected: the new selection→preview test passes; existing MainWindowViewModel tests pass (update any that asserted on `HabitScreen` directly to use `Workspace`).

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/ViewModels/MainWindowViewModel.cs tests/Recordum.WPF.Tests/ViewModels/MainWindowViewModelTests.cs tests/Recordum.WPF.Tests/Fixtures/MainWindowViewModelFactory.cs
git commit -m "feat(wpf): MainWindowViewModel owns WorkspaceViewModel; tree selection opens preview tabs"
```

---

### Task 2: Relocate per-screen events onto each tab (Configure / close / export / child-select)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Workspace/SubjectTabViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs`

**Interfaces:**
- Consumes: `HabitScreenViewModel` events `AllColumnsClosed`, `ConfigureRequested`, `ExportRequested`, `Panoramica.ChildSelected`, and `IsConfiguraOpen` (existing on the screen).
- Produces: per-tab behavior — `AllColumnsClosed` on a tab's screen closes that tab; `ConfigureRequested` opens that tab's Configura column (the screen handles its own column already; verify no extra wiring needed); `ExportRequested` raises a workspace-level `ExportRequested(Guid subjectId)` event; `Panoramica.ChildSelected` raises a workspace-level `SubjectActivationRequested(Guid)` to open that child as a tab.

> Why: these used to be wired once in `MainWindowViewModel` for the single screen. With N tabs each owning a screen, the wiring must attach when a tab is created and detach on dispose.

- [ ] **Step 1: Write the failing tests**

In `WorkspaceViewModelTests.cs` add (the fake screen from `HabitScreenViewModelFactory` exposes these events; if an event is awkward to raise from the fake, assert via the smallest observable effect):

```csharp
    [Fact]
    public async Task A_tabs_AllColumnsClosed_closes_that_tab()
    {
        var (sut, _, _) = WorkspaceViewModelFactory.CreateWithScreens();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        var tab = sut.Tabs[0];

        tab.Screen.RaiseAllColumnsClosedForTest(); // see Step 2

        sut.Tabs.Should().BeEmpty();
        sut.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_tabs_ExportRequested_bubbles_up_with_the_subject_id()
    {
        var (sut, _, _) = WorkspaceViewModelFactory.CreateWithScreens();
        var id = Guid.NewGuid();
        await sut.OpenSubjectAsync(id, WorkspaceTabKind.Habit, "Corsa", pin: true);
        Guid? exported = null;
        sut.ExportRequested += (_, sid) => exported = sid;

        sut.Tabs[0].Screen.RaiseExportRequestedForTest();

        exported.Should().Be(id);
    }
```

**Step 2 — test seam on the fake screen:** `HabitScreenViewModel` is concrete; to raise its events from a test, add internal test-only raisers on `HabitScreenViewModel` IF they don't exist, e.g. `internal void RaiseAllColumnsClosedForTest() => AllColumnsClosed?.Invoke(this, EventArgs.Empty);` and `internal void RaiseExportRequestedForTest() => ExportRequested?.Invoke(this, EventArgs.Empty);` guarded by `[InternalsVisibleTo("Recordum.WPF.Tests")]` (check whether that attribute already exists for the WPF assembly; the test project already accesses internals like `StateSettingKey`, so it does). Place these raisers next to the event declarations.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: FAIL — `ExportRequested` event + close-on-AllColumnsClosed not wired; raisers missing.

- [ ] **Step 3: Wire events per tab in WorkspaceViewModel**

(a) Add a workspace-level event surface near the top of `WorkspaceViewModel`:

```csharp
    /// <summary>Raised when a tab asks to export its subject (bubbled from the tab's screen).</summary>
    public event EventHandler<Guid>? ExportRequested;
    /// <summary>Raised when something inside a tab (e.g. a Panoramica child row) asks to open another subject.</summary>
    public event EventHandler<Guid>? SubjectActivationRequested;
```

(b) Move the per-tab subscription into `SubjectTabViewModel` so it attaches on construct and detaches on dispose. In `SubjectTabViewModel`, add events it re-raises and wire to its `Screen` in the ctor; detach in `Dispose`:

```csharp
    public event EventHandler? AllColumnsClosed;
    public event EventHandler? ExportRequested;
    public event EventHandler<Guid>? ChildSelected;

    // in ctor, after assigning Screen:
    Screen.AllColumnsClosed += OnScreenAllColumnsClosed;
    Screen.ExportRequested += OnScreenExportRequested;
    Screen.Panoramica.ChildSelected += OnScreenChildSelected;

    private void OnScreenAllColumnsClosed(object? s, EventArgs e) => AllColumnsClosed?.Invoke(this, EventArgs.Empty);
    private void OnScreenExportRequested(object? s, EventArgs e) => ExportRequested?.Invoke(this, EventArgs.Empty);
    private void OnScreenChildSelected(object? s, Guid id) => ChildSelected?.Invoke(this, id);
```
And in `Dispose` (before `_scope.Dispose()`):
```csharp
        Screen.AllColumnsClosed -= OnScreenAllColumnsClosed;
        Screen.ExportRequested -= OnScreenExportRequested;
        Screen.Panoramica.ChildSelected -= OnScreenChildSelected;
```

(c) In `WorkspaceViewModel.OpenSubjectAsync`, after creating `tab`, subscribe to the tab's re-raised events:

```csharp
        tab.AllColumnsClosed += (_, _) => CloseTab(tab);
        tab.ExportRequested += (_, _) => ExportRequested?.Invoke(this, tab.SubjectId);
        tab.ChildSelected += (_, id) => SubjectActivationRequested?.Invoke(this, id);
```
(These lambdas are GC-reachable as long as `tab` is; `CloseTab(tab)` disposes the tab which detaches the screen handlers, and the tab is then unreferenced. Acceptable for this scope.)

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: all pass.

- [ ] **Step 5: Re-wire the workspace-level export/child events in MainWindowViewModel**

In `MainWindowViewModel` ctor, subscribe:

```csharp
        Workspace.ExportRequested += OnWorkspaceExportRequested;
        Workspace.SubjectActivationRequested += async (_, id) =>
        {
            var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == id);
            if (row is not null) await Workspace.OpenSubjectAsync(id, KindOf(row), row.Name).ConfigureAwait(true);
        };
```
and add the export handler (mirrors the old `OnHabitScreenExportRequested`, but resolves name from the row):
```csharp
    private async void OnWorkspaceExportRequested(object? sender, Guid subjectId)
    {
        var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == subjectId);
        await _exportLauncher.ShowForHabitAsync(subjectId, row?.Name ?? string.Empty).ConfigureAwait(true);
    }
```

- [ ] **Step 6: Build + targeted tests + commit**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj` then `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~WorkspaceViewModelTests|FullyQualifiedName~MainWindowViewModelTests"`
Expected: green.

```bash
git add src/Recordum.WPF/ViewModels/Workspace/SubjectTabViewModel.cs src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs src/Recordum.WPF/ViewModels/MainWindowViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs
git commit -m "feat(wpf): relocate per-screen events (close/export/child) onto each workspace tab"
```

---

### Task 3: Session restore + save-on-change

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/MainWindowViewModelTests.cs`

**Interfaces:**
- Consumes: `Workspace.RestoreStateAsync(titleResolver)`, `Workspace.SaveStateAsync()`, `IHabitNodeService.GetAsync` (existing) for the title resolver.
- Produces: restore on `InitializeAsync`; save when tabs/active/sidebar change.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public async Task Closing_all_tabs_persists_workspace_state()
    {
        var (vm, home) = MainWindowViewModelFactory.Create(out var settings); // adjust accessor
        var id = Guid.NewGuid();
        home.SetRowsForTest(id, isGroup: false, name: "Corsa");
        home.SelectedRow = home.Rows.First();
        await Task.Yield();
        vm.Workspace.PinActiveTab();

        vm.Workspace.CloseTab(vm.Workspace.Tabs[0]);
        await Task.Yield();

        var raw = await settings.GetAsync(Recordum.WPF.ViewModels.Workspace.WorkspaceViewModel.StateSettingKey);
        raw.Should().NotBeNull(); // save was triggered on the change
    }
```
(If the factory doesn't expose `settings`, add an overload mirroring the existing pattern.)

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~MainWindowViewModelTests`
Expected: FAIL — no save wiring yet.

- [ ] **Step 3: Wire restore + save**

(a) In `InitializeAsync`, replace the removed `HabitScreen.RestoreOpenColumnsAsync` call with workspace restore (after `Home.LoadCommand` so rows exist for the resolver fallback, but the resolver uses the node service directly):

```csharp
        await Workspace.RestoreStateAsync(ResolveTitle).ConfigureAwait(true);
```
and add the resolver (synchronous signature required by Plan 1; resolve name synchronously from the already-loaded Home rows, falling back to empty):
```csharp
    private string ResolveTitle(Guid id, Workspace.WorkspaceTabKind kind)
        => Home.Rows.FirstOrDefault(r => r.HabitNodeId == id)?.Name ?? string.Empty;
```
(Call `RestoreStateAsync` AFTER `await Home.LoadCommand.ExecuteAsync(null)` so `Home.Rows` is populated.)

(b) Subscribe to workspace changes and persist (debounce not required for SP1):
```csharp
        Workspace.Tabs.CollectionChanged += async (_, _) => await Workspace.SaveStateAsync().ConfigureAwait(true);
        Workspace.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName is nameof(Workspace.ActiveSubjectTab)
                or nameof(Workspace.SidebarCollapsed) or nameof(Workspace.SidebarWidth))
                await Workspace.SaveStateAsync().ConfigureAwait(true);
        };
```
Add these in the ctor (after `Workspace` is assigned).

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~MainWindowViewModelTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/MainWindowViewModel.cs tests/Recordum.WPF.Tests/ViewModels/MainWindowViewModelTests.cs
git commit -m "feat(wpf): restore workspace session on startup; persist on tab/sidebar changes"
```

---

### Task 4: Sidebar collapse command + WorkspaceViewModel.IDisposable

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs`

**Interfaces:**
- Produces: `[RelayCommand] ToggleSidebar()` flipping `SidebarCollapsed`; `WorkspaceViewModel : IDisposable` disposing every open tab's scope (the deferred Plan 1 shutdown item).

- [ ] **Step 1: Write the failing tests**

```csharp
    [Fact]
    public void ToggleSidebar_flips_collapsed()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        sut.SidebarCollapsed.Should().BeFalse();
        sut.ToggleSidebarCommand.Execute(null);
        sut.SidebarCollapsed.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_disposes_all_open_tabs_scopes()
    {
        var (sut, _, screens) = WorkspaceViewModelFactory.CreateWithScreens();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);

        sut.Dispose();

        screens.Scopes.Should().OnlyContain(s => s.Disposed);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: FAIL — `ToggleSidebarCommand`/`Dispose` missing.

- [ ] **Step 3: Implement**

Make the class `public sealed partial class WorkspaceViewModel : ObservableObject, IDisposable` and add:

```csharp
    [RelayCommand]
    private void ToggleSidebar() => SidebarCollapsed = !SidebarCollapsed;

    public void Dispose()
    {
        foreach (var tab in Tabs) tab.Dispose();
        Tabs.Clear();
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs
git commit -m "feat(wpf): sidebar toggle command + WorkspaceViewModel disposes tab scopes on shutdown"
```

---

### Task 5: Tab-bar strings

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`

**Interfaces:**
- Produces: `Strings.Workspace_NewTab` ("New tab" / "Nuovo tab"), `Strings.Workspace_CloseTab` ("Close tab" / "Chiudi tab"), `Strings.Workspace_Empty` ("Select a habit from the sidebar" / "Seleziona un'abitudine dalla barra laterale"), `Strings.Workspace_ToggleSidebar` ("Toggle sidebar" / "Mostra/nascondi barra laterale"). (`Workspace_Tab_Panoramica` already exists from Plan 1.)

- [ ] **Step 1: Add the resx entries** (EN in `Strings.resx`, IT in `Strings.it.resx`) for the four keys above, next to `Workspace.Tab.Panoramica`.

- [ ] **Step 2: Add accessors via shell (guardrail-safe)** — append after an existing `Workspace_*` accessor line:

```bash
awk '/public static string Workspace_Tab_Panoramica => Get\("Workspace.Tab.Panoramica"\);/{print; print "    public static string Workspace_NewTab => Get(\"Workspace.NewTab\");"; print "    public static string Workspace_CloseTab => Get(\"Workspace.CloseTab\");"; print "    public static string Workspace_Empty => Get(\"Workspace.Empty\");"; print "    public static string Workspace_ToggleSidebar => Get(\"Workspace.ToggleSidebar\");"; next}1' \
  src/Recordum.WPF/Resources/Strings.Designer.cs > /tmp/sd.cs && mv /tmp/sd.cs src/Recordum.WPF/Resources/Strings.Designer.cs
grep -n "Workspace_NewTab\|Workspace_CloseTab\|Workspace_Empty\|Workspace_ToggleSidebar" src/Recordum.WPF/Resources/Strings.Designer.cs
```

- [ ] **Step 3: Build** — `dotnet build src/Recordum.WPF/Recordum.WPF.csproj` → 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "feat(wpf): workspace tab-bar strings (EN+IT)"
```

---

### Task 6: WorkspaceView (tab bar + content host)

**Files:**
- Create: `src/Recordum.WPF/Views/Workspace/WorkspaceView.xaml` (+ `.xaml.cs`)

**Interfaces:**
- Consumes: `WorkspaceViewModel` (DataContext), `vhs:HabitScreen` view, the Task 5 strings.
- Produces: the visual shell content (tab strip + active screen / empty panel).

- [ ] **Step 1: Create the view**

Create `src/Recordum.WPF/Views/Workspace/WorkspaceView.xaml` as a `UserControl` with `d:DataContext` = `WorkspaceViewModel`. Structure:
- Root `DockPanel`.
- `DockPanel.Dock="Top"`: the **tab strip** — a horizontal `ItemsControl` (`ItemsSource="{Binding Tabs}"`) with a `WrapPanel`/`StackPanel(Horizontal)` items panel. Each item `DataTemplate` (DataType `wsvm:SubjectTabViewModel`):
  - a `Border` (tab chrome) containing a `TextBlock Text="{Binding Title}"` with a `Style` trigger on `IsPreview=true` → `FontStyle=Italic`, and a `Button` "✕" (`Command="{Binding DataContext.CloseTabCommand, RelativeSource=AncestorType ItemsControl}" CommandParameter="{Binding}"` — see Step 2 for the command) ;
  - the tab `Border` has an `InputBindings`/`MouseDown` to activate on single click and pin on double click (handled in code-behind, Step 3, since `WorkspaceViewModel` exposes `ActivateTab`/`PinActiveTab` methods, not parameterized commands by default).
  - active-tab highlight via a trigger comparing the item to `DataContext.ActiveSubjectTab` (use a `DataTrigger` with a converter `IsSameRef` or bind `Tag` — simplest: a code-behind selection sync, or an `ActiveSubjectTab` equality `MultiBinding`. Use the converter approach: add a tiny `ReferenceEqualsConverter` if none exists.)
  - a trailing **"+" affordance** is not needed (new subjects come from the sidebar); omit per YAGNI.
- Center (`DockPanel` fill): a `Grid` with two children:
  - `ContentControl Content="{Binding ActiveSubjectTab.Screen}"` with an implicit `DataTemplate DataType="{x:Type hsvm:HabitScreenViewModel}"` → `<vhs:HabitScreen/>`, `Visibility` bound to `IsPanoramicaActive` inverted (`InvBoolToVisible`).
  - an **empty panel**: a centered `TextBlock Text="{x:Static res:Strings.Workspace_Empty}"`, `Visibility="{Binding IsPanoramicaActive, Converter={StaticResource BoolToVisible}}"`.

Add the needed `xmlns`:
```xml
xmlns:wsvm="clr-namespace:Recordum.WPF.ViewModels.Workspace"
xmlns:hsvm="clr-namespace:Recordum.WPF.ViewModels.HabitScreen"
xmlns:vhs="clr-namespace:Recordum.WPF.Views.HabitScreen"
xmlns:res="clr-namespace:Recordum.WPF.Resources"
```
Implicit template for the screen (place in `<UserControl.Resources>`):
```xml
<DataTemplate DataType="{x:Type hsvm:HabitScreenViewModel}">
    <vhs:HabitScreen/>
</DataTemplate>
```

- [ ] **Step 2: Expose parameterized close + activate for the tab template**

`WorkspaceViewModel` currently has plain methods `CloseTab(tab)`, `ActivateTab(tab)`, `PinActiveTab()`. Add `[RelayCommand]` wrappers so XAML can bind with `CommandParameter`:
```csharp
    [RelayCommand] private void CloseTabCmd(SubjectTabViewModel tab) => CloseTab(tab);
    [RelayCommand] private void ActivateTabCmd(SubjectTabViewModel tab) { ActivateTab(tab); }
```
(Generated commands: `CloseTabCmdCommand`, `ActivateTabCmdCommand`. Name them `CloseTabCommand`/`ActivateTabCommand` if you prefer by renaming the methods to `CloseTabCommandImpl`—pick clear names and use them consistently in the XAML.)
Add a tiny unit test in `WorkspaceViewModelTests` that `CloseTabCmdCommand.Execute(tab)` removes the tab (mirrors the existing CloseTab test) to keep the command covered.

- [ ] **Step 3: Code-behind for single-click activate / double-click pin**

In `WorkspaceView.xaml.cs`, handle the tab border's `MouseLeftButtonUp` (single → `ActivateTab`) and `MouseDoubleClick` (→ `PinActiveTab` after activating). Resolve the `SubjectTabViewModel` from `((FrameworkElement)sender).DataContext` and the `WorkspaceViewModel` from the control's `DataContext`. Keep logic minimal; the VM holds the behavior.

- [ ] **Step 4: Build (XAML parses)**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: Build succeeded. (XAML errors are usually a wrong xmlns — copy exact namespaces from existing views.)

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Views/Workspace/WorkspaceView.xaml src/Recordum.WPF/Views/Workspace/WorkspaceView.xaml.cs src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs
git commit -m "feat(wpf): WorkspaceView tab strip + per-tab HabitScreen host + empty state"
```

---

### Task 7: MainWindow restructure — collapsible sidebar + WorkspaceView

**Files:**
- Modify: `src/Recordum.WPF/MainWindow.xaml`
- Modify: `src/Recordum.WPF/MainWindow.xaml.cs`
- Modify: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs` (remove now-dead HabitScreen-width members)

**Interfaces:**
- Consumes: `MainWindowViewModel.Workspace`, `Workspace.SidebarCollapsed/SidebarWidth/ToggleSidebarCommand`, `v:WorkspaceView` (new), existing `v:HomeView`.

- [ ] **Step 1: Swap the Home-workspace layout**

In `MainWindow.xaml`, the Home-workspace `Grid` (the one gated by `IsHomeWorkspace`, currently `[HomeView *][splitter][HabitScreen 0..1100]`) becomes `[Sidebar (HomeView)][splitter][WorkspaceView *]`:
```xml
<Grid Visibility="{Binding IsHomeWorkspace, Converter={StaticResource BoolToVisible}}">
    <Grid.ColumnDefinitions>
        <ColumnDefinition x:Name="SidebarColumn" Width="{Binding Workspace.SidebarWidth, Mode=OneWay}" MinWidth="0" MaxWidth="460"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>
    <v:HomeView Grid.Column="0" DataContext="{Binding Home}"
                Visibility="{Binding Workspace.SidebarCollapsed, Converter={StaticResource InvBoolToVisible}}"/>
    <GridSplitter x:Name="SidebarSplitter" Grid.Column="1" Width="4"
                  HorizontalAlignment="Stretch" VerticalAlignment="Stretch"
                  ResizeBehavior="PreviousAndNext" Background="{DynamicResource Brush.Border}"
                  ShowsPreview="False" DragCompleted="OnSidebarSplitterDragCompleted"
                  Visibility="{Binding Workspace.SidebarCollapsed, Converter={StaticResource InvBoolToVisible}}"/>
    <v:WorkspaceView Grid.Column="2" DataContext="{Binding Workspace}"/>
</Grid>
```
Remove the old `HabitScreenColumn`/`HabitScreenSplitter`/`HabitScreenContent` block and its `OnHabitScreenSplitterDragCompleted` handler.

- [ ] **Step 2: Sidebar collapse toggle + width persistence in code-behind**

In `MainWindow.xaml.cs`:
- Replace `OnHabitScreenSplitterDragCompleted` with `OnSidebarSplitterDragCompleted` that writes the new width back: set `vm.Workspace.SidebarWidth = SidebarColumn.ActualWidth` then `await vm.Workspace.SaveStateAsync()` (the PropertyChanged save in Task 3 also fires). Clamp to `[0, 460]`.
- Add a sidebar toggle button somewhere in the title bar or ribbon: bind `Command="{Binding Workspace.ToggleSidebarCommand}"`, tooltip `Strings.Workspace_ToggleSidebar`. (Place a small `≡` `Button` at the left of the content area or in `WorkspaceRibbon` — simplest: add it to the title-bar left `StackPanel` after the app name.)

Add `using` as needed. Resolve `MainWindowViewModel` from `DataContext`.

- [ ] **Step 3: Remove dead HabitScreen-width members**

In `MainWindowViewModel.cs`, delete `HabitScreenWidthSettingKey`, `DefaultHabitScreenWidth`, `MinHabitScreenWidth`, `MaxHabitScreenWidth`, `HabitScreenWidth`, `CommitHabitScreenWidthAsync`, `LoadHabitScreenWidthAsync`, and the `LoadHabitScreenWidthAsync` call in `InitializeAsync` (now unused after the XAML swap). Keep `LoadWidthAsync` only if still used elsewhere; otherwise remove it too.

- [ ] **Step 4: Build**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: 0 errors (no remaining references to removed members or the old `HabitScreen` binding).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/MainWindow.xaml src/Recordum.WPF/MainWindow.xaml.cs src/Recordum.WPF/ViewModels/MainWindowViewModel.cs
git commit -m "feat(wpf): MainWindow shell — collapsible sidebar + WorkspaceView tab host"
```

---

### Task 8: Keyboard, full suite, manual verification

**Files:**
- Modify: `src/Recordum.WPF/MainWindow.xaml` (InputBindings)

- [ ] **Step 1: Add Ctrl+W to close the active tab**

In `MainWindow.xaml` `Window.InputBindings`, add:
```xml
<KeyBinding Key="W" Modifiers="Control" Command="{Binding Workspace.CloseActiveTabCommand}"/>
```
Add a `[RelayCommand] private void CloseActiveTab() { if (ActiveSubjectTab is { } t) CloseTab(t); }` to `WorkspaceViewModel` (+ a one-line unit test that it closes the active tab). Rebuild.

- [ ] **Step 2: Full build + suites**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj` then `dotnet test tests/Recordum.WPF.Tests` then `dotnet test tests/Recordum.Data.Tests`.
Expected: all green. WPF ≥ 604 + new workspace/mainwindow tests; Data 343.

- [ ] **Step 3: Manual on-screen verification (author)**

Use the `run` skill (`--clean --seed-demo`). Verify:
1. Left sidebar = the full habit tree (groups, color bars, Active/Archived/Trash) — collapsible via the ≡ toggle; width persists across restart.
2. Single-click a habit → opens an italic **preview** tab on the right showing that habit's `HabitScreen` (all its columns/chips work). Single-click another habit → the preview tab is **replaced**, not accumulated.
3. Double-click a tab (or record/save something) → the tab becomes **pinned** (non-italic) and survives further single-clicks.
4. ✕ closes a tab; closing the last tab shows the empty "select a habit" panel; `Ctrl+W` closes the active tab.
5. Open a journal habit → its `HabitScreen` shows the diary editor (Plan 3 will give it a dedicated focusable pane; for now confirm it still works inside the tab).
6. Restart the app → pinned tabs + active tab + sidebar state are restored.
7. Charts (Ctrl+4) and Backup history (Ctrl+5) workspaces still switch and render.

- [ ] **Step 4: Update memory**

Update `recordum-workspace-shell-sp1.md`: mark Plan 2 done, note the HabitScreen-per-tab reuse decision (Plan 3's per-tab panes largely satisfied), the empty-default-tab simplification, and final test counts.

---

## Self-Review

**Spec coverage (Plan 2 slice):**
- §3.1 sidebar (rich tree, collapsible, width persisted) → Tasks 4, 7. ✓
- §3.2 tab bar (open/activate/close/preview-italic) → Tasks 1, 6; reorder via `MoveTab` exists (Plan 1) — drag-reorder UI is optional polish, noted not built here (YAGNI for SP1 unless desired). ✓ (gap: drag-reorder UI — intentionally deferred)
- §3.3 preview/pin/promote → driven by Plan 1 via Tasks 1–3 wiring. ✓
- §3.7 close affordances + Ctrl+W + click-empty default → Tasks 6, 8. ✓
- §4 MainWindow restructure, per-tab screen, event relocation, persistence → Tasks 1–7. ✓
- §5 Panoramica default → simplified to an empty/welcome panel (stated deliberate scope cut). ⚠️ documented.
- Diario dedicated pane + focus mode → Plan 3 (out of scope). ✓

**Placeholder scan:** Task 1 Step 2a and Task 3 Step 1 reference a Home-row seeding seam (`SetRowsForTest`) that must be matched to the real test helper — these are explicit "use the existing mechanism" verification instructions, not code placeholders. Task 6 Step 1's active-tab highlight names a converter to add if missing. No "TBD"/"handle errors". All code steps carry code.

**Type consistency:** `WorkspaceTabKind`, `OpenSubjectAsync(Guid,WorkspaceTabKind,string,bool,CancellationToken)`, `KindOf(HomeRowViewModel)`, `ResolveTitle(Guid,WorkspaceTabKind)`, the new events (`ExportRequested`/`SubjectActivationRequested` on VM; `AllColumnsClosed`/`ExportRequested`/`ChildSelected` on tab), and the `[RelayCommand]` names are used consistently across tasks. `HabitScreenViewModel.Name` is the tab title source (confirm the property name in code).

**Risks to watch during execution:**
- **Home-row test seeding** (Task 1/3): must reuse the real mechanism; if none exists, drive `Home.LoadCommand` over a `FakeHabitNodeService` seeded with the node.
- **`HomeRowViewModel` display-name property**: confirm it is `Name` (used for tab titles); adjust if different.
- **Mid-plan launchability**: Task 1 leaves `MainWindow.xaml` still bound to the removed `HabitScreen` until Task 6/7. Keep the project COMPILING each task (the binding is runtime, not compile — but `d:DataContext` design instance may complain; if the build breaks, stub the binding to `Workspace` and finalize in Task 7). The full manual run is Task 8.
- **Per-group Panoramica child-select**: `HabitScreenViewModel.Panoramica.ChildSelected` must still route (now via the tab → workspace `SubjectActivationRequested`). Verify the per-group Panoramica still works inside a tab.
- **Lambda subscriptions in OpenSubjectAsync (Task 2 Step 3c)**: ensure they don't leak — they're rooted by the tab and released when the tab is unreferenced after `CloseTab`/`Dispose`. Acceptable for SP1; if a leak test is cheap, add it.
