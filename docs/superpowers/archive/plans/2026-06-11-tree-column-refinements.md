# Rifiniture albero/colonne (pacchetto B) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scroll verticale dell'albero, collasso gruppi funzionante, click-a-vuoto sul ribbon, workspace che si chiude quando l'ultima colonna viene chiusa, e riapertura sull'ultima colonna chiusa.

**Architecture:** Collasso = `HashSet<Guid>` dei gruppi collassati in `HomeViewModel`, consultato da `WalkSubtree`, aggiornato da un evento `ExpandToggled` per-riga (guard anti-ricorsione durante `Rebuild`). Ultima-colonna-chiusa = campo in `HabitScreenViewModel` persistito in `ui.last_closed_column`, usato come default in `LoadAsync`; set vuoto → evento `AllColumnsClosed` → `MainWindowViewModel` azzera la selezione. Scroll e click-ribbon sono view-only.

**Tech Stack:** WPF + CommunityToolkit.Mvvm; xUnit/FluentAssertions.

**Spec:** `docs/superpowers/archive/specs/2026-06-11-group-view-tree-refinements-design.md` §B

---

## Mappa file

| Azione | Path | Responsabilità |
|---|---|---|
| Modify | `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs` | evento `ExpandToggled` |
| Modify | `src/Recordum.WPF/ViewModels/HomeViewModel.cs` | set collassati + `WalkSubtree` skip + guard |
| Modify | `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` | `_lastClosedColumn` + persistenza + `AllColumnsClosed` + default `LoadAsync` |
| Modify | `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs` | `AllColumnsClosed` → deselezione |
| Modify | `src/Recordum.WPF/Views/HomeView.xaml` | ScrollViewer attorno all'ItemsControl |
| Modify | `src/Recordum.WPF/Views/WorkspaceRibbon.xaml(.cs)` | click-a-vuoto deseleziona |
| Test | `HomeViewModelTests` + `HabitScreenColumnsTests` + `MainWindowViewModelTests` (append) | §B.2/B.4/B.5 |

**Convenzioni**: build 0 warning nuovi; flaky noto `CompletionRecorded_event_reloads…` ammesso se verde isolato; commit per task.

---

### Task 1: collasso gruppi (B.2, TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HomeViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HomeViewModelTests.cs` (append)

- [x] **Step 1: test (RED)** — append a `HomeViewModelTests` (riusa `SampleRow`, `CreateSut`):

```csharp
    [Fact]
    public async Task Collapsing_a_group_hides_descendants_and_survives_reload()
    {
        var groupId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        _query.HomeRows.Add(SampleRow(name: "child", parent: groupId));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.Rows.Should().HaveCount(2);

        sut.Rows.Single(r => r.IsGroup).IsExpanded = false;   // toggle → rebuild

        sut.Rows.Should().ContainSingle(r => r.HabitNodeId == groupId);

        await sut.LoadCommand.ExecuteAsync(null);             // lo stato sopravvive al reload
        sut.Rows.Should().ContainSingle(r => r.HabitNodeId == groupId);
        sut.Rows.Single().IsExpanded.Should().BeFalse();

        sut.Rows.Single(r => r.IsGroup).IsExpanded = true;    // re-espansione
        sut.Rows.Should().HaveCount(2);
    }
```

- [x] **Step 2: RED** — `dotnet test tests/Recordum.WPF.Tests --filter Collapsing_a_group` → FAIL (oggi il toggle è inerte: Rows resta 2).

- [x] **Step 3: implementare**

`HomeRowViewModel` — accanto all'evento `Changed` (riga ~23):

```csharp
    /// <summary>Raised when the ▾/▸ toggle changes (HomeViewModel rebuilds the visible rows).</summary>
    public event EventHandler? ExpandToggled;

    partial void OnIsExpandedChanged(bool value) => ExpandToggled?.Invoke(this, EventArgs.Empty);
```

`HomeViewModel` — campi accanto a `_suppressRebuild`:

```csharp
    private readonly HashSet<Guid> _collapsedGroupIds = [];
    private bool _rebuilding;
```

In `Rebuild()` (riga ~239): marcare l'inizio/fine del loop di costruzione e inizializzare/sottoscrivere:

```csharp
        _rebuilding = true;
        try
        {
            foreach (var (row, level) in OrderHierarchically(q.ToList()))
            {
                var vm = new HomeRowViewModel(row, level, _nodes, _purge)
                {
                    CurrentTab = ActiveTab,
                    IsExpanded = !_collapsedGroupIds.Contains(row.HabitNodeId),
                };
                if (_quickActions is not null) vm.AttachQuickActionSelector(_quickActions);
                vm.Changed += OnRowChanged;
                vm.QuickActionExecuted += OnRowQuickActionExecuted;
                vm.ExpandToggled += OnRowExpandToggled;
                Rows.Add(vm);
            }
        }
        finally { _rebuilding = false; }
```

(il `Rows.Clear()` esistente resta prima del blocco; le righe `State = …` e il sync `IsSelected` restano dopo).

Nuovo handler:

```csharp
    // ▾/▸ toggle: track the collapsed set and rebuild the visible rows. The guard skips
    // the notifications fired by the IsExpanded initializer while Rebuild constructs rows.
    private void OnRowExpandToggled(object? sender, EventArgs e)
    {
        if (_rebuilding || sender is not HomeRowViewModel row || !row.IsGroup) return;
        if (row.IsExpanded) _collapsedGroupIds.Remove(row.HabitNodeId);
        else _collapsedGroupIds.Add(row.HabitNodeId);
        Rebuild();
    }
```

In `WalkSubtree` (riga ~270), dopo `yield return (node, level);`:

```csharp
        if (_collapsedGroupIds.Contains(node.HabitNodeId)) yield break;
```

- [x] **Step 4: GREEN** — filtro `Collapsing_a_group` + suite `HomeViewModel` completa verde.

- [x] **Step 5: commit** `feat(wpf): group collapse hides descendants (session-scoped state)`.

---

### Task 2: ultima colonna chiusa + workspace che si chiude (B.4 + B.5, TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenColumnsTests.cs` + `tests/Recordum.WPF.Tests/ViewModels/MainWindowViewModelTests.cs` (append)

- [x] **Step 1: test (RED)** — append a `HabitScreenColumnsTests`:

```csharp
    [Fact]
    public async Task Reopen_defaults_to_the_last_closed_column()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out var settings);
        var a = AddHabit(nodes);
        var b = AddHabit(nodes);
        await sut.LoadAsync(a);          // Cronologia (default)
        sut.IsDettaglioOpen = true;
        sut.IsCronologiaOpen = false;    // last closed = Cronologia
        sut.IsDettaglioOpen = false;     // last closed = Dettaglio, set vuoto

        (await settings.GetAsync(HabitScreenViewModel.LastClosedColumnSettingKey)).Should().Be("Dettaglio");

        await sut.LoadAsync(b);          // riapertura: usa l'ultima chiusa, non Cronologia
        sut.IsDettaglioOpen.Should().BeTrue();
        sut.IsCronologiaOpen.Should().BeFalse();
    }

    [Fact]
    public async Task AllColumnsClosed_raised_when_the_open_set_empties()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);         // Cronologia
        var raised = 0;
        sut.AllColumnsClosed += (_, _) => raised++;

        sut.IsCronologiaOpen = false;

        raised.Should().Be(1);
    }

    [Fact]
    public async Task Restore_reads_the_persisted_last_closed_column()
    {
        var (_, _) = HabitScreenViewModelFactory.CreateWithSettings(out var settings);
        await settings.SetAsync(HabitScreenViewModel.LastClosedColumnSettingKey, "Note");

        var (sut, nodes) = HabitScreenViewModelFactory.CreateOver(settings);
        await sut.RestoreOpenColumnsAsync();
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);

        sut.IsNoteOpen.Should().BeTrue();
        sut.IsCronologiaOpen.Should().BeFalse();
    }
```

e a `MainWindowViewModelTests` (riusa `SampleRow` e `CreateSut` del file):

```csharp
    [Fact]
    public async Task Closing_the_last_open_column_deselects_the_row()
    {
        var (sut, parts) = CreateSut();
        var id = Guid.NewGuid();
        parts.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "a", Color = "#808080" };
        parts.Query.HomeRows.Add(SampleRow(id, "a"));
        await sut.InitializeAsync();
        sut.Home.SelectedRow = sut.Home.Rows.Single();
        await Task.Delay(30);            // Cronologia aperta di default

        sut.HabitScreen.IsCronologiaOpen = false;
        await Task.Delay(30);

        sut.Home.SelectedRow.Should().BeNull();
    }
```

- [x] **Step 2: RED** — `dotnet test tests/Recordum.WPF.Tests --filter "Reopen_defaults|AllColumnsClosed_raised|Restore_reads_the_persisted|Closing_the_last_open_column"` (errori di compilazione attesi: `LastClosedColumnSettingKey`, `AllColumnsClosed`).

- [x] **Step 3: implementare** in `HabitScreenViewModel`:

Costante + campo + evento, nella regione column-workspace (sotto `OpenColumnsSettingKey`):

```csharp
    internal const string LastClosedColumnSettingKey = "ui.last_closed_column";
    private HabitColumn _lastClosedColumn = HabitColumn.Cronologia;

    /// <summary>Raised when the user closes the last open view column (host deselects, spec §B.4).</summary>
    public event EventHandler? AllColumnsClosed;
```

`OnColumnFlag` diventa:

```csharp
    private void OnColumnFlag(HabitColumn column, bool open)
    {
        if (open) { if (!_openOrder.Contains(column)) _openOrder.Add(column); }
        else _openOrder.Remove(column);
        if (_restoringColumns) return;
        _ = PersistOpenColumnsAsync();
        if (!open)
        {
            _lastClosedColumn = column;
            _ = _appSettings.SetAsync(LastClosedColumnSettingKey, column.ToString()).AsTask();
            if (Node is not null && !CanonicalOpen().Any())
                AllColumnsClosed?.Invoke(this, EventArgs.Empty);
        }
    }
```

In `LoadAsync`, il default (riga ~301) diventa:

```csharp
        // Opening a habit with no view columns open: default to the last closed column (§B.5).
        if (!CanonicalOpen().Any())
            SetColumnFlag(_lastClosedColumn, true);
```

In `RestoreOpenColumnsAsync`, dopo la chiamata a `RestoreColumnWidthsAsync`:

```csharp
        var lastClosed = await _appSettings.GetAsync(LastClosedColumnSettingKey, ct).ConfigureAwait(true);
        if (Enum.TryParse<HabitColumn>(lastClosed, out var lastClosedColumn))
            _lastClosedColumn = lastClosedColumn;
```

In `MainWindowViewModel`, nel ctor dopo `HabitScreen.ConfigureRequested += …`:

```csharp
        HabitScreen.AllColumnsClosed += OnAllColumnsClosed;
```

e accanto a `OnHomeGroupCreated`:

```csharp
    // Closing the last open column collapses the workspace back to a clean Home (§B.4).
    private void OnAllColumnsClosed(object? sender, EventArgs e)
        => Home.SelectedRow = null;
```

- [x] **Step 4: GREEN** — filtro dello step 2 + suite `HabitScreenColumns` e `MainWindowViewModel` complete verdi.

- [x] **Step 5: commit** `feat(wpf): empty column set closes the workspace; reopen on last closed column`.

---

### Task 3: scroll albero + click-a-vuoto sul ribbon (B.1 + B.3, view-only)

**Files:**
- Modify: `src/Recordum.WPF/Views/HomeView.xaml`
- Modify: `src/Recordum.WPF/Views/WorkspaceRibbon.xaml`, `src/Recordum.WPF/Views/WorkspaceRibbon.xaml.cs`

- [x] **Step 1: scroll** — in `HomeView.xaml` (DataTrigger `State=Loaded`, riga ~169) avvolgere l'ItemsControl:

```xml
                                    <DataTemplate>
                                        <ScrollViewer VerticalScrollBarVisibility="Auto">
                                            <ItemsControl ItemsSource="{Binding Rows}"
                                                          b:TreeDragDropBehavior.Enabled="True"
                                                          KeyboardNavigation.TabNavigation="Continue">
                                                <ItemsControl.ItemTemplateSelector>
                                                    <v:RowTemplateSelector
                                                        HabitTemplate="{StaticResource HabitRowTemplate}"
                                                        GroupTemplate="{StaticResource GroupRowTemplate}"/>
                                                </ItemsControl.ItemTemplateSelector>
                                            </ItemsControl>
                                        </ScrollViewer>
                                    </DataTemplate>
```

- [x] **Step 2: ribbon** — in `WorkspaceRibbon.xaml` sul Border radice (riga ~41) aggiungere `MouseLeftButtonUp="OnRibbonEmptyClick"`; in `WorkspaceRibbon.xaml.cs`:

```csharp
    // Clicking empty rail space deselects (spec §B.3): with a full tree there is no empty
    // Home space left to click. Button clicks never reach here (ButtonBase handles the event).
    private void OnRibbonEmptyClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if (DataContext is Recordum.WPF.ViewModels.MainWindowViewModel { Home.SelectedRow: not null } vm)
            vm.Home.SelectedRow = null;
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase) return true;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return false;
    }
```

- [x] **Step 3**: build 0/0 + suite WPF completa verde (flaky noto ammesso se verde isolato).

- [x] **Step 4: commit** `feat(wpf): tree vertical scroll + ribbon empty-click deselect`.

---

### Task 4: verifica visiva + tracker

- [ ] skill `run` + checklist: albero con molte habit scrolla (rotella + scrollbar flat); ▾/▸ collassa/espande e lo stato regge al reload (es. dopo un completamento); click su spazio vuoto del ribbon deseleziona; chiudere l'ultima colonna (chip/✕/Esc) collassa il workspace; riselezionare una habit riapre l'ultima colonna chiusa anche dopo riavvio; due temi.
- [x] aggiornare `docs/phase-4-issues.md` + memoria (pacchetto B fatto; prossimo piano 2 = Panoramica); commit docs.

---

## Self-review

- Spec B.1→T3; B.2→T1; B.3→T3; B.4→T2 (evento + deselezione); B.5→T2 (campo+persistenza+default+restore). Coverage completa.
- Recursione evitata: l'initializer `IsExpanded` in Rebuild fa scattare `ExpandToggled` → guard `_rebuilding`.
- `SetColumnFlag(_lastClosedColumn, true)` in LoadAsync: il default era `IsCronologiaOpen = true` e `_lastClosedColumn` parte da Cronologia → comportamento invariato finché l'utente non chiude qualcosa.
- Tipi coerenti: `LastClosedColumnSettingKey`, `AllColumnsClosed` (EventHandler), `ExpandToggled` (EventHandler), `_collapsedGroupIds`.
- Nota A: il default per i gruppi (Panoramica) arriva col piano 2 — qui i gruppi seguono la stessa logica delle habit (accettabile come stato intermedio).
