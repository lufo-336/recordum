# Colonna Panoramica per i gruppi (pacchetto A) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un gruppo selezionato mostra chip `Panoramica · Note · Configura` con la nuova colonna Panoramica (conteggio oggi x/y, righe habit discendenti con sparkline, card Goal di gruppo, click-per-navigare); le colonne non applicabili al tipo nodo sono nascoste senza perdere i flag.

**Architecture:** `HabitColumn.Panoramica` (in coda all'enum — la CSV `ui.column_widths` guadagna un token). Applicabilità per tipo nodo centralizzata in `HabitScreenViewModel.IsColumnApplicable` e consumata da: proprietà `Show*` (visibilità chip/colonne), default di `LoadAsync` (gruppo→Panoramica, habit→B.5), evento `AllColumnsClosed` (conta solo le colonne applicabili). `PanoramicaViewModel` nuovo, dati da `IHabitTreeQueryService` (nuova dipendenza ctor di HabitScreenViewModel, +1) filtrati per discendenza; navigazione via evento `ChildSelected` → `HomeViewModel.SelectById` (espande gli antenati collassati).

**Tech Stack:** WPF + CommunityToolkit.Mvvm; xUnit/FluentAssertions; controllo `HeatmapStrip` esistente per le sparkline.

**Spec:** `docs/superpowers/archive/specs/2026-06-11-group-view-tree-refinements-design.md` §A

---

## Mappa file

| Azione | Path | Responsabilità |
|---|---|---|
| Modify | `src/Recordum.WPF/ViewModels/HabitScreen/HabitColumn.cs` | + `Panoramica` |
| Create | `src/Recordum.WPF/ViewModels/HabitScreen/PanoramicaViewModel.cs` | rollup gruppo |
| Modify | `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` | applicabilità + `Show*` + default + load gruppo + ctor +1 |
| Modify | `src/Recordum.WPF/ViewModels/HomeViewModel.cs` | `SelectById` (espande antenati) |
| Modify | `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs` | `ChildSelected` → `SelectById` |
| Modify | `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` | chip + colonna Panoramica |
| Modify | `src/Recordum.WPF/Resources/Strings(.resx/.it.resx/.Designer.cs)` | 3 stringhe |
| Modify | fixture: `HabitScreenViewModelFactory`, `MainWindowViewModelFactory`, `MainWindowViewModelTests.CreateSut` | ctor +1 |
| Test | `PanoramicaViewModelTests` (new) + `HabitScreenColumnsTests`/`HabitScreenColumnWidthsTests`/`HomeViewModelTests`/`MainWindowViewModelTests` (append/update) | §A |

**Convenzioni**: build 0 warning nuovi; flaky noto ammesso se verde isolato; commit per task.

---

### Task 1: stringhe resx

**Files:** Modify `Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`

- [x] **Step 1**: EN, accanto a `HabitScreen.Column.Cronologia`:

```xml
  <data name="HabitScreen.Column.Panoramica" xml:space="preserve">
    <value>Overview</value>
  </data>
  <data name="HabitScreen.Panoramica.Today" xml:space="preserve">
    <value>Today {0}/{1}</value>
  </data>
  <data name="HabitScreen.Panoramica.Empty" xml:space="preserve">
    <value>No habits in this group yet</value>
  </data>
```

IT:

```xml
  <data name="HabitScreen.Column.Panoramica" xml:space="preserve">
    <value>Panoramica</value>
  </data>
  <data name="HabitScreen.Panoramica.Today" xml:space="preserve">
    <value>Oggi {0}/{1}</value>
  </data>
  <data name="HabitScreen.Panoramica.Empty" xml:space="preserve">
    <value>Nessuna abitudine in questo gruppo</value>
  </data>
```

Designer (accanto a `HabitScreen_Column_Cronologia`):

```csharp
    public static string HabitScreen_Column_Panoramica => Get("HabitScreen.Column.Panoramica");
    public static string HabitScreen_Panoramica_Today => Get("HabitScreen.Panoramica.Today");
    public static string HabitScreen_Panoramica_Empty => Get("HabitScreen.Panoramica.Empty");
```

- [x] **Step 2**: build 0/0; commit `feat(wpf): resx — Panoramica column strings`.

---

### Task 2: `PanoramicaViewModel` (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/PanoramicaViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/PanoramicaViewModelTests.cs` (new)

- [x] **Step 1: test (RED)**

```csharp
using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class PanoramicaViewModelTests
{
    private readonly FakeHabitTreeQueryService _tree = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero));

    private PanoramicaViewModel CreateSut() =>
        new(_tree, new FakeGoalService(), new FakeCompletionService(), _clock);

    private static HomeTreeRow Row(Guid id, string name, Guid? parent, HabitType? type = HabitType.Action, bool today = false) =>
        new(id, parent, name, "#112233", type, null, 0, today, PinScope.None, 0, false,
            ImmutableArray.Create(0, 1, 0, 1, 0, 1, 0), ImmutableArray<Guid>.Empty);

    private static HabitNode Group(Guid id) =>
        new() { Id = id, NodeType = NodeType.Group, Name = "G", Color = "#808080" };

    [Fact]
    public async Task Load_lists_descendant_habits_recursively_excluding_subgroup_rows()
    {
        var g = Guid.NewGuid(); var sub = Guid.NewGuid();
        var h1 = Guid.NewGuid(); var h2 = Guid.NewGuid(); var other = Guid.NewGuid();
        _tree.HomeRows.Add(Row(g, "G", null, type: null));
        _tree.HomeRows.Add(Row(h1, "diretto", g, today: true));
        _tree.HomeRows.Add(Row(sub, "Sub", g, type: null));
        _tree.HomeRows.Add(Row(h2, "annidato", sub));
        _tree.HomeRows.Add(Row(other, "estraneo", null));
        var sut = CreateSut();

        await sut.LoadAsync(Group(g));

        sut.Children.Select(c => c.Name).Should().BeEquivalentTo(new[] { "diretto", "annidato" });
        sut.TotalChildren.Should().Be(2);
        sut.CompletedToday.Should().Be(1);
        sut.HasChildren.Should().BeTrue();
    }

    [Fact]
    public async Task Load_with_no_descendants_reports_empty()
    {
        var g = Guid.NewGuid();
        _tree.HomeRows.Add(Row(g, "G", null, type: null));
        var sut = CreateSut();

        await sut.LoadAsync(Group(g));

        sut.HasChildren.Should().BeFalse();
        sut.TotalChildren.Should().Be(0);
    }

    [Fact]
    public async Task Selecting_a_child_raises_ChildSelected()
    {
        var g = Guid.NewGuid(); var h = Guid.NewGuid();
        _tree.HomeRows.Add(Row(g, "G", null, type: null));
        _tree.HomeRows.Add(Row(h, "figlio", g));
        var sut = CreateSut();
        await sut.LoadAsync(Group(g));
        Guid? selected = null;
        sut.ChildSelected += (_, id) => selected = id;

        sut.SelectChildCommand.Execute(sut.Children.Single());

        selected.Should().Be(h);
    }
}
```

- [x] **Step 2: RED** — `dotnet test tests/Recordum.WPF.Tests --filter PanoramicaViewModel` (compile error: tipo mancante).

- [x] **Step 3: implementare**

```csharp
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.Detail;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>Group overview column: today rollup, descendant habit rows, group goal (spec §A.2).</summary>
public sealed partial class PanoramicaViewModel : ObservableObject
{
    private readonly IHabitTreeQueryService _tree;

    public GoalProgressCardViewModel Goal { get; }
    public ObservableCollection<PanoramicaChildRow> Children { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChildren))]
    private int _totalChildren;
    [ObservableProperty] private int _completedToday;

    public bool HasChildren => TotalChildren > 0;

    /// <summary>Click on a child row: the host selects that habit in the Home tree (spec §A.3).</summary>
    public event EventHandler<Guid>? ChildSelected;

    public PanoramicaViewModel(IHabitTreeQueryService tree, IGoalService goals, ICompletionService completions, IClock clock)
    {
        _tree = tree;
        Goal = new GoalProgressCardViewModel(goals, completions, clock);
    }

    public async Task LoadAsync(HabitNode group, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        var rows = await _tree.GetHomeTreeAsync(ct).ConfigureAwait(true);
        var byParent = rows.ToLookup(r => r.ParentId);
        var habits = Descendants(group.Id, byParent).Where(r => r.HabitType is not null).ToList();
        Children.Clear();
        foreach (var h in habits)
            Children.Add(new PanoramicaChildRow(h.HabitNodeId, h.Name, h.ColorHex, h.IsCompletedToday, h.CompletionsLast7Days));
        TotalChildren = habits.Count;
        CompletedToday = habits.Count(h => h.IsCompletedToday);
        await Goal.LoadAsync(group, ct).ConfigureAwait(true);
    }

    private static IEnumerable<HomeTreeRow> Descendants(Guid parentId, ILookup<Guid?, HomeTreeRow> byParent)
    {
        foreach (var child in byParent[parentId])
        {
            yield return child;
            foreach (var nested in Descendants(child.HabitNodeId, byParent))
                yield return nested;
        }
    }

    [RelayCommand]
    private void SelectChild(PanoramicaChildRow row) => ChildSelected?.Invoke(this, row.HabitNodeId);
}

public sealed record PanoramicaChildRow(Guid HabitNodeId, string Name, string ColorHex, bool IsCompletedToday, ImmutableArray<int> CompletionsLast7Days);
```

NB: verificare il namespace/ctor esatti di `GoalProgressCardViewModel` (in `ViewModels/HabitScreen/Detail/`, ctor `(IGoalService, ICompletionService, IClock)`) e di `IClock` (`Recordum.Core.Abstractions`); adeguare gli using se differiscono.

- [x] **Step 4: GREEN** — filtro `PanoramicaViewModel` → 3 PASS.

- [x] **Step 5: commit** `feat(wpf): PanoramicaViewModel — group rollup with child navigation`.

---

### Task 3: HabitScreenViewModel — applicabilità, Show*, default, load gruppo (TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitColumn.cs` (+ `Panoramica` in coda)
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Modify: fixture `HabitScreenViewModelFactory.Build`, `MainWindowViewModelFactory`, `MainWindowViewModelTests.CreateSut` (ctor +1: `IHabitTreeQueryService` → `new FakeHabitTreeQueryService()`; in `MainWindowViewModelTests.CreateSut` e `MainWindowViewModelFactory` riusare la `query` già istanziata)
- Test: `HabitScreenColumnsTests` (append) + `HabitScreenColumnWidthsTests` (update CSV atteso)

- [x] **Step 1: test (RED)** — append a `HabitScreenColumnsTests` (aggiungere helper `AddGroup`):

```csharp
    private static Guid AddGroup(FakeHabitNodeService nodes)
    {
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Group, Name = "g", Color = "#808080" };
        return id;
    }

    [Fact]
    public async Task Group_with_no_applicable_columns_defaults_to_panoramica()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var g = AddGroup(nodes);

        await sut.LoadAsync(g);

        sut.IsPanoramicaOpen.Should().BeTrue();
        sut.ShowPanoramica.Should().BeTrue();
        sut.ShowCronologia.Should().BeFalse();
    }

    [Fact]
    public async Task Habit_columns_stay_flagged_but_hidden_on_a_group_and_return_on_a_habit()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var h = AddHabit(nodes);
        var g = AddGroup(nodes);
        await sut.LoadAsync(h);          // Cronologia aperta
        sut.IsDettaglioOpen = true;

        await sut.LoadAsync(g);          // gruppo: flag intatti, colonne habit nascoste

        sut.IsCronologiaOpen.Should().BeTrue();
        sut.ShowCronologia.Should().BeFalse();
        sut.ShowDettaglio.Should().BeFalse();
        sut.ShowPanoramica.Should().BeTrue();   // default applicabile aggiunto

        await sut.LoadAsync(h);          // tornando alla habit le colonne ricompaiono
        sut.ShowCronologia.Should().BeTrue();
        sut.ShowDettaglio.Should().BeTrue();
        sut.ShowPanoramica.Should().BeFalse();
    }

    [Fact]
    public async Task Closing_the_last_applicable_column_on_a_group_raises_AllColumnsClosed()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var h = AddHabit(nodes);
        var g = AddGroup(nodes);
        await sut.LoadAsync(h);          // Cronologia aperta (non applicabile al gruppo)
        await sut.LoadAsync(g);          // + Panoramica
        var raised = 0;
        sut.AllColumnsClosed += (_, _) => raised++;

        sut.IsPanoramicaOpen = false;    // Cronologia resta flaggata ma non applicabile

        raised.Should().Be(1);
    }

    [Fact]
    public async Task Group_load_populates_the_panoramica_rollup()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var g = AddGroup(nodes);

        await sut.LoadAsync(g);

        sut.Panoramica.Should().NotBeNull();
        sut.Panoramica.TotalChildren.Should().Be(0);   // il fake tree è vuoto: rollup caricato, zero figli
    }
```

e in `HabitScreenColumnWidthsTests.Persist_writes_csv_and_roundtrips_on_restore` aggiornare l'atteso:

```csharp
            .Should().Be("Cronologia=460,Dettaglio=520,Note=600,Configura=480,Panoramica=460");
```

- [x] **Step 2: RED** — filtro `"Group_with_no_applicable|Habit_columns_stay_flagged|Closing_the_last_applicable|Group_load_populates|Persist_writes_csv"`.

- [x] **Step 3: implementare**

`HabitColumn.cs`: aggiungere `Panoramica` come ULTIMO membro (ordine CSV stabile).

`HabitScreenViewModel`:

1. ctor: nuovo parametro `IHabitTreeQueryService tree` (dopo `IHabitNodeService nodes` o in coda prima di `editor` — scegliere la coda per minimizzare i diff) e:

```csharp
        Panoramica = new PanoramicaViewModel(tree, goals, completions, clock);
```

2. Membri colonna (regione column-workspace):

```csharp
    public PanoramicaViewModel Panoramica { get; }

    [ObservableProperty] private bool _isPanoramicaOpen;
    partial void OnIsPanoramicaOpenChanged(bool value) { OnColumnFlag(HabitColumn.Panoramica, value); NotifyShowFlags(); }

    /// <summary>Column applicability per node type (spec §A.1): groups get Panoramica/Note/Configura.</summary>
    internal static bool IsColumnApplicable(HabitColumn column, NodeType nodeType) => nodeType == NodeType.Group
        ? column is HabitColumn.Panoramica or HabitColumn.Note or HabitColumn.Configura
        : column is not HabitColumn.Panoramica;

    public bool IsGroupNode => Node?.NodeType == NodeType.Group;
    public bool ShowPanoramica => IsPanoramicaOpen && Node is not null && IsColumnApplicable(HabitColumn.Panoramica, Node.NodeType);
    public bool ShowCronologia => IsCronologiaOpen && Node is not null && IsColumnApplicable(HabitColumn.Cronologia, Node.NodeType);
    public bool ShowDettaglio => IsDettaglioOpen && Node is not null && IsColumnApplicable(HabitColumn.Dettaglio, Node.NodeType);
    public bool ShowNote => IsNoteOpen && Node is not null;
    public bool ShowConfigura => IsConfiguraOpen && Node is not null;

    private void NotifyShowFlags()
    {
        OnPropertyChanged(nameof(IsGroupNode));
        OnPropertyChanged(nameof(ShowPanoramica));
        OnPropertyChanged(nameof(ShowCronologia));
        OnPropertyChanged(nameof(ShowDettaglio));
        OnPropertyChanged(nameof(ShowNote));
        OnPropertyChanged(nameof(ShowConfigura));
    }

    partial void OnNodeChanged(HabitNode? value) => NotifyShowFlags();
```

   e nei partial esistenti `OnIsCronologiaOpenChanged`/`OnIsDettaglioOpenChanged`/`OnIsNoteOpenChanged`/`OnIsConfiguraOpenChanged` aggiungere `NotifyShowFlags();` in coda.

3. `SetColumnFlag`/`CanonicalOpen`/`ToggleColumn` (`switch`): aggiungere il caso `Panoramica` (flag `IsPanoramicaOpen`); in `CanonicalOpen` lo yield di Panoramica va in coda (dopo Configura) per coerenza con l'ordine enum.

4. `OnColumnFlag`, condizione AllColumnsClosed — applicabile-aware:

```csharp
            if (Node is not null && !CanonicalOpen().Any(c => IsColumnApplicable(c, Node.NodeType)))
                AllColumnsClosed?.Invoke(this, EventArgs.Empty);
```

5. `LoadAsync`, default applicabile-aware (sostituisce il blocco `if (!CanonicalOpen().Any()) …`):

```csharp
        // No applicable column open for this node type: open the last closed one if it fits,
        // else the type's default (group → Panoramica §A.1, habit → Cronologia §3.7/§B.5).
        if (!CanonicalOpen().Any(c => IsColumnApplicable(c, node.NodeType)))
        {
            var fallback = node.NodeType == NodeType.Group ? HabitColumn.Panoramica : HabitColumn.Cronologia;
            SetColumnFlag(IsColumnApplicable(_lastClosedColumn, node.NodeType) ? _lastClosedColumn : fallback, true);
        }
```

6. `LoadAsync`, dopo il blocco default: caricare il rollup per i gruppi:

```csharp
        if (node.NodeType == NodeType.Group)
            await Panoramica.LoadAsync(node, ct).ConfigureAwait(true);
```

7. Larghezza colonna: in `DefaultWidth` aggiungere `HabitColumn.Panoramica => 460,`; aggiungere la coppia campo/proprietà:

```csharp
    private double _panoramicaWidth = DefaultWidth(HabitColumn.Panoramica);
    public double PanoramicaWidth { get => _panoramicaWidth; set => SetProperty(ref _panoramicaWidth, ClampWidth(value)); }
```

   e i casi `Panoramica` in `GetColumnWidth`/`SetColumnWidth`.

8. Fixture: aggiornare le 3 costruzioni (`HabitScreenViewModelFactory.Build`, `MainWindowViewModelFactory`, `MainWindowViewModelTests.CreateSut`) col nuovo parametro (`query` esistente in MainWindow*, `new FakeHabitTreeQueryService()` nella factory di HabitScreen). `grep -rn "new HabitScreenViewModel" src tests` per verificare che non ce ne siano altre (in `src` la costruzione è via DI: nessun cambio a `ServiceRegistrar`).

- [x] **Step 4: GREEN** — filtri step 2 + suite `HabitScreenColumns|HabitScreenColumnWidths|MainWindowViewModel` complete verdi.

- [x] **Step 5: commit** `feat(wpf): Panoramica column state — per-node-type applicability`.

---

### Task 4: navigazione figlio (TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HomeViewModel.cs` (`SelectById`)
- Modify: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs` (wiring)
- Test: `HomeViewModelTests` + `MainWindowViewModelTests` (append)

- [x] **Step 1: test (RED)** — `HomeViewModelTests`:

```csharp
    [Fact]
    public async Task SelectById_expands_collapsed_ancestors_and_selects()
    {
        var groupId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        _query.HomeRows.Add(SampleRow(name: "child", id: childId, parent: groupId));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.Rows.Single(r => r.IsGroup).IsExpanded = false;   // il figlio sparisce

        sut.SelectById(childId);

        sut.SelectedRow!.HabitNodeId.Should().Be(childId);
        sut.Rows.Should().HaveCount(2);                        // antenato riespanso
    }
```

`MainWindowViewModelTests`:

```csharp
    [Fact]
    public async Task Panoramica_child_click_selects_the_habit_in_home()
    {
        var (sut, parts) = CreateSut();
        var g = Guid.NewGuid(); var h = Guid.NewGuid();
        parts.Nodes.Store[g] = new HabitNode { Id = g, NodeType = NodeType.Group, Name = "G", Color = "#808080" };
        parts.Nodes.Store[h] = new HabitNode { Id = h, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h", Color = "#808080" };
        parts.Query.HomeRows.Add(SampleRow(g, "G", type: null));
        parts.Query.HomeRows.Add(SampleRow(h, "h", parent: g));
        await sut.InitializeAsync();
        sut.Home.SelectedRow = sut.Home.Rows.Single(r => r.HabitNodeId == g);
        await Task.Delay(30);

        sut.HabitScreen.Panoramica.SelectChildCommand.Execute(sut.HabitScreen.Panoramica.Children.Single());
        await Task.Delay(30);

        sut.Home.SelectedRow!.HabitNodeId.Should().Be(h);
    }
```

- [x] **Step 2: RED** — filtro `"SelectById_expands|Panoramica_child_click"`.

- [x] **Step 3: implementare**

`HomeViewModel` (dopo `SelectRow`):

```csharp
    /// <summary>Selects a node by id, expanding any collapsed ancestor so its row is visible
    /// (used by the group Panoramica child navigation, spec §A.3).</summary>
    public void SelectById(Guid id)
    {
        var byId = _lastFetched.ToDictionary(r => r.HabitNodeId);
        var changed = false;
        for (var cur = byId.GetValueOrDefault(id); cur?.ParentId is { } pid; cur = byId.GetValueOrDefault(pid))
            changed |= _collapsedGroupIds.Remove(pid);
        if (changed) Rebuild();
        SelectedRow = Rows.FirstOrDefault(r => r.HabitNodeId == id);
    }
```

`MainWindowViewModel` ctor (dopo `HabitScreen.AllColumnsClosed += …`):

```csharp
        HabitScreen.Panoramica.ChildSelected += OnPanoramicaChildSelected;
```

e accanto a `OnAllColumnsClosed`:

```csharp
    // Panoramica child row click: jump to that habit in the tree (spec §A.3).
    private void OnPanoramicaChildSelected(object? sender, Guid habitNodeId)
        => Home.SelectById(habitNodeId);
```

- [x] **Step 4: GREEN** — filtri step 2 + suite `HomeViewModel` e `MainWindowViewModel` verdi.

- [x] **Step 5: commit** `feat(wpf): Panoramica child navigation selects the habit in the tree`.

---

### Task 5: XAML — chip + colonna Panoramica

**Files:** Modify `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`

- [x] **Step 1: chip** — nella chip-bar (StackPanel, riga ~600): la chip Panoramica va PRIMA di Cronologia; le chip Cronologia/Dettaglio si nascondono sui gruppi; tutte le `Visibility` con `BoolToVisible`:

```xml
                <CheckBox Style="{StaticResource Recordum.SegmentCheck}"
                          Content="{x:Static res:Strings.HabitScreen_Column_Panoramica}"
                          IsChecked="{Binding IsPanoramicaOpen}"
                          Visibility="{Binding IsGroupNode, Converter={StaticResource BoolToVisible}}"/>
```

e sulle chip Cronologia e Dettaglio esistenti aggiungere:

```xml
                          Visibility="{Binding IsGroupNode, Converter={StaticResource InvBoolToVisible}}"
```

(verificare la key esatta del converter inverso già usato nel file: `InvBoolToVisible`).

- [x] **Step 2: visibilità colonne** — sostituire i binding `Visibility` delle 4 colonne esistenti: `IsCronologiaOpen`→`ShowCronologia`, `IsDettaglioOpen`→`ShowDettaglio`, `IsNoteOpen`→`ShowNote`, `IsConfiguraOpen`→`ShowConfigura` (stesso converter).

- [x] **Step 3: colonna Panoramica** — PRIMA del Border Cronologia, stesso chrome delle altre (header+✕+thumb):

```xml
                    <!-- Panoramica (group overview) -->
                    <Border Width="{Binding PanoramicaWidth}" Style="{StaticResource HabitScreen.Column}"
                            Visibility="{Binding ShowPanoramica, Converter={StaticResource BoolToVisible}}">
                        <Grid>
                            <DockPanel>
                                <Grid DockPanel.Dock="Top" Style="{StaticResource HabitScreen.ColumnHeader}">
                                    <TextBlock Text="{x:Static res:Strings.HabitScreen_Column_Panoramica}" Style="{StaticResource HabitScreen.ColumnTitle}"/>
                                    <Button Style="{StaticResource HabitScreen.ColumnClose}"
                                            Command="{Binding ToggleColumnCommand}"
                                            CommandParameter="{x:Static vm:HabitColumn.Panoramica}"/>
                                </Grid>
                                <ScrollViewer VerticalScrollBarVisibility="Auto">
                                    <StackPanel DataContext="{Binding Panoramica}" Margin="12">
                                        <!-- Goal card (same markup family as the Detail column) -->
                                        <StackPanel Margin="0,0,0,12">
                                            <TextBlock Text="{Binding Goal.Name}" FontWeight="SemiBold"
                                                       Visibility="{Binding Goal.IsEmpty, Converter={StaticResource InvBoolToVisible}}"/>
                                            <StackPanel Orientation="Horizontal" Margin="0,4,0,0"
                                                        Visibility="{Binding Goal.IsEmpty, Converter={StaticResource InvBoolToVisible}}">
                                                <TextBlock Text="{Binding Goal.Actual}" FontSize="{StaticResource FontSize.Heading}"/>
                                                <TextBlock Text=" / " VerticalAlignment="Center"/>
                                                <TextBlock Text="{Binding Goal.Target}" FontSize="{StaticResource FontSize.Heading}"/>
                                                <TextBlock Text="{Binding Goal.PeriodLabel}" Foreground="{DynamicResource Brush.TextMuted}"
                                                           Margin="12,0,0,0" VerticalAlignment="Center"/>
                                            </StackPanel>
                                        </StackPanel>
                                        <!-- Today rollup -->
                                        <TextBlock FontWeight="SemiBold" Margin="0,0,0,8"
                                                   Foreground="{DynamicResource Brush.Text}">
                                            <TextBlock.Text>
                                                <MultiBinding StringFormat="{x:Static res:Strings.HabitScreen_Panoramica_Today}">
                                                    <Binding Path="CompletedToday"/>
                                                    <Binding Path="TotalChildren"/>
                                                </MultiBinding>
                                            </TextBlock.Text>
                                        </TextBlock>
                                        <TextBlock Text="{x:Static res:Strings.HabitScreen_Panoramica_Empty}"
                                                   Foreground="{DynamicResource Brush.TextMuted}"
                                                   Visibility="{Binding HasChildren, Converter={StaticResource InvBoolToVisible}}"/>
                                        <!-- Child habit rows -->
                                        <ItemsControl ItemsSource="{Binding Children}">
                                            <ItemsControl.ItemTemplate>
                                                <DataTemplate>
                                                    <Button Command="{Binding DataContext.SelectChildCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                                            CommandParameter="{Binding}"
                                                            Background="Transparent" BorderThickness="0" Padding="0"
                                                            HorizontalContentAlignment="Stretch" Cursor="Hand">
                                                        <Grid Height="32">
                                                            <Grid.ColumnDefinitions>
                                                                <ColumnDefinition Width="Auto"/>
                                                                <ColumnDefinition Width="*"/>
                                                                <ColumnDefinition Width="Auto"/>
                                                                <ColumnDefinition Width="Auto"/>
                                                            </Grid.ColumnDefinitions>
                                                            <Rectangle Width="4" RadiusX="1" RadiusY="1" Margin="0,4"
                                                                       Fill="{Binding ColorHex, Converter={StaticResource HexToBrush}}"/>
                                                            <TextBlock Grid.Column="1" Text="{Binding Name}" Margin="8,0,0,0"
                                                                       VerticalAlignment="Center"
                                                                       Foreground="{DynamicResource Brush.Text}"/>
                                                            <v2:HeatmapStrip Grid.Column="2" Margin="0,0,8,0" VerticalAlignment="Center"
                                                                             Counts="{Binding CompletionsLast7Days}"
                                                                             AccentColor="{Binding ColorHex}"/>
                                                            <TextBlock Grid.Column="3" Text="✓" Margin="0,0,4,0" VerticalAlignment="Center"
                                                                       Foreground="{DynamicResource Brush.Accent}"
                                                                       Visibility="{Binding IsCompletedToday, Converter={StaticResource BoolToVisible}}"/>
                                                        </Grid>
                                                    </Button>
                                                </DataTemplate>
                                            </ItemsControl.ItemTemplate>
                                        </ItemsControl>
                                    </StackPanel>
                                </ScrollViewer>
                            </DockPanel>
                            <Thumb Style="{StaticResource HabitScreen.ColumnResizeThumb}"
                                   Tag="{x:Static vm:HabitColumn.Panoramica}"
                                   DragDelta="OnColumnResizeDelta"
                                   DragCompleted="OnColumnResizeCompleted"
                                   MouseDoubleClick="OnColumnResizeReset"/>
                        </Grid>
                    </Border>
```

NB: verificare l'xmlns per `HeatmapStrip` (`Recordum.WPF.Views`, probabilmente già mappato come `v:` — usare il prefisso esistente; `v2:` nel blocco sopra è segnaposto da sostituire col prefisso reale). `MultiBinding StringFormat` con `x:Static` non è valido: se il compilatore XAML si lamenta, sostituire con `StringFormat='{}{0}/{1}'` prefissato dall'etichetta, oppure esporre dal VM una stringa `TodayLabel` già formattata — in tal caso aggiungere al VM:

```csharp
    public string TodayLabel => string.Format(Resources.Strings.HabitScreen_Panoramica_Today, CompletedToday, TotalChildren);
```

con `[NotifyPropertyChangedFor(nameof(TodayLabel))]` su `_completedToday` e `_totalChildren`, e nel XAML un semplice `Text="{Binding TodayLabel}"`. **Preferire direttamente questa seconda forma** (più semplice e testabile).

- [x] **Step 4**: build 0/0 + suite WPF completa verde.

- [x] **Step 5: commit** `feat(wpf): Panoramica column UI — group rollup with goal card and child rows`.

---

### Task 6: verifica visiva + tracker

- [ ] skill `run` + checklist: gruppo selezionato → chip Panoramica/Note/Configura (niente Cronologia/Dettaglio); Panoramica default; rollup oggi x/y + righe figli con sparkline e ✓; click su figlio → naviga (anche con gruppo collassato); goal di gruppo visibile se configurato; habit → le 4 chip tornano col set conservato; resize/✕/Esc della colonna Panoramica; due temi.
- [x] aggiornare `docs/phase-4-issues.md` + memoria (piano 2 fatto; resta piano 3 = selezione multipla); commit docs.

---

## Self-review

- Spec A.1 (chip dinamiche, flag conservati, default Panoramica)→T3/T5; A.2 (rollup: conteggio, righe ricorsive senza righe-sottogruppo, goal card, empty)→T2/T5; A.3 (navigazione)→T4. Coverage completa.
- CSV widths: Panoramica in coda all'enum → token nuovo in coda; restore di CSV vecchie senza il token → default 460 (già tollerato dal parser). Test aggiornato in T3.
- `AllColumnsClosed` applicabile-aware (T3) mantiene verde il test B `AllColumnsClosed_raised_when_the_open_set_empties` (habit: Cronologia applicabile).
- Tipi coerenti: `PanoramicaViewModel`, `PanoramicaChildRow`, `ChildSelected` `EventHandler<Guid>`, `SelectById(Guid)`, `IsColumnApplicable(HabitColumn, NodeType)`, `Show*`, `PanoramicaWidth`, `TodayLabel`.
- Rischio noto: `GoalProgressCardViewModel` su gruppo è già il percorso usato da `DetailViewModel.LoadAsync` (ramo IsGroup) — nessun comportamento nuovo.
