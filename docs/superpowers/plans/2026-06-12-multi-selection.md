# Selezione multipla stile file-manager (pacchetto C) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ctrl+clic/Shift+clic selezionano più righe dell'albero (tutte e tre le tab); con 2+ selezionate il workspace si chiude e menu contestuale/tasti/drag agiscono in blocco (archivia/elimina/ripristina/svuota/sposta).

**Architecture:** Il set di selezione (`HashSet<Guid>`) vive in `HomeViewModel` con ancora per lo Shift; `SelectedRow` resta l'API verso il workspace (valorizzata solo con selezione singola, guard anti-rientro). Le azioni bulk passano da un hook nelle righe: `HomeRowViewModel` chiede al `IRowSelectionHost` (implementato da `HomeViewModel`) di gestire l'azione come bulk quando la riga fa parte di una multi-selezione — così menu contestuale e scorciatoie esistenti funzionano senza duplicare XAML. Il drop multiplo passa da `TryBulkMoveAsync` nel behavior.

**Tech Stack:** WPF + CommunityToolkit.Mvvm; xUnit/FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-06-11-group-view-tree-refinements-design.md` §C

---

## Mappa file

| Azione | Path | Responsabilità |
|---|---|---|
| Create | `src/Recordum.WPF/ViewModels/IRowSelectionHost.cs` | interfaccia hook bulk + enum azioni |
| Modify | `src/Recordum.WPF/ViewModels/HomeViewModel.cs` | set selezione, `SelectInteractive`, `ClearSelection`, bulk ops, reload che preserva il set |
| Modify | `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs` | hook `AttachSelectionHost` + deviazione bulk nei command |
| Modify | `src/Recordum.WPF/Views/HomeView.xaml.cs` | Ctrl/Shift al clic + right-click collapse |
| Modify | `src/Recordum.WPF/Views/HomeView.xaml` | handler right-click sul TreeRegion |
| Modify | `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` | drop multiplo |
| Modify | `src/Recordum.WPF/MainWindow.xaml.cs` | Esc azzera la multi-selezione |
| Test | `tests/Recordum.WPF.Tests/ViewModels/HomeMultiSelectionTests.cs` (new) | §C.1–C.3 |

**Convenzioni**: build 0 warning nuovi; flaky noto ammesso se verde isolato; commit per task.

---

### Task 1: modello di selezione in `HomeViewModel` (TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HomeViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HomeMultiSelectionTests.cs` (new)

- [ ] **Step 1: test (RED)**

```csharp
using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class HomeMultiSelectionTests
{
    private readonly FakeHabitTreeQueryService _query = new();
    private readonly FakeHabitNodeService _nodes = new();
    private readonly FakePurgeService _purge = new();

    private HomeViewModel CreateSut() => new(_query, _nodes, _purge);

    private static HomeTreeRow SampleRow(string name, Guid? id = null, Guid? parent = null, HabitType? type = HabitType.Action) =>
        new(id ?? Guid.NewGuid(), parent, name, "#000000", type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    private async Task<HomeViewModel> LoadedSutAsync(params string[] names)
    {
        foreach (var n in names) _query.HomeRows.Add(SampleRow(n));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        return sut;
    }

    [Fact]
    public async Task Plain_click_single_selects()
    {
        var sut = await LoadedSutAsync("a", "b");

        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);

        sut.SelectedRow.Should().Be(sut.Rows[0]);
        sut.Rows[0].IsSelected.Should().BeTrue();
        sut.Rows[1].IsSelected.Should().BeFalse();
        sut.HasMultiSelection.Should().BeFalse();
    }

    [Fact]
    public async Task Ctrl_click_toggles_membership_and_nulls_SelectedRow_at_two()
    {
        var sut = await LoadedSutAsync("a", "b");
        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);

        sut.SelectInteractive(sut.Rows[1], ctrl: true, shift: false);

        sut.HasMultiSelection.Should().BeTrue();
        sut.SelectedRow.Should().BeNull();           // 2+ → workspace chiuso (§C.2)
        sut.Rows[0].IsSelected.Should().BeTrue();
        sut.Rows[1].IsSelected.Should().BeTrue();

        sut.SelectInteractive(sut.Rows[0], ctrl: true, shift: false);   // toggle off
        sut.HasMultiSelection.Should().BeFalse();
        sut.SelectedRow.Should().Be(sut.Rows[1]);    // tornati a 1 → workspace riapre
    }

    [Fact]
    public async Task Shift_click_selects_the_range_from_the_anchor()
    {
        var sut = await LoadedSutAsync("a", "b", "c", "d");
        sut.SelectInteractive(sut.Rows[1], ctrl: false, shift: false);  // anchor = b

        sut.SelectInteractive(sut.Rows[3], ctrl: false, shift: true);

        sut.Rows.Where(r => r.IsSelected).Should().BeEquivalentTo(new[] { sut.Rows[1], sut.Rows[2], sut.Rows[3] });
        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task ClearSelection_and_plain_SelectedRow_assignment_reset_the_set()
    {
        var sut = await LoadedSutAsync("a", "b");
        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows[1], ctrl: true, shift: false);

        sut.ClearSelection();
        sut.HasMultiSelection.Should().BeFalse();
        sut.Rows.Should().OnlyContain(r => !r.IsSelected);

        sut.SelectInteractive(sut.Rows[0], ctrl: true, shift: false);
        sut.SelectInteractive(sut.Rows[1], ctrl: true, shift: false);
        sut.SelectedRow = sut.Rows[0];               // assegnazione diretta = selezione singola
        sut.HasMultiSelection.Should().BeFalse();
        sut.Rows[1].IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task Reload_preserves_the_multi_selection_set()
    {
        var sut = await LoadedSutAsync("a", "b", "c");
        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows[2], ctrl: true, shift: false);

        await sut.LoadCommand.ExecuteAsync(null);    // reload ricrea i row VM

        sut.Rows.Count(r => r.IsSelected).Should().Be(2);
        sut.HasMultiSelection.Should().BeTrue();
    }
}
```

- [ ] **Step 2: RED** — `dotnet test tests/Recordum.WPF.Tests --filter HomeMultiSelection` (errori di compilazione: `SelectInteractive`, `HasMultiSelection`, `ClearSelection`).

- [ ] **Step 3: implementare** in `HomeViewModel`:

Campi (accanto a `_collapsedGroupIds`):

```csharp
    private readonly HashSet<Guid> _selectedIds = [];
    private Guid? _selectionAnchor;
    private bool _syncingSelection;
```

API (dopo `SelectById`):

```csharp
    public bool HasMultiSelection => _selectedIds.Count > 1;

    /// <summary>File-manager selection (spec §C.1): plain=single, Ctrl=toggle, Shift=range from anchor.</summary>
    public void SelectInteractive(HomeRowViewModel row, bool ctrl, bool shift)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (shift && _selectionAnchor is { } anchorId
                  && Rows.FirstOrDefault(r => r.HabitNodeId == anchorId) is { } anchorRow)
        {
            var from = Rows.IndexOf(anchorRow);
            var to = Rows.IndexOf(row);
            _selectedIds.Clear();
            foreach (var r in Rows.Skip(Math.Min(from, to)).Take(Math.Abs(to - from) + 1))
                _selectedIds.Add(r.HabitNodeId);
        }
        else if (ctrl)
        {
            if (!_selectedIds.Remove(row.HabitNodeId)) _selectedIds.Add(row.HabitNodeId);
            _selectionAnchor = row.HabitNodeId;
        }
        else
        {
            _selectedIds.Clear();
            _selectedIds.Add(row.HabitNodeId);
            _selectionAnchor = row.HabitNodeId;
        }
        ApplySelectionToRows();
        SyncSelectedRowFromSet();
    }

    /// <summary>Esc with 2+ selected, or tab switches: back to no selection (spec §C.1).</summary>
    public void ClearSelection()
    {
        _selectedIds.Clear();
        _selectionAnchor = null;
        ApplySelectionToRows();
        SyncSelectedRowFromSet();
    }

    private void ApplySelectionToRows()
    {
        foreach (var r in Rows) r.IsSelected = _selectedIds.Contains(r.HabitNodeId);
        OnPropertyChanged(nameof(HasMultiSelection));
    }

    // SelectedRow is the single-selection API the workspace tracks: non-null only when
    // exactly one row is selected. The guard stops the setter's partial from clobbering the set.
    private void SyncSelectedRowFromSet()
    {
        _syncingSelection = true;
        try
        {
            SelectedRow = _selectedIds.Count == 1
                ? Rows.FirstOrDefault(r => _selectedIds.Contains(r.HabitNodeId))
                : null;
        }
        finally { _syncingSelection = false; }
    }
```

`OnSelectedRowChanged` (sostituisce il corpo attuale che flippa i due `IsSelected`):

```csharp
    partial void OnSelectedRowChanged(HomeRowViewModel? oldValue, HomeRowViewModel? newValue)
    {
        if (!_syncingSelection)
        {
            // Direct assignment (tree click plumbing, SelectById, deselects) = single selection.
            _selectedIds.Clear();
            if (newValue is not null)
            {
                _selectedIds.Add(newValue.HabitNodeId);
                _selectionAnchor = newValue.HabitNodeId;
            }
        }
        ApplySelectionToRows();
    }
```

In `Rebuild()`, l'ultima riga `foreach (var r in Rows) r.IsSelected = ReferenceEquals(r, SelectedRow);` diventa:

```csharp
        ApplySelectionToRows();
```

`ReloadPreservingSelectionAsync` diventa set-aware:

```csharp
    private async Task ReloadPreservingSelectionAsync()
    {
        var keep = _selectedIds.ToList();
        await LoadAsync();
        _selectedIds.Clear();
        foreach (var id in keep.Where(id => Rows.Any(r => r.HabitNodeId == id)))
            _selectedIds.Add(id);
        ApplySelectionToRows();
        SyncSelectedRowFromSet();
    }
```

- [ ] **Step 4: GREEN** — filtro `HomeMultiSelection` + suite `HomeViewModel` completa (i test esistenti su `SelectedRow` devono restare verdi).

- [ ] **Step 5: commit** `feat(wpf): multi-selection model — plain/Ctrl/Shift with anchor`.

---

### Task 2: azioni bulk via hook di riga (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/IRowSelectionHost.cs`
- Modify: `src/Recordum.WPF/ViewModels/HomeViewModel.cs`, `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HomeMultiSelectionTests.cs` (append)

- [ ] **Step 1: test (RED)** — append:

```csharp
    [Fact]
    public async Task Row_archive_on_a_multi_selected_row_archives_all_selected()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow("a", a));
        _query.HomeRows.Add(SampleRow("b", b));
        _query.HomeRows.Add(SampleRow("c"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectInteractive(sut.Rows.Single(r => r.HabitNodeId == a), ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows.Single(r => r.HabitNodeId == b), ctrl: true, shift: false);
        var reloadsBefore = _query.HomeCallCount;

        await sut.Rows.Single(r => r.HabitNodeId == a).ArchiveCommand.ExecuteAsync(null);

        _nodes.ArchivedCalls.Should().BeEquivalentTo(new[] { (a, true), (b, true) });
        _query.HomeCallCount.Should().Be(reloadsBefore + 1);   // un solo reload
        sut.HasMultiSelection.Should().BeFalse();              // selezione azzerata dopo il bulk
    }

    [Fact]
    public async Task Row_archive_on_a_row_outside_the_selection_stays_single()
    {
        var a = Guid.NewGuid(); var c = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow("a", a));
        _query.HomeRows.Add(SampleRow("b"));
        _query.HomeRows.Add(SampleRow("c", c));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows[1], ctrl: true, shift: false);

        await sut.Rows.Single(r => r.HabitNodeId == c).ArchiveCommand.ExecuteAsync(null);

        _nodes.ArchivedCalls.Should().BeEquivalentTo(new[] { (c, true) });
    }

    [Fact]
    public async Task Bulk_move_moves_every_selected_row_and_skips_cycles()
    {
        var g = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow("G", g, type: null));
        _query.HomeRows.Add(SampleRow("a", a));
        _query.HomeRows.Add(SampleRow("b", b));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var rowA = sut.Rows.Single(r => r.HabitNodeId == a);
        sut.SelectInteractive(rowA, ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows.Single(r => r.HabitNodeId == b), ctrl: true, shift: false);

        var handled = await sut.TryBulkMoveAsync(rowA, g);

        handled.Should().BeTrue();
        _nodes.MoveCalls.Should().BeEquivalentTo(new[] { (a, (Guid?)g), (b, (Guid?)g) });
    }
```

NB: verificare che `FakeHabitNodeService` esponga `ArchivedCalls`/`MoveCalls`; se mancano, aggiungerli al fake:

```csharp
    public List<(Guid Id, bool Archived)> ArchivedCalls { get; } = [];
    public List<(Guid Id, Guid? Parent)> MoveCalls { get; } = [];
```

registrando in `SetArchivedAsync` → `ArchivedCalls.Add((id, archived));` e in `MoveAsync` → `MoveCalls.Add((id, newParentId));` (PRIMA degli hook esistenti, senza rimuoverli).

- [ ] **Step 2: RED** — filtro `"Row_archive_on|Bulk_move_moves"`.

- [ ] **Step 3: implementare**

`IRowSelectionHost.cs` (nuovo):

```csharp
namespace Recordum.WPF.ViewModels;

/// <summary>Bulk actions over the multi-selection (spec §C.3). A row asks its host to handle
/// an action as bulk; the host answers false when the row isn't part of a multi-selection,
/// and the row falls back to its single-node path.</summary>
public interface IRowSelectionHost
{
    Task<bool> TryBulkAsync(HomeRowViewModel origin, RowBulkAction action);
}

public enum RowBulkAction { Archive, Unarchive, SoftDelete, Restore, Purge }
```

`HomeViewModel` — implementa l'interfaccia (`public sealed partial class HomeViewModel : ObservableObject, IRowSelectionHost`):

```csharp
    public async Task<bool> TryBulkAsync(HomeRowViewModel origin, RowBulkAction action)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!HasMultiSelection || !origin.IsSelected) return false;
        var ids = Rows.Where(r => r.IsSelected).Select(r => r.HabitNodeId).ToList();
        foreach (var id in ids)
        {
            switch (action)
            {
                case RowBulkAction.Archive: await _nodes.SetArchivedAsync(id, true); break;
                case RowBulkAction.Unarchive: await _nodes.SetArchivedAsync(id, false); break;
                case RowBulkAction.SoftDelete: await _nodes.SoftDeleteAsync(id); break;
                case RowBulkAction.Restore: await _nodes.RestoreAsync(id); break;
                case RowBulkAction.Purge: await _purge.PurgeSubtreeAsync(id); break;
            }
        }
        ClearSelection();
        await LoadAsync();
        return true;
    }

    /// <summary>Bulk move from drag-drop: moves every selected row, skipping cyclic targets (spec §C.3).</summary>
    public async Task<bool> TryBulkMoveAsync(HomeRowViewModel origin, Guid? newParentId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!HasMultiSelection || !origin.IsSelected) return false;
        var ids = Rows.Where(r => r.IsSelected).Select(r => r.HabitNodeId).ToList();
        foreach (var id in ids)
        {
            try { await _nodes.MoveAsync(id, newParentId); }
            catch (CyclicHabitNodeMoveException) { /* skip, like the single-row path */ }
        }
        ClearSelection();
        await LoadAsync();
        return true;
    }
```

(using nuovo: `Recordum.Core.Errors` per `CyclicHabitNodeMoveException`.)

In `Rebuild()`, dopo `vm.ExpandToggled += OnRowExpandToggled;`:

```csharp
                vm.AttachSelectionHost(this);
```

`HomeRowViewModel` — campo + attach + deviazione bulk nei 5 command:

```csharp
    private IRowSelectionHost? _selectionHost;
    public void AttachSelectionHost(IRowSelectionHost host) => _selectionHost = host;

    private async Task<bool> HandledAsBulkAsync(RowBulkAction action)
        => _selectionHost is not null && await _selectionHost.TryBulkAsync(this, action);
```

e in testa a ciascun command body (`ArchiveAsync`, `UnarchiveAsync`, `SoftDeleteAsync`, `RestoreAsync`, `PurgeAsync`):

```csharp
        if (await HandledAsBulkAsync(RowBulkAction.Archive)) return;   // (azione corrispondente)
```

- [ ] **Step 4: GREEN** — filtro step 2 + suite `HomeViewModel|HomeMultiSelection` completa.

- [ ] **Step 5: commit** `feat(wpf): bulk archive/delete/restore/purge/move over the multi-selection`.

---

### Task 3: view — Ctrl/Shift al clic, right-click collapse, drop multiplo, Esc

**Files:**
- Modify: `src/Recordum.WPF/Views/HomeView.xaml.cs`, `src/Recordum.WPF/Views/HomeView.xaml`
- Modify: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs`
- Modify: `src/Recordum.WPF/MainWindow.xaml.cs`

- [ ] **Step 1: clic con modificatori** — in `HomeView.xaml.cs`, `OnPreviewMouseLeftButtonUp`, il ramo `if (row is not null)` diventa:

```csharp
        if (row is not null)
        {
            var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if ((ctrl || shift) && DataContext is HomeViewModel vm)
                vm.SelectInteractive(row, ctrl, shift);
            else
                home.SelectRowCommand.Execute(row);
            return;
        }
```

- [ ] **Step 2: right-click collapse (§C.3)** — sul `Border` `TreeRegion` in `HomeView.xaml` (riga ~126) aggiungere `PreviewMouseRightButtonDown="OnPreviewMouseRightButtonDown"`; handler nel code-behind:

```csharp
    // Right-click on a row OUTSIDE the selection collapses the selection to that row, like
    // file managers, so the context menu's bulk routing matches what the user sees.
    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not HomeViewModel home) return;
        var row = FindAncestorDataContext<HomeRowViewModel>(e.OriginalSource as DependencyObject);
        if (row is { IsSelected: false })
            home.SelectRowCommand.Execute(row);
    }
```

- [ ] **Step 3: drop multiplo** — in `TreeDragDropBehavior.OnDrop`, subito PRIMA di `try { await src.MoveToAsync(newParent); }`:

```csharp
        if (ic.DataContext is HomeViewModel homeVm && await homeVm.TryBulkMoveAsync(src, newParent))
            return;   // bulk path reloads by itself
```

- [ ] **Step 4: Esc** — in `MainWindow.xaml.cs`, `OnWindowPreviewKeyDown`, PRIMA del blocco Esc esistente:

```csharp
        // Esc with a multi-selection: clear it (spec §C.1) before any column handling.
        if (e.Key == Key.Escape && _viewModel.Home.HasMultiSelection)
        {
            _viewModel.Home.ClearSelection();
            e.Handled = true;
            return;
        }
```

- [ ] **Step 5**: build 0/0 + suite WPF completa verde.

- [ ] **Step 6: commit** `feat(wpf): multi-selection UI — modifier clicks, bulk context routing, multi-drop, Esc`.

---

### Task 4: verifica visiva + tracker

- [ ] skill `run` + checklist (tutte e tre le tab): Ctrl+clic toggla e le righe si evidenziano; Shift+clic seleziona l'intervallo; con 2+ il workspace si chiude; menu contestuale su riga selezionata archivia/elimina TUTTE; su riga fuori selezione collassa a quella; Canc/Ctrl+E bulk; drag di una selezionata trascina tutte in un gruppo (highlight invariato); Esc azzera; cambio tab azzera; due temi.
- [ ] aggiornare `docs/phase-4-issues.md` + memoria (pacchetto C fatto → spec combinata completata); commit docs.

---

## Self-review

- Spec C.1 (plain/Ctrl/Shift, 3 tab, Esc, cambio tab)→T1/T3 (tab switch già azzera via `SelectedRow = null` → partial); C.2 (1 vs 2+, SelectedRow null, ancora)→T1; C.3 (drag multiplo, menu bulk, right-click collapse, tasti, reload singolo)→T2/T3; C.4 fuori scope rispettato (no Ctrl+A/lasso/bulk pin).
- Coerenza tipi: `SelectInteractive(row, ctrl, shift)`, `HasMultiSelection`, `ClearSelection()`, `TryBulkAsync(origin, RowBulkAction)`, `TryBulkMoveAsync(origin, Guid?)`, `AttachSelectionHost(IRowSelectionHost)`.
- I tasti (Canc/Ctrl+E/Ctrl+R) passano dai command di riga → coperti dall'hook senza modifiche a `DispatchRowKey`.
- `SelectById`/`OpenNewHabit`/`CreateGroup` assegnano `SelectedRow` direttamente → il partial li tratta come selezione singola: coerente.
- Quick-action "+" su riga: usa `SelectRow`/attivazione, non toccato.
