# Workspace Shell SP1 — Plan 1 of 3: Tab Model & Persistence (VM foundation)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the testable ViewModel foundation of the tabbed workspace shell — a `WorkspaceViewModel` that owns a collection of subject tabs with browser-style preview/pin lifecycle, a default Panoramica tab, and light session persistence — with zero XAML changes yet.

**Architecture:** A new `WorkspaceViewModel` owns `ObservableCollection<SubjectTabViewModel>` plus the always-present Panoramica. Each `SubjectTabViewModel` wraps one per-tab `HabitScreenViewModel` (created through an injected factory delegate, since each tab needs its own instance) and carries the tab's identity + preview/pinned state. Opening a subject creates a single *preview* tab that the next open replaces; a double-open or an explicit pin promotes it to a persistent tab. State (pinned tabs + active tab + sidebar collapse/width) round-trips through a new `ui.workspace` `AppSetting` JSON blob that supersedes `ui.open_columns`. This plan ships no UI: the shell XAML (Plan 2) and per-tab panes / Diario pane / focus mode (Plan 3) consume this foundation.

**Tech Stack:** C# / .NET 10, WPF + CommunityToolkit.Mvvm (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`), System.Text.Json, xUnit + FluentAssertions, Microsoft.Extensions.DependencyInjection.

**Source spec:** `docs/superpowers/specs/2026-06-20-workspace-shell-sp1-design.md`. This plan covers §3.2 (tab bar state), §3.3 (preview+pin model), §6 (light persistence), and the `WorkspaceViewModel`/`SubjectTabViewModel` parts of §4. The shell XAML (§3.1 sidebar, §3.4–3.7 panes/focus rendering) and the Diario pane extraction are deferred to Plans 2 and 3.

## SP1 staging (context)

- **Plan 1 (this):** tab model + lifecycle + persistence — pure ViewModel + unit tests. No visible UI change yet.
- **Plan 2:** `MainWindow` shell restructure (`[Sidebar tree][splitter][Tab host]`), tab-bar UI, collapsible rich sidebar, wiring `HomeViewModel` selection → `WorkspaceViewModel.OpenSubject`.
- **Plan 3:** per-tab panes (move `HabitColumn` scope into the tab), `HabitColumn.Diario` pane extraction for journals, focus/expand mode.

## Global Constraints

- **MVVM** with CommunityToolkit.Mvvm. UI-thread continuations use `.ConfigureAwait(true)` (match surrounding files).
- **Never** `DateTime.UtcNow`/`Now`; all time via injected `IClock`/`TimeProvider`. (Not needed in this plan, but holds.)
- **User-facing strings** in `.resx` (English-first, Italian secondary). This plan adds the Panoramica tab title string.
- **DI everywhere** via `Microsoft.Extensions.DependencyInjection`. `HabitScreenViewModel` is registered `AddTransient` (`ServiceRegistrar.cs:149`); per-tab instances come from an injected factory delegate, not a captured singleton.
- **Persistence** via `IAppSettingService` only. New key `ui.workspace` supersedes `ui.open_columns`; a stale `ui.open_columns` value is ignored, never parsed.
- Existing test baselines to keep green: **WPF 576**, **Data 343**. This plan raises WPF.

## Interfaces introduced by this plan (consumed by Plans 2–3)

```csharp
// Recordum.WPF.ViewModels.Workspace
public enum WorkspaceTabKind { Habit, Group }

public sealed partial class SubjectTabViewModel : ObservableObject
{
    public Guid SubjectId { get; }
    public WorkspaceTabKind Kind { get; }
    public HabitScreenViewModel Screen { get; }
    public string Title { get; }            // observable
    public bool IsPreview { get; }          // observable; true = transient italic tab
    public void Pin();                      // IsPreview -> false
}

public sealed partial class WorkspaceViewModel : ObservableObject
{
    public ObservableCollection<SubjectTabViewModel> Tabs { get; }
    public SubjectTabViewModel? ActiveSubjectTab { get; }  // null when Panoramica active
    public bool IsPanoramicaActive { get; }
    public bool SidebarCollapsed { get; set; }
    public double SidebarWidth { get; set; }

    public Task OpenSubjectAsync(Guid subjectId, WorkspaceTabKind kind, string title, bool pin = false, CancellationToken ct = default);
    public void ActivatePanoramica();
    public void ActivateTab(SubjectTabViewModel tab);
    public void CloseTab(SubjectTabViewModel tab);
    public void MoveTab(SubjectTabViewModel tab, int newIndex);
    public void PinActiveTab();
    public Task SaveStateAsync(CancellationToken ct = default);
    public Task RestoreStateAsync(Func<Guid, WorkspaceTabKind, string> titleResolver, CancellationToken ct = default);
}
```

---

### Task 1: Persistence DTO + JSON round-trip

**Files:**
- Create: `src/Recordum.WPF/ViewModels/Workspace/WorkspaceState.cs`
- Create: `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceStateTests.cs`

**Interfaces:**
- Produces: `WorkspaceTabKind` enum; `WorkspaceTabState` record; `WorkspaceState` record; `WorkspaceState.ToJson()` / `WorkspaceState.FromJson(string?)`.
- Consumes: nothing.

- [ ] **Step 1: Write the failing test**

Create `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceStateTests.cs`:

```csharp
using FluentAssertions;
using Recordum.WPF.ViewModels.Workspace;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Workspace;

public class WorkspaceStateTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var id = Guid.NewGuid();
        var state = new WorkspaceState
        {
            SidebarCollapsed = true,
            SidebarWidth = 240,
            ActiveTabId = id.ToString(),
            Tabs = [new WorkspaceTabState(id, WorkspaceTabKind.Habit)],
        };

        var restored = WorkspaceState.FromJson(state.ToJson());

        restored.Should().NotBeNull();
        restored!.SidebarCollapsed.Should().BeTrue();
        restored.SidebarWidth.Should().Be(240);
        restored.ActiveTabId.Should().Be(id.ToString());
        restored.Tabs.Should().ContainSingle();
        restored.Tabs[0].SubjectId.Should().Be(id);
        restored.Tabs[0].Kind.Should().Be(WorkspaceTabKind.Habit);
    }

    [Fact]
    public void FromJson_returns_null_for_null_empty_or_garbage()
    {
        WorkspaceState.FromJson(null).Should().BeNull();
        WorkspaceState.FromJson("").Should().BeNull();
        WorkspaceState.FromJson("{not json").Should().BeNull();
    }

    [Fact]
    public void Default_active_is_panoramica_sentinel()
    {
        WorkspaceState.PanoramicaId.Should().Be("panoramica");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceStateTests`
Expected: FAIL / does not compile — types do not exist.

- [ ] **Step 3: Create the DTO**

Create `src/Recordum.WPF/ViewModels/Workspace/WorkspaceState.cs`:

```csharp
using System.Text.Json;

namespace Recordum.WPF.ViewModels.Workspace;

/// <summary>Whether an open tab points at a habit or a group node.</summary>
public enum WorkspaceTabKind { Habit, Group }

/// <summary>One persisted (pinned) tab: which node and its kind.</summary>
public sealed record WorkspaceTabState(Guid SubjectId, WorkspaceTabKind Kind);

/// <summary>Light session state for the workspace shell (spec §6). Persisted in the
/// <c>ui.workspace</c> AppSetting. Only pinned tabs are stored; the preview tab, the per-tab
/// open-pane set, pane widths, and focus state are intentionally NOT persisted.</summary>
public sealed record WorkspaceState
{
    /// <summary>Sentinel <see cref="ActiveTabId"/> value meaning "the always-present Panoramica tab".</summary>
    public const string PanoramicaId = "panoramica";

    public bool SidebarCollapsed { get; init; }
    public double SidebarWidth { get; init; }
    /// <summary>A pinned tab's SubjectId as string, or <see cref="PanoramicaId"/>.</summary>
    public string ActiveTabId { get; init; } = PanoramicaId;
    public IReadOnlyList<WorkspaceTabState> Tabs { get; init; } = [];

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Parses persisted JSON; returns null for null/empty/malformed input (caller falls back to defaults).</summary>
    public static WorkspaceState? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<WorkspaceState>(json, Options); }
        catch (JsonException) { return null; }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceStateTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Workspace/WorkspaceState.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceStateTests.cs
git commit -m "feat(wpf): WorkspaceState persistence DTO + JSON round-trip"
```

---

### Task 2: SubjectTabViewModel

**Files:**
- Create: `src/Recordum.WPF/ViewModels/Workspace/SubjectTabViewModel.cs`
- Create: `tests/Recordum.WPF.Tests/ViewModels/Workspace/SubjectTabViewModelTests.cs`

**Interfaces:**
- Consumes: `WorkspaceTabKind` (Task 1), `HabitScreenViewModel` (existing).
- Produces: `SubjectTabViewModel(Guid subjectId, WorkspaceTabKind kind, string title, HabitScreenViewModel screen, bool isPreview)`; props `SubjectId`, `Kind`, `Screen`, `Title`, `IsPreview`; `void Pin()`.

- [ ] **Step 1: Write the failing test**

Create `tests/Recordum.WPF.Tests/ViewModels/Workspace/SubjectTabViewModelTests.cs`:

```csharp
using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Workspace;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Workspace;

public class SubjectTabViewModelTests
{
    private static SubjectTabViewModel NewTab(bool isPreview)
    {
        var (screen, _) = HabitScreenViewModelFactory.Create();
        return new SubjectTabViewModel(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", screen, isPreview);
    }

    [Fact]
    public void Carries_identity_and_screen()
    {
        var tab = NewTab(isPreview: true);
        tab.Title.Should().Be("Corsa");
        tab.Kind.Should().Be(WorkspaceTabKind.Habit);
        tab.Screen.Should().NotBeNull();
        tab.IsPreview.Should().BeTrue();
    }

    [Fact]
    public void Pin_clears_preview_and_raises_change()
    {
        var tab = NewTab(isPreview: true);
        var changed = new List<string?>();
        tab.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        tab.Pin();

        tab.IsPreview.Should().BeFalse();
        changed.Should().Contain(nameof(SubjectTabViewModel.IsPreview));
    }

    [Fact]
    public void Pin_is_idempotent_on_already_pinned()
    {
        var tab = NewTab(isPreview: false);
        tab.Pin();
        tab.IsPreview.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~SubjectTabViewModelTests`
Expected: FAIL / does not compile — `SubjectTabViewModel` does not exist.

- [ ] **Step 3: Create the ViewModel**

Create `src/Recordum.WPF/ViewModels/Workspace/SubjectTabViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.ViewModels.Workspace;

/// <summary>One open tab in the workspace shell. Wraps a per-tab <see cref="HabitScreenViewModel"/>
/// (its panes/content) and the tab's identity + preview state. A "preview" tab is the transient,
/// italic, single-click tab that the next single open replaces; pinning makes it persistent.</summary>
public sealed partial class SubjectTabViewModel : ObservableObject
{
    public Guid SubjectId { get; }
    public WorkspaceTabKind Kind { get; }
    public HabitScreenViewModel Screen { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isPreview;

    public SubjectTabViewModel(Guid subjectId, WorkspaceTabKind kind, string title,
        HabitScreenViewModel screen, bool isPreview)
    {
        ArgumentNullException.ThrowIfNull(screen);
        SubjectId = subjectId;
        Kind = kind;
        Screen = screen;
        _title = title;
        _isPreview = isPreview;
    }

    /// <summary>Promote a preview tab to a persistent one (no-op if already pinned).</summary>
    public void Pin() => IsPreview = false;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~SubjectTabViewModelTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Workspace/SubjectTabViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/SubjectTabViewModelTests.cs
git commit -m "feat(wpf): SubjectTabViewModel (tab identity + preview/pin)"
```

---

### Task 3: WorkspaceViewModel skeleton — Panoramica default + factory

**Files:**
- Create: `src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs`
- Create: `tests/Recordum.WPF.Tests/Fixtures/WorkspaceViewModelFactory.cs`
- Create: `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs`

**Interfaces:**
- Consumes: `SubjectTabViewModel` (Task 2), `IAppSettingService` (existing), `Func<HabitScreenViewModel>` (per-tab screen factory).
- Produces: `WorkspaceViewModel(Func<HabitScreenViewModel> screenFactory, IAppSettingService appSettings)`; props `Tabs`, `ActiveSubjectTab`, `IsPanoramicaActive`, `SidebarCollapsed`, `SidebarWidth`; `ActivatePanoramica()`.

- [ ] **Step 1: Create the test factory**

Create `tests/Recordum.WPF.Tests/Fixtures/WorkspaceViewModelFactory.cs`:

```csharp
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Workspace;

namespace Recordum.WPF.Tests.Fixtures;

/// <summary>Builds a WorkspaceViewModel whose per-tab HabitScreenViewModels come from the existing
/// HabitScreenViewModelFactory (all-fakes). Tab-lifecycle tests don't exercise screen content, so a
/// fresh fake screen per tab is fine.</summary>
public static class WorkspaceViewModelFactory
{
    public static (WorkspaceViewModel Vm, FakeAppSettingService Settings) Create()
    {
        var settings = new FakeAppSettingService();
        Func<HabitScreenViewModel> screenFactory = () => HabitScreenViewModelFactory.Create().Vm;
        return (new WorkspaceViewModel(screenFactory, settings), settings);
    }
}
```

- [ ] **Step 2: Write the failing test**

Create `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs`:

```csharp
using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Workspace;

public class WorkspaceViewModelTests
{
    [Fact]
    public void Starts_with_panoramica_active_and_no_tabs()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        sut.Tabs.Should().BeEmpty();
        sut.ActiveSubjectTab.Should().BeNull();
        sut.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public void Sidebar_defaults_expanded_with_a_sensible_width()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        sut.SidebarCollapsed.Should().BeFalse();
        sut.SidebarWidth.Should().BeGreaterThan(0);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: FAIL / does not compile — `WorkspaceViewModel` does not exist.

- [ ] **Step 4: Create the skeleton**

Create `src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.ViewModels.Workspace;

/// <summary>Owns the tabbed workspace shell: the open subject tabs, the always-present Panoramica
/// (active when no subject tab is), the preview/pin lifecycle, and light session persistence.
/// Ships no XAML; Plans 2–3 render it.</summary>
public sealed partial class WorkspaceViewModel : ObservableObject
{
    internal const double DefaultSidebarWidth = 280d;

    private readonly Func<HabitScreenViewModel> _screenFactory;
    private readonly IAppSettingService _appSettings;

    public ObservableCollection<SubjectTabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanoramicaActive))]
    private SubjectTabViewModel? _activeSubjectTab;

    [ObservableProperty] private bool _sidebarCollapsed;
    [ObservableProperty] private double _sidebarWidth = DefaultSidebarWidth;

    public bool IsPanoramicaActive => ActiveSubjectTab is null;

    public WorkspaceViewModel(Func<HabitScreenViewModel> screenFactory, IAppSettingService appSettings)
    {
        ArgumentNullException.ThrowIfNull(screenFactory);
        ArgumentNullException.ThrowIfNull(appSettings);
        _screenFactory = screenFactory;
        _appSettings = appSettings;
    }

    /// <summary>Make the Panoramica the active view (no subject tab selected).</summary>
    public void ActivatePanoramica() => ActiveSubjectTab = null;
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: 2 passed.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs tests/Recordum.WPF.Tests/Fixtures/WorkspaceViewModelFactory.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs
git commit -m "feat(wpf): WorkspaceViewModel skeleton (Panoramica default + screen factory)"
```

---

### Task 4: OpenSubjectAsync — preview semantics

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs`

**Interfaces:**
- Produces: `Task OpenSubjectAsync(Guid subjectId, WorkspaceTabKind kind, string title, bool pin = false, CancellationToken ct = default)`.
- Consumes: `_screenFactory`, `HabitScreenViewModel.LoadAsync(Guid?)` (existing).

- [ ] **Step 1: Write the failing tests**

Add to `WorkspaceViewModelTests`:

```csharp
    [Fact]
    public async Task Single_open_creates_one_preview_tab_and_activates_it()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        var id = Guid.NewGuid();

        await sut.OpenSubjectAsync(id, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");

        sut.Tabs.Should().ContainSingle();
        sut.Tabs[0].IsPreview.Should().BeTrue();
        sut.Tabs[0].SubjectId.Should().Be(id);
        sut.ActiveSubjectTab.Should().Be(sut.Tabs[0]);
        sut.IsPanoramicaActive.Should().BeFalse();
    }

    [Fact]
    public async Task Second_single_open_replaces_the_preview_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");
        var second = Guid.NewGuid();

        await sut.OpenSubjectAsync(second, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Peso");

        sut.Tabs.Should().ContainSingle();
        sut.Tabs[0].SubjectId.Should().Be(second);
        sut.Tabs[0].IsPreview.Should().BeTrue();
    }

    [Fact]
    public async Task Opening_an_already_open_subject_activates_it_without_duplicating()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        var id = Guid.NewGuid();
        await sut.OpenSubjectAsync(id, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Peso", pin: true);

        await sut.OpenSubjectAsync(id, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");

        sut.Tabs.Where(t => t.SubjectId == id).Should().ContainSingle();
        sut.ActiveSubjectTab!.SubjectId.Should().Be(id);
    }

    [Fact]
    public async Task Open_with_pin_true_creates_a_pinned_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        await sut.OpenSubjectAsync(Guid.NewGuid(), Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa", pin: true);

        sut.Tabs[0].IsPreview.Should().BeFalse();
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: FAIL / does not compile — `OpenSubjectAsync` does not exist.

- [ ] **Step 3: Implement OpenSubjectAsync**

In `WorkspaceViewModel.cs`, add a field to track the preview tab and the method:

```csharp
    private SubjectTabViewModel? _previewTab;
```

(place next to the other fields), and the method (inside the class):

```csharp
    /// <summary>Open a subject as a tab. Single open = a transient preview tab that the next single
    /// open replaces; <paramref name="pin"/> = a persistent tab. Re-opening an already-open subject
    /// just activates it (spec §3.3).</summary>
    public async Task OpenSubjectAsync(Guid subjectId, WorkspaceTabKind kind, string title,
        bool pin = false, CancellationToken ct = default)
    {
        var existing = Tabs.FirstOrDefault(t => t.SubjectId == subjectId);
        if (existing is not null)
        {
            if (pin) existing.Pin();
            if (!existing.IsPreview) _previewTab = _previewTab == existing ? null : _previewTab;
            ActiveSubjectTab = existing;
            return;
        }

        var screen = _screenFactory();
        var tab = new SubjectTabViewModel(subjectId, kind, title, screen, isPreview: !pin);

        if (!pin && _previewTab is not null)
        {
            // Replace the existing preview tab in place (no accumulation).
            var idx = Tabs.IndexOf(_previewTab);
            Tabs[idx] = tab;
        }
        else
        {
            Tabs.Add(tab);
        }
        _previewTab = pin ? null : tab;
        ActiveSubjectTab = tab;

        await screen.LoadAsync(subjectId, ct).ConfigureAwait(true);
    }
```

Note: `HabitScreenViewModel.LoadAsync` takes a `Guid?` and is the existing per-subject loader; here it loads the tab's own screen instance.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: all pass (6).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs
git commit -m "feat(wpf): WorkspaceViewModel.OpenSubjectAsync preview/pin/activate semantics"
```

---

### Task 5: Pin, activate, close, reorder

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs`

**Interfaces:**
- Produces: `void PinActiveTab()`, `void ActivateTab(SubjectTabViewModel tab)`, `void CloseTab(SubjectTabViewModel tab)`, `void MoveTab(SubjectTabViewModel tab, int newIndex)`.

- [ ] **Step 1: Write the failing tests**

Add to `WorkspaceViewModelTests` (usings already include the Workspace namespace via the fixture; add `using Recordum.WPF.ViewModels.Workspace;` to the file's top if not present):

```csharp
    [Fact]
    public async Task PinActiveTab_promotes_the_preview_and_clears_preview_slot()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");

        sut.PinActiveTab();

        sut.ActiveSubjectTab!.IsPreview.Should().BeFalse();
        // A later single open now ADDS rather than replaces (preview slot is empty).
        var n = sut.Tabs.Count;
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Peso");
        sut.Tabs.Should().HaveCount(n + 1);
    }

    [Fact]
    public async Task CloseTab_removes_it_and_closing_last_returns_to_panoramica()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        var tab = sut.Tabs[0];

        sut.CloseTab(tab);

        sut.Tabs.Should().BeEmpty();
        sut.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public async Task CloseTab_activates_a_neighbour_when_others_remain()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);
        var first = sut.Tabs[0];
        sut.ActivateTab(first);

        sut.CloseTab(first);

        sut.Tabs.Should().ContainSingle();
        sut.ActiveSubjectTab.Should().Be(sut.Tabs[0]);
    }

    [Fact]
    public async Task MoveTab_reorders()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);
        var a = sut.Tabs[0];

        sut.MoveTab(a, 1);

        sut.Tabs[1].Should().Be(a);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: FAIL / does not compile — the four methods don't exist.

- [ ] **Step 3: Implement the methods**

In `WorkspaceViewModel.cs` add:

```csharp
    public void PinActiveTab()
    {
        if (ActiveSubjectTab is { } t)
        {
            t.Pin();
            if (_previewTab == t) _previewTab = null;
        }
    }

    public void ActivateTab(SubjectTabViewModel tab) => ActiveSubjectTab = tab;

    public void CloseTab(SubjectTabViewModel tab)
    {
        var idx = Tabs.IndexOf(tab);
        if (idx < 0) return;
        if (_previewTab == tab) _previewTab = null;
        Tabs.RemoveAt(idx);
        if (ActiveSubjectTab == tab)
            ActiveSubjectTab = Tabs.Count == 0 ? null : Tabs[Math.Min(idx, Tabs.Count - 1)];
    }

    public void MoveTab(SubjectTabViewModel tab, int newIndex)
    {
        var old = Tabs.IndexOf(tab);
        if (old < 0) return;
        newIndex = Math.Clamp(newIndex, 0, Tabs.Count - 1);
        if (old != newIndex) Tabs.Move(old, newIndex);
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs
git commit -m "feat(wpf): WorkspaceViewModel pin/activate/close/reorder tab commands"
```

---

### Task 6: Promote-on-edit (the preview tab pins when its subject is mutated)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs`

**Interfaces:**
- Consumes: `HabitScreenViewModel` events that signal a mutation. The simplest existing signal is the global `IEventBus` (`CompletionRecorded`/`NoteCreated`/…), but to keep this VM decoupled from the bus we promote on the **tab's own screen activity**. For Plan 1 we expose a public hook `NotifySubjectEdited(Guid subjectId)` that Plan 3 calls from the per-tab save paths; here we unit-test the hook.
- Produces: `void NotifySubjectEdited(Guid subjectId)`.

- [ ] **Step 1: Write the failing test**

Add to `WorkspaceViewModelTests`:

```csharp
    [Fact]
    public async Task NotifySubjectEdited_promotes_the_matching_preview_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        var id = Guid.NewGuid();
        await sut.OpenSubjectAsync(id, WorkspaceTabKind.Habit, "Diario");
        sut.Tabs[0].IsPreview.Should().BeTrue();

        sut.NotifySubjectEdited(id);

        sut.Tabs[0].IsPreview.Should().BeFalse();
    }

    [Fact]
    public async Task NotifySubjectEdited_ignores_unknown_subjects()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Diario");

        sut.NotifySubjectEdited(Guid.NewGuid());

        sut.Tabs[0].IsPreview.Should().BeTrue();
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: FAIL / does not compile — `NotifySubjectEdited` does not exist.

- [ ] **Step 3: Implement the hook**

In `WorkspaceViewModel.cs` add:

```csharp
    /// <summary>Called when the user mutates a subject (saved a diary entry, recorded a completion,
    /// edited config). Promotes that subject's preview tab to a persistent one (spec §3.3). Plan 3
    /// wires the per-tab save paths to call this; in Plan 1 it is unit-tested directly.</summary>
    public void NotifySubjectEdited(Guid subjectId)
    {
        var tab = Tabs.FirstOrDefault(t => t.SubjectId == subjectId && t.IsPreview);
        if (tab is null) return;
        tab.Pin();
        if (_previewTab == tab) _previewTab = null;
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs
git commit -m "feat(wpf): WorkspaceViewModel.NotifySubjectEdited promotes preview on mutation"
```

---

### Task 7: Persistence — SaveStateAsync / RestoreStateAsync

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs`

**Interfaces:**
- Consumes: `IAppSettingService.GetAsync/SetAsync`, `WorkspaceState` (Task 1).
- Produces: `internal const string StateSettingKey = "ui.workspace";`, `Task SaveStateAsync(CancellationToken)`, `Task RestoreStateAsync(Func<Guid, WorkspaceTabKind, string> titleResolver, CancellationToken)`.

- [ ] **Step 1: Write the failing tests**

Add to `WorkspaceViewModelTests`:

```csharp
    [Fact]
    public async Task SaveState_persists_only_pinned_tabs_plus_active_and_sidebar()
    {
        var (sut, settings) = WorkspaceViewModelFactory.Create();
        var pinned = Guid.NewGuid();
        await sut.OpenSubjectAsync(pinned, WorkspaceTabKind.Habit, "Corsa", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "preview"); // preview, must NOT persist
        sut.ActivateTab(sut.Tabs.First(t => t.SubjectId == pinned));
        sut.SidebarCollapsed = true;
        sut.SidebarWidth = 240;

        await sut.SaveStateAsync();

        var json = await settings.GetAsync(WorkspaceViewModel.StateSettingKey);
        var state = WorkspaceState.FromJson(json);
        state!.Tabs.Should().ContainSingle();
        state.Tabs[0].SubjectId.Should().Be(pinned);
        state.ActiveTabId.Should().Be(pinned.ToString());
        state.SidebarCollapsed.Should().BeTrue();
        state.SidebarWidth.Should().Be(240);
    }

    [Fact]
    public async Task SaveState_records_panoramica_when_no_subject_active()
    {
        var (sut, settings) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        sut.ActivatePanoramica();

        await sut.SaveStateAsync();

        var state = WorkspaceState.FromJson(await settings.GetAsync(WorkspaceViewModel.StateSettingKey));
        state!.ActiveTabId.Should().Be(WorkspaceState.PanoramicaId);
    }

    [Fact]
    public async Task RestoreState_reopens_pinned_tabs_and_active()
    {
        var (writer, settings) = WorkspaceViewModelFactory.Create();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await writer.OpenSubjectAsync(a, WorkspaceTabKind.Habit, "A", pin: true);
        await writer.OpenSubjectAsync(b, WorkspaceTabKind.Group, "B", pin: true);
        writer.ActivateTab(writer.Tabs.First(t => t.SubjectId == b));
        await writer.SaveStateAsync();

        // New VM over the SAME settings store.
        Func<Recordum.WPF.ViewModels.HabitScreen.HabitScreenViewModel> factory =
            () => HabitScreenViewModelFactory.Create().Vm;
        var sut = new WorkspaceViewModel(factory, settings);

        await sut.RestoreStateAsync((id, kind) => kind == WorkspaceTabKind.Group ? "B" : "A");

        sut.Tabs.Select(t => t.SubjectId).Should().Equal(a, b);
        sut.Tabs.Should().OnlyContain(t => !t.IsPreview);
        sut.ActiveSubjectTab!.SubjectId.Should().Be(b);
    }

    [Fact]
    public async Task RestoreState_ignores_a_stale_open_columns_value_and_defaults_clean()
    {
        var (sut, settings) = WorkspaceViewModelFactory.Create();
        await settings.SetAsync("ui.open_columns", "Cronologia,Note"); // legacy key, must be ignored

        await sut.RestoreStateAsync((id, kind) => "x");

        sut.Tabs.Should().BeEmpty();
        sut.IsPanoramicaActive.Should().BeTrue();
    }
```

Note: `RestoreStateAsync` takes a `titleResolver(Guid, WorkspaceTabKind) -> string` so the workspace doesn't depend on the tree/node service directly — Plan 2 passes a resolver backed by `IHabitNodeService`/the tree.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: FAIL / does not compile — methods/const don't exist.

- [ ] **Step 3: Implement persistence**

In `WorkspaceViewModel.cs` add the const near the other constants:

```csharp
    internal const string StateSettingKey = "ui.workspace";
```

and the two methods:

```csharp
    /// <summary>Persist the light session state (pinned tabs + active + sidebar) to <c>ui.workspace</c>.</summary>
    public async Task SaveStateAsync(CancellationToken ct = default)
    {
        var state = new WorkspaceState
        {
            SidebarCollapsed = SidebarCollapsed,
            SidebarWidth = SidebarWidth,
            ActiveTabId = ActiveSubjectTab?.SubjectId.ToString() ?? WorkspaceState.PanoramicaId,
            Tabs = Tabs.Where(t => !t.IsPreview)
                       .Select(t => new WorkspaceTabState(t.SubjectId, t.Kind))
                       .ToList(),
        };
        await _appSettings.SetAsync(StateSettingKey, state.ToJson(), ct).ConfigureAwait(true);
    }

    /// <summary>Reopen pinned tabs + active tab + sidebar from <c>ui.workspace</c>. <paramref name="titleResolver"/>
    /// supplies a fresh title for each persisted subject (the workspace doesn't depend on the node service).
    /// A stale legacy <c>ui.open_columns</c> value is never parsed.</summary>
    public async Task RestoreStateAsync(Func<Guid, WorkspaceTabKind, string> titleResolver, CancellationToken ct = default)
    {
        var state = WorkspaceState.FromJson(await _appSettings.GetAsync(StateSettingKey, ct).ConfigureAwait(true));
        if (state is null) return;

        SidebarCollapsed = state.SidebarCollapsed;
        if (state.SidebarWidth > 0) SidebarWidth = state.SidebarWidth;

        foreach (var t in state.Tabs)
            await OpenSubjectAsync(t.SubjectId, t.Kind, titleResolver(t.SubjectId, t.Kind), pin: true, ct).ConfigureAwait(true);

        if (state.ActiveTabId == WorkspaceState.PanoramicaId || !Guid.TryParse(state.ActiveTabId, out var activeId))
            ActivatePanoramica();
        else
            ActiveSubjectTab = Tabs.FirstOrDefault(t => t.SubjectId == activeId);
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceViewModelTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Workspace/WorkspaceViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Workspace/WorkspaceViewModelTests.cs
git commit -m "feat(wpf): WorkspaceViewModel session persistence (ui.workspace, ignores legacy key)"
```

---

### Task 8: DI registration — per-tab screen factory + WorkspaceViewModel

**Files:**
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`
- Create: `tests/Recordum.WPF.Tests/Bootstrap/WorkspaceRegistrationTests.cs`

**Interfaces:**
- Consumes: `WorkspaceViewModel` ctor (Task 3).
- Produces: DI registrations: `Func<HabitScreenViewModel>` factory + `AddTransient<WorkspaceViewModel>()`.

- [ ] **Step 1: Write the failing test**

Create `tests/Recordum.WPF.Tests/Bootstrap/WorkspaceRegistrationTests.cs`. First check how the existing registration tests build the provider — search the test project for an existing `ServiceRegistrar`/`ServiceProvider` test and mirror its setup. If none exists, use this minimal resolve check that mirrors `ServiceRegistrar.Register`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Workspace;
using Xunit;

namespace Recordum.WPF.Tests.Bootstrap;

public class WorkspaceRegistrationTests
{
    [Fact]
    public void Screen_factory_resolves_distinct_instances()
    {
        var services = new ServiceCollection();
        Recordum.WPF.Bootstrap.ServiceRegistrar.Register(services); // adjust if the method name/signature differs
        using var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<Func<HabitScreenViewModel>>();
        var a = factory();
        var b = factory();

        a.Should().NotBeNull();
        b.Should().NotBeSameAs(a);
    }

    [Fact]
    public void WorkspaceViewModel_resolves()
    {
        var services = new ServiceCollection();
        Recordum.WPF.Bootstrap.ServiceRegistrar.Register(services);
        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<WorkspaceViewModel>().Should().NotBeNull();
    }
}
```

**Before running:** open `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` and confirm the exact public entry point used to register services (e.g. `ServiceRegistrar.Register(IServiceCollection)` vs an extension method). Adjust the two `ServiceRegistrar.Register(services)` calls to match. If registration requires extra arguments (options, paths), construct them as the app's composition root does (see `App.xaml.cs`).

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceRegistrationTests`
Expected: FAIL — `Func<HabitScreenViewModel>` and `WorkspaceViewModel` are not registered.

- [ ] **Step 3: Add the registrations**

In `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`, next to `services.AddTransient<HabitScreenViewModel>();` (line ~149), add:

```csharp
        // Each workspace tab needs its own HabitScreenViewModel; expose a factory delegate
        // (MS.DI does not auto-provide Func<T>).
        services.AddTransient<Func<HabitScreenViewModel>>(sp => () => sp.GetRequiredService<HabitScreenViewModel>());
        services.AddTransient<WorkspaceViewModel>();
```

Add `using Recordum.WPF.ViewModels.Workspace;` to the file's usings if not present.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter FullyQualifiedName~WorkspaceRegistrationTests`
Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs tests/Recordum.WPF.Tests/Bootstrap/WorkspaceRegistrationTests.cs
git commit -m "feat(wpf): register per-tab HabitScreenViewModel factory + WorkspaceViewModel"
```

---

### Task 9: Panoramica tab title string + full-suite green

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx`
- Modify: `src/Recordum.WPF/Resources/Strings.it.resx`
- Modify: `src/Recordum.WPF/Resources/Strings.Designer.cs` (hand-maintained accessor — see note)

**Interfaces:**
- Produces: `Strings.Workspace_Tab_Panoramica` (used by Plan 2's tab bar).

> **Note (project quirk):** `Strings.Designer.cs` is NOT EF/tool-generated — it is a hand-maintained accessor (`public static string X => Get("...")`). A guardrail hook blocks `Edit`/`Write` on `*.Designer.cs` by suffix (false positive); add the accessor line via a shell command (`awk`/`sed`) instead. The `.resx` `<data>` entries are added normally.

- [ ] **Step 1: Add the resx entries**

In `src/Recordum.WPF/Resources/Strings.resx`, add near the other `Workspace.*`/top-level entries:

```xml
  <data name="Workspace.Tab.Panoramica" xml:space="preserve">
    <value>Overview</value>
  </data>
```

In `src/Recordum.WPF/Resources/Strings.it.resx`:

```xml
  <data name="Workspace.Tab.Panoramica" xml:space="preserve">
    <value>Panoramica</value>
  </data>
```

- [ ] **Step 2: Add the accessor via shell (guardrail-safe)**

Run (adjust the anchor line if needed — pick any existing accessor line to insert after):

```bash
awk '/public static string Notes_Category_Quick => Get\("Notes.Category.Quick"\);/{print; print "    public static string Workspace_Tab_Panoramica => Get(\"Workspace.Tab.Panoramica\");"; next}1' \
  src/Recordum.WPF/Resources/Strings.Designer.cs > /tmp/sd.cs && mv /tmp/sd.cs src/Recordum.WPF/Resources/Strings.Designer.cs
grep -n "Workspace_Tab_Panoramica" src/Recordum.WPF/Resources/Strings.Designer.cs
```

Expected: the grep prints the new accessor line.

- [ ] **Step 3: Build**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: Build succeeded, 0 errors. `Strings.Workspace_Tab_Panoramica` resolves.

- [ ] **Step 4: Run the full WPF + Data suites**

Run: `dotnet test tests/Recordum.WPF.Tests` then `dotnet test tests/Recordum.Data.Tests`
Expected: all green. WPF ≥ 576 + the new Workspace tests; Data still 343.

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "feat(wpf): Panoramica workspace-tab title string (EN+IT)"
```

---

## Self-Review

**Spec coverage (Plan 1 portion of SP1 spec):**
- §3.2 tab-bar state (open tabs, active, reorder) → Tasks 3–5. ✓
- §3.3 preview + pin + promote-on-edit + open-already-open-activates → Tasks 4, 6. ✓
- §6 light persistence (pinned tabs + active + sidebar; preview & per-tab layout NOT persisted; ignore legacy `ui.open_columns`) → Tasks 1, 7. ✓
- §4 `WorkspaceViewModel`/`SubjectTabViewModel` + per-tab `HabitScreenViewModel` factory → Tasks 2, 3, 8. ✓
- Panoramica default tab → Tasks 3, 7, 9. ✓
- Deferred to Plan 2 (sidebar XAML, tree→OpenSubject wiring, MainWindow restructure) and Plan 3 (per-tab panes, Diario pane, focus) — explicitly out of this plan. ✓

**Placeholder scan:** Task 8 contains a deliberate "confirm the exact registration entry point" verification instruction (the test must match `ServiceRegistrar`'s real signature) — this is a concrete verification with a named source, not a code placeholder. All code steps carry complete code. No TBD/TODO. ✓

**Type consistency:**
- `WorkspaceTabKind` / `WorkspaceTabState` / `WorkspaceState` identical across Tasks 1, 2, 7. ✓
- `SubjectTabViewModel(Guid, WorkspaceTabKind, string, HabitScreenViewModel, bool)` consistent across Task 2 def and Task 4 construction. ✓
- `OpenSubjectAsync(Guid, WorkspaceTabKind, string, bool, CancellationToken)` consistent across Tasks 4, 5, 7. ✓
- `WorkspaceViewModel(Func<HabitScreenViewModel>, IAppSettingService)` consistent across Tasks 3, 7, 8. ✓
- `StateSettingKey = "ui.workspace"` defined Task 7, used in Task 7 tests. ✓

**Risks to watch during execution:**
- **`HabitScreenViewModelFactory.Create()` signature:** Task 2/3 fixtures assume `Create()` returns `(HabitScreenViewModel Vm, FakeHabitNodeService Nodes)` (confirmed in the current fixture). If the fixture's tuple names differ, adjust the destructuring.
- **`HabitScreenViewModel.LoadAsync(Guid?)`:** Task 4 calls `screen.LoadAsync(subjectId, ct)`. Confirm the existing signature accepts `(Guid?, CancellationToken)`; the current method is `LoadAsync(Guid? habitNodeId, CancellationToken ct = default)`.
- **`ServiceRegistrar` entry point:** Task 8 must match the real registration method name/signature (see Task 8 Step 1 note).
- **Each `OpenSubjectAsync` builds a real `HabitScreenViewModel`** in tests via the all-fakes factory; this is heavier than a pure unit but keeps the seam real. Acceptable for the tab-lifecycle tests; if test time balloons, a future refactor can hide the screen behind an interface.
