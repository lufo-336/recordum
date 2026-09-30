# Gruppi-basi (＋Nuova▾ + drag highlight) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Il bottone "Nuova" della Home diventa un menu Habit/Gruppo context-aware sul parent, "Nuovo gruppo" crea subito e apre Configura, e il drag-drop evidenzia il gruppo bersaglio.

**Architecture:** Parent resolution e creazione gruppo in `HomeViewModel` (evento `GroupCreated` verso `MainWindowViewModel`, che apre la colonna Configura). Fix mirato del rebind editor in `MainWindowViewModel.OnHomePropertyChanged` (oggi cambiando habit con Configura aperta l'editor resta sul nodo precedente). Highlight drag via flag `IsDropTarget` su `HomeRowViewModel` settato da `TreeDragDropBehavior.DragOver` e consumato da un DataTrigger in `Recordum.HabitBlock`.

**Tech Stack:** WPF + CommunityToolkit.Mvvm; xUnit/FluentAssertions (fixture esistenti: `FakeHabitNodeService`, `FakeHabitTreeQueryService`, `FakeHabitTemplatePickerLauncher` — quest'ultimo cattura già `LastParentId`).

**Spec:** `docs/superpowers/archive/specs/2026-06-11-groups-basics-design.md`

---

## Mappa file

| Azione | Path | Responsabilità |
|---|---|---|
| Modify | `src/Recordum.WPF/Resources/Strings(.resx/.it.resx/.Designer.cs)` | 2 stringhe menu + label bottone |
| Modify | `src/Recordum.WPF/ViewModels/HomeViewModel.cs` | parent resolution, `CreateGroupCommand`, evento `GroupCreated`, `PickAsync(parentId)` |
| Modify | `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs` | handler `GroupCreated` + fix rebind editor |
| Modify | `src/Recordum.WPF/Views/HomeView.xaml(.cs)` | bottone → menu a 2 voci |
| Modify | `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs` | flag `IsDropTarget` |
| Modify | `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` | DragOver/DragLeave → set/clear flag |
| Modify | `src/Recordum.WPF/App.xaml` | DataTrigger `IsDropTarget` in `Recordum.HabitBlock` |
| Modify | `tests/Recordum.WPF.Tests/Fixtures/FakeHabitNodeService.cs` | hook `OnCreate` |
| Modify | `tests/Recordum.WPF.Tests/Fixtures/MainWindowViewModelFactory.cs` | overload che espone i fake |
| Test | `tests/.../ViewModels/HomeViewModelTests.cs` + `tests/.../ViewModels/MainWindowViewModelTests.cs` | append |

**Convenzioni**: build 0 warning nuovi; flaky noto `CompletionRecorded_event_reloads…` ammesso se verde isolato; commit per task.

---

### Task 1: stringhe resx

**Files:** Modify `Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`

- [x] **Step 1**: trovare la chiave esistente del bottone (`grep -n "Home.NewHabit.Button" src/Recordum.WPF/Resources/Strings.resx`) e aggiornarne il valore: EN `＋ New ▾`, IT `＋ Nuova ▾`. Poi aggiungere subito dopo (EN):

```xml
  <data name="Home.NewMenu.Habit" xml:space="preserve">
    <value>New habit</value>
  </data>
  <data name="Home.NewMenu.Group" xml:space="preserve">
    <value>New group</value>
  </data>
```

IT:

```xml
  <data name="Home.NewMenu.Habit" xml:space="preserve">
    <value>Nuova abitudine</value>
  </data>
  <data name="Home.NewMenu.Group" xml:space="preserve">
    <value>Nuovo gruppo</value>
  </data>
```

Designer (accanto a `Home_NewHabit_Button`):

```csharp
    public static string Home_NewMenu_Habit => Get("Home.NewMenu.Habit");
    public static string Home_NewMenu_Group => Get("Home.NewMenu.Group");
```

- [x] **Step 2**: build `dotnet build src/Recordum.WPF -v q` 0 errori/0 warning; commit `feat(wpf): resx — New habit / New group menu strings`.

---

### Task 2: HomeViewModel — parent resolution + CreateGroup (TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HomeViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/Fixtures/FakeHabitNodeService.cs` (hook `OnCreate`)
- Test: `tests/Recordum.WPF.Tests/ViewModels/HomeViewModelTests.cs` (append)

- [x] **Step 1: hook nel fake** — in `FakeHabitNodeService` aggiungere accanto agli altri hook (`OnSoftDelete`, riga ~29):

```csharp
    public Action<HabitNode>? OnCreate { get; set; }
```

e in `CreateAsync`, dopo l'inserimento nello `Store` e prima del `return`, invocare:

```csharp
        OnCreate?.Invoke(draft);
```

- [x] **Step 2: test (RED)** — append a `HomeViewModelTests` (riusa `SampleRow`, `_query/_nodes/_purge`, `CreateSut`):

```csharp
    [Fact]
    public async Task OpenNewHabit_with_group_selected_passes_group_as_parent()
    {
        var groupId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        var launcher = new FakeHabitTemplatePickerLauncher();
        var sut = new HomeViewModel(_query, _nodes, _purge, launcher);
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == groupId);

        await sut.OpenNewHabitCommand.ExecuteAsync(null);

        launcher.LastParentId.Should().Be(groupId);
    }

    [Fact]
    public async Task OpenNewHabit_with_habit_selected_passes_its_parent_as_parent()
    {
        var groupId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        _query.HomeRows.Add(SampleRow(name: "h", id: habitId, parent: groupId));
        var launcher = new FakeHabitTemplatePickerLauncher();
        var sut = new HomeViewModel(_query, _nodes, _purge, launcher);
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == habitId);

        await sut.OpenNewHabitCommand.ExecuteAsync(null);

        launcher.LastParentId.Should().Be(groupId);
    }

    [Fact]
    public async Task CreateGroup_with_no_selection_creates_at_root_selects_and_raises()
    {
        _nodes.OnCreate = draft => _query.HomeRows.Add(
            new HomeTreeRow(draft.Id, draft.ParentId, draft.Name, draft.Color, null, null, 0,
                false, PinScope.None, 0, false,
                ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        Guid? raised = null;
        sut.GroupCreated += (_, id) => raised = id;

        await sut.CreateGroupCommand.ExecuteAsync(null);

        var created = _nodes.Store.Values.Single();
        created.NodeType.Should().Be(NodeType.Group);
        created.ParentId.Should().BeNull();
        sut.SelectedRow!.HabitNodeId.Should().Be(created.Id);
        raised.Should().Be(created.Id);
    }

    [Fact]
    public async Task CreateGroup_with_group_selected_creates_inside_it()
    {
        var groupId = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: groupId));
        _nodes.OnCreate = draft => _query.HomeRows.Add(
            new HomeTreeRow(draft.Id, draft.ParentId, draft.Name, draft.Color, null, null, 0,
                false, PinScope.None, 0, false,
                ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == groupId);

        await sut.CreateGroupCommand.ExecuteAsync(null);

        _nodes.Store.Values.Single(n => n.NodeType == NodeType.Group && n.Id != groupId)
            .ParentId.Should().Be(groupId);
    }
```

NB: se `NodeType`/`ImmutableArray` non sono già importati nel file di test, aggiungere gli using (`Recordum.Core.Enums`, `System.Collections.Immutable` sono già presenti — verificare).

- [x] **Step 3: RED** — `dotnet test tests/Recordum.WPF.Tests --filter "CreateGroup|OpenNewHabit_with"` (errori di compilazione attesi: `GroupCreated`, `CreateGroupCommand`, `OnCreate`).

- [x] **Step 4: implementare** in `HomeViewModel`:

Using nuovi in testa al file: `using Recordum.Core.Models;` e `using Recordum.WPF.Resources;`.

Evento (accanto a `HabitActivationRequested`, cercarlo nel file):

```csharp
    /// <summary>Raised after a group is created via the ＋New menu (host opens Configura for rename).</summary>
    public event EventHandler<Guid>? GroupCreated;
```

`OpenNewHabitAsync` (riga ~117): la chiamata `_launcher.PickAsync()` diventa:

```csharp
        var newId = await _launcher.PickAsync(ResolveCreationParentId());
```

Nuovi membri (dopo `OpenNewHabitAsync`):

```csharp
    // ＋Nuova▾ context: group selected → inside it; habit selected → sibling; nothing → root.
    private Guid? ResolveCreationParentId() => SelectedRow switch
    {
        null => null,
        { IsGroup: true } row => row.HabitNodeId,
        var row => row.ParentId,
    };

    [RelayCommand]
    private async Task CreateGroupAsync()
    {
        var id = await _nodes.CreateAsync(new HabitNode
        {
            NodeType = NodeType.Group,
            Name = Strings.Editor_Default_NewGroup,
            Color = "#808080",
            ParentId = ResolveCreationParentId(),
        });
        await LoadAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.HabitNodeId == id);
        GroupCreated?.Invoke(this, id);
    }
```

(Dopo `LoadAsync` tutte le righe rinascono con `IsExpanded = true`, quindi il nuovo nodo è sempre presente in `Rows` anche dentro un gruppo.)

- [x] **Step 5: GREEN** — filtro `"CreateGroup|OpenNewHabit"` tutto verde (incluso il test esistente `OpenNewHabit_launches_picker_and_selects_created_row`).

- [x] **Step 6: commit** `feat(wpf): context-aware creation parent + direct group creation in HomeViewModel`.

---

### Task 3: MainWindowViewModel — apertura Configura su GroupCreated + fix rebind editor (TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs`
- Modify: `tests/Recordum.WPF.Tests/Fixtures/MainWindowViewModelFactory.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/MainWindowViewModelTests.cs` (append)

- [x] **Step 1: factory** — in `MainWindowViewModelFactory` aggiungere un overload che espone i fake (rifattorizzare `Build` per delegare):

```csharp
    public static (MainWindowViewModel Vm, FakeHabitTreeQueryService Query, FakeHabitNodeService Nodes)
        BuildWithFakes(IBackupDialogLauncher? launcher = null, IBackupLogService? log = null)
    {
        // corpo identico all'attuale Build, ma ritorna anche query e nodes
    }

    public static MainWindowViewModel Build(
        IBackupDialogLauncher? launcher = null, IBackupLogService? log = null)
        => BuildWithFakes(launcher, log).Vm;
```

(Spostare il corpo attuale di `Build` dentro `BuildWithFakes` cambiando solo il `return`.)

NB: dentro la factory `HomeViewModel` è costruito senza launcher (`new HomeViewModel(query, nodes, purge)`) — va bene: questi test non passano dal picker.

- [x] **Step 2: test (RED)** — append a `MainWindowViewModelTests` (rispettare gli using esistenti del file; servono `Recordum.Core.Enums`, `Recordum.Core.Abstractions.Services.Dtos`, `System.Collections.Immutable` — verificare):

```csharp
    private static HomeTreeRow Row(Guid id, string name, HabitType? type = HabitType.Action, Guid? parent = null) =>
        new(id, parent, name, "#000000", type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    [Fact]
    public async Task Switching_habit_with_configura_open_rebinds_the_editor()
    {
        var (vm, query, nodes) = MainWindowViewModelFactory.BuildWithFakes();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        nodes.Store[a] = new HabitNode { Id = a, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "a", Color = "#808080" };
        nodes.Store[b] = new HabitNode { Id = b, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "b", Color = "#808080" };
        query.HomeRows.Add(Row(a, "a"));
        query.HomeRows.Add(Row(b, "b"));
        await vm.Home.LoadCommand.ExecuteAsync(null);

        vm.Home.SelectedRow = vm.Home.Rows.Single(r => r.HabitNodeId == a);
        vm.HabitScreen.IsConfiguraOpen = true;
        vm.Editor.Node!.Id.Should().Be(a);

        vm.Home.SelectedRow = vm.Home.Rows.Single(r => r.HabitNodeId == b);

        vm.Editor.Node!.Id.Should().Be(b);
    }

    [Fact]
    public async Task Creating_a_group_opens_configura_on_the_new_group()
    {
        var (vm, query, nodes) = MainWindowViewModelFactory.BuildWithFakes();
        nodes.OnCreate = draft =>
        {
            nodes.Store[draft.Id] = draft;
            query.HomeRows.Add(Row(draft.Id, draft.Name, type: null));
        };
        await vm.Home.LoadCommand.ExecuteAsync(null);

        await vm.Home.CreateGroupCommand.ExecuteAsync(null);

        vm.HabitScreen.IsConfiguraOpen.Should().BeTrue();
        vm.Editor.Node!.NodeType.Should().Be(NodeType.Group);
    }
```

(Se `vm.Editor.Node` non esiste con quel nome, cercare la property del nodo corrente in `HabitEditorViewModel` — è l'`[ObservableProperty]` usato da `LoadAsync` — e adeguare le assert.)

- [x] **Step 3: RED** — `dotnet test tests/Recordum.WPF.Tests --filter "Switching_habit_with_configura_open|Creating_a_group_opens_configura"` → il primo fallisce sull'ultima assert (editor fermo su `a`), il secondo non compila/fallisce.

- [x] **Step 4: implementare** in `MainWindowViewModel`:

Nel ctor, dopo `Home.HabitActivationRequested += …`:

```csharp
        Home.GroupCreated += OnHomeGroupCreated;
```

`OnHomePropertyChanged` diventa:

```csharp
    private async void OnHomePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.SelectedRow)) return;
        await HabitScreen.LoadAsync(Home.SelectedRow?.HabitNodeId).ConfigureAwait(true);
        if (Home.SelectedRow is null)
            await Editor.LoadAsync(null).ConfigureAwait(true);
        else if (HabitScreen.IsConfiguraOpen)
            // Rebind: with the Configura column open, switching habit must reload the editor
            // (ConfigureRequested only fires on the closed→open transition).
            await Editor.LoadAsync(Home.SelectedRow.HabitNodeId).ConfigureAwait(true);
    }
```

Nuovo handler accanto a `OnHabitScreenConfigureRequested`:

```csharp
    // New group from ＋Nuova▾: open Configura for the immediate rename. If the column is
    // already open the flag set is a no-op and the rebind in OnHomePropertyChanged covers it.
    private void OnHomeGroupCreated(object? sender, Guid id)
        => HabitScreen.IsConfiguraOpen = true;
```

- [x] **Step 5: GREEN** — filtro del passo 3 verde + filtro `MainWindowViewModel` completo verde.

- [x] **Step 6: commit** `feat(wpf): open Configura on group creation; fix editor rebind with open Configura`.

---

### Task 4: HomeView — bottone → menu ＋Nuova▾

**Files:** Modify `src/Recordum.WPF/Views/HomeView.xaml`, `src/Recordum.WPF/Views/HomeView.xaml.cs`

- [x] **Step 1**: in `HomeView.xaml` (riga ~45) sostituire il Button:

```xml
            <Button Grid.Column="2"
                    Content="{x:Static res:Strings.Home_NewHabit_Button}"
                    Click="OnNewMenuClick"
                    Margin="0,0,16,0"
                    Padding="12,4"
                    VerticalAlignment="Center">
                <Button.ContextMenu>
                    <ContextMenu>
                        <MenuItem Header="{x:Static res:Strings.Home_NewMenu_Habit}"
                                  Command="{Binding OpenNewHabitCommand}"/>
                        <MenuItem Header="{x:Static res:Strings.Home_NewMenu_Group}"
                                  Command="{Binding CreateGroupCommand}"/>
                    </ContextMenu>
                </Button.ContextMenu>
            </Button>
```

- [x] **Step 2**: in `HomeView.xaml.cs` aggiungere l'handler (stesso pattern del menu Backup nel ribbon — verificare `WorkspaceRibbon.xaml.cs` e riusarne la forma):

```csharp
    private void OnNewMenuClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu!.DataContext = button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }
```

(using necessari: `System.Windows`, `System.Windows.Controls` — verificare quelli già presenti.)

- [x] **Step 3**: build 0/0; commit `feat(wpf): Home New button becomes Habit/Group menu`.

---

### Task 5: drag-drop evidente

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs`
- Modify: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs`
- Modify: `src/Recordum.WPF/App.xaml`

- [x] **Step 1**: in `HomeRowViewModel` aggiungere accanto a `_isExpanded` (riga ~50):

```csharp
    /// <summary>True while a dragged row hovers this group's "into" zone (drop highlight).</summary>
    [ObservableProperty] private bool _isDropTarget;
```

- [x] **Step 2**: in `TreeDragDropBehavior`:

Campo statico accanto a `_armed`:

```csharp
    private static HomeRowViewModel? _dropTarget;
```

In `OnEnabledChanged`, ramo enable: aggiungere

```csharp
            ic.DragOver += OnDragOver;
            ic.DragLeave += OnDragLeave;
```

e nel ramo disable i corrispondenti `-=`.

Nuovi handler:

```csharp
    private static void OnDragOver(object sender, DragEventArgs e)
    {
        HomeRowViewModel? target = null;
        if (e.Data.GetData(typeof(HomeRowViewModel)) is HomeRowViewModel src
            && e.OriginalSource is FrameworkElement fe
            && fe.DataContext is HomeRowViewModel row
            && !ReferenceEquals(row, src)
            && row.IsGroup
            && ResolveZone(e.GetPosition(fe).Y / Math.Max(1, fe.ActualHeight)) == DropZone.Onto)
        {
            target = row;
        }
        SetDropTarget(target);
    }

    private static void OnDragLeave(object sender, DragEventArgs e) => SetDropTarget(null);

    private static void SetDropTarget(HomeRowViewModel? target)
    {
        if (ReferenceEquals(_dropTarget, target)) return;
        if (_dropTarget is not null) _dropTarget.IsDropTarget = false;
        _dropTarget = target;
        if (_dropTarget is not null) _dropTarget.IsDropTarget = true;
    }
```

In `OnDrop`, prima riga del corpo: `SetDropTarget(null);`

- [x] **Step 3**: in `App.xaml`, nello style `Recordum.HabitBlock` (riga ~65), aggiungere DOPO il DataTrigger `IsSelected` (così il drop-target vince quando entrambi attivi):

```xml
                    <DataTrigger Binding="{Binding IsDropTarget}" Value="True">
                        <Setter Property="Background" Value="{Binding ColorHex, Converter={StaticResource HabitTint}, ConverterParameter=51}"/>
                        <Setter Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                        <Setter Property="BorderThickness" Value="1.5"/>
                    </DataTrigger>
```

- [x] **Step 4**: build 0/0 + suite WPF completa (`dotnet test tests/Recordum.WPF.Tests -v q --nologo`) — verde (flaky noto ammesso se verde isolato).

- [x] **Step 5**: commit `feat(wpf): drop-target highlight while dragging onto a group`.

---

### Task 6: verifica visiva + tracker

- [x] skill `run` + checklist (utente: ok 2026-06-11): menu ＋Nuova▾ (2 voci); "Nuova habit" con gruppo selezionato → il picker crea dentro il gruppo; con habit selezionata → fratello; "Nuovo gruppo" nei 3 contesti → creato nel posto giusto, selezionato, Configura aperta col nome da rinominare; cambio habit con Configura aperta → editor segue (fix rebind); drag di una riga sopra un gruppo → highlight accent solo nella zona centrale; due temi.
- [x] aggiornare `docs/phase-4-issues.md` (gruppi-basi fatto; nav-model §3.2 chiuso; resta §5 vista gruppo) + memoria; commit docs.

---

## Self-review

- Spec §2.1 (menu)→T4; §2.2 (parent resolution + PickAsync)→T2; §2.3 (creazione diretta + Configura)→T2/T3; §2.4 (highlight Onto su gruppi)→T5; §4 testing→T2/T3 + T6 visivo. Fix rebind editor: fuori spec ma gap reale scoperto durante il design del glue — documentato in Architecture e T3.
- Tipi coerenti: `GroupCreated` `EventHandler<Guid>`, `CreateGroupCommand` (generato da `CreateGroupAsync`), `ResolveCreationParentId`, `IsDropTarget`, hook `OnCreate(HabitNode)`.
- `HomeTreeRow` positional ctor replicato dal `SampleRow` esistente (13 argomenti) — se il ctor differisce, copiare la forma da `SampleRow`.
- Niente migrazioni, niente nuove chiavi AppSetting.
