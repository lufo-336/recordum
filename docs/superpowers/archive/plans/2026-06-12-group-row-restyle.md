# Restyle riga gruppo — cornice contenitore (BC3) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** I gruppi nell'albero hanno chevron ▸/▾, nome in grassetto e — quando aperti — una cornice arrotondata nel colore del gruppo che racchiude i figli indentati.

**Architecture:** View ricorsiva su una nuova `Roots` (+ `Children` per riga) costruita da `Rebuild` in parallelo alla flat `Rows`, che resta la fonte di verità per selezione/shift/drag. `RowTemplateSelector` risolve i template per chiave via `FindResource` (autoreferenza dei gruppi annidati). Gli eventi di drag/click risalgono per bubbling all'ItemsControl esterno: behavior invariato.

**Tech Stack:** WPF + CommunityToolkit.Mvvm; xUnit/FluentAssertions.

**Spec:** `docs/superpowers/archive/specs/2026-06-12-group-row-restyle-design.md`

---

## Mappa file

| Azione | Path | Responsabilità |
|---|---|---|
| Modify | `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs` | `Children` collection |
| Modify | `src/Recordum.WPF/ViewModels/HomeViewModel.cs` | `Roots` + costruzione gerarchia in `Rebuild` |
| Modify | `src/Recordum.WPF/Views/RowTemplateSelector.cs` | risoluzione per chiave |
| Modify | `src/Recordum.WPF/Views/GroupRowTemplate.xaml` | cornice + chevron + ItemsControl ricorsivo |
| Modify | `src/Recordum.WPF/Views/HomeView.xaml` | ItemsControl esterno su `Roots`, selettore keyless |
| Test | `tests/Recordum.WPF.Tests/ViewModels/HomeViewModelTests.cs` (append) | gerarchia Roots/Children |

**Convenzioni**: build 0 warning nuovi; flaky noto ammesso se verde isolato; commit per task.

---

### Task 1: gerarchia `Roots`/`Children` (TDD)

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs`, `src/Recordum.WPF/ViewModels/HomeViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HomeViewModelTests.cs` (append)

- [x] **Step 1: test (RED)** — append a `HomeViewModelTests` (riusa `SampleRow`/`CreateSut`):

```csharp
    [Fact]
    public async Task Rebuild_builds_the_roots_hierarchy_in_parallel_with_flat_rows()
    {
        var g = Guid.NewGuid();
        var sub = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: g));
        _query.HomeRows.Add(SampleRow(name: "h1", parent: g));
        _query.HomeRows.Add(SampleRow(name: "Sub", type: null, id: sub, parent: g));
        _query.HomeRows.Add(SampleRow(name: "h2", parent: sub));
        _query.HomeRows.Add(SampleRow(name: "root"));
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        sut.Rows.Should().HaveCount(5);                       // flat invariata
        sut.Roots.Select(r => r.Name).Should().Equal("G", "root");
        var gRow = sut.Roots.Single(r => r.Name == "G");
        gRow.Children.Select(c => c.Name).Should().Equal("h1", "Sub");
        gRow.Children.Single(c => c.Name == "Sub").Children.Single().Name.Should().Be("h2");
        // stesse istanze nei due alberi (selezione/flag condivisi)
        gRow.Should().BeSameAs(sut.Rows.Single(r => r.Name == "G"));
    }

    [Fact]
    public async Task Collapsed_group_has_no_children_in_the_hierarchy()
    {
        var g = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "G", type: null, id: g));
        _query.HomeRows.Add(SampleRow(name: "h1", parent: g));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.Rows.Single(r => r.IsGroup).IsExpanded = false;

        sut.Roots.Should().ContainSingle();
        sut.Roots.Single().Children.Should().BeEmpty();
    }

    [Fact]
    public async Task NonActive_tabs_put_every_row_in_roots()
    {
        var g = Guid.NewGuid();
        _query.TrashRows.Add(SampleRow(name: "G", type: null, id: g));
        _query.TrashRows.Add(SampleRow(name: "h1", parent: g));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Trash);

        sut.Roots.Should().HaveCount(2);
        sut.Roots.Should().OnlyContain(r => r.Children.Count == 0);
    }
```

- [x] **Step 2: RED** — `dotnet test tests/Recordum.WPF.Tests --filter "Rebuild_builds_the_roots|Collapsed_group_has_no_children|NonActive_tabs_put_every"` (compile error: `Roots`/`Children`).

- [x] **Step 3: implementare**

`HomeRowViewModel` (accanto a `Rows`-like membri, es. dopo `CompletionsLast7Days`):

```csharp
    /// <summary>Visible children, populated by HomeViewModel.Rebuild (hierarchical view rendering).
    /// Empty for habits and for collapsed groups.</summary>
    public ObservableCollection<HomeRowViewModel> Children { get; } = new();
```

(using `System.Collections.ObjectModel` se manca.)

`HomeViewModel`:

```csharp
    /// <summary>Top-level rows for the hierarchical view (the frame around group children
    /// requires children to render INSIDE the group template). Built by Rebuild alongside
    /// the flat Rows, which stays the source of truth for selection/shift/drag.</summary>
    public ObservableCollection<HomeRowViewModel> Roots { get; } = new();
```

In `Rebuild()`: `Rows.Clear();` diventa `Rows.Clear(); Roots.Clear();` e il loop di costruzione tiene uno stack dei livelli — il corpo del `foreach` dopo `Rows.Add(vm);` aggiunge:

```csharp
                // Hierarchy mirror: level 0 → Roots, deeper rows → parent's Children.
                while (parentStack.Count > level) parentStack.RemoveAt(parentStack.Count - 1);
                if (level == 0) Roots.Add(vm);
                else parentStack[level - 1].Children.Add(vm);
                if (parentStack.Count == level) parentStack.Add(vm);
```

con, PRIMA del foreach (dentro il try, dopo `_rebuilding = true;`):

```csharp
            var parentStack = new List<HomeRowViewModel>();
```

NB: serve il `level` — il foreach è già `foreach (var (row, level) in OrderHierarchically(...))`. I row VM sono ricreati a ogni Rebuild, quindi `Children` parte sempre vuota: nessun clear per-riga necessario.

- [x] **Step 4: GREEN** — filtro step 2 + suite `HomeViewModel|HomeMultiSelection` completa (la flat non cambia → tutto resta verde).

- [x] **Step 5: commit** `feat(wpf): hierarchical Roots/Children mirror for the tree view`.

---

### Task 2: selettore per chiave

**Files:** Modify `src/Recordum.WPF/Views/RowTemplateSelector.cs`, `src/Recordum.WPF/Views/HomeView.xaml`

- [x] **Step 1**: `RowTemplateSelector` risolve per chiave (le property restano per compat finché HomeView non è aggiornata, poi si tolgono):

```csharp
using System.Windows;
using System.Windows.Controls;
using Recordum.WPF.ViewModels;

namespace Recordum.WPF.Views;

/// <summary>Resolves the row template by resource key from the container's scope, so the
/// group template can recursively select itself for nested groups (a self StaticResource
/// inside GroupRowTemplate.xaml would be unresolvable).</summary>
public sealed class RowTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        container is FrameworkElement fe
            ? (DataTemplate)fe.FindResource(item is HomeRowViewModel { IsGroup: true } ? "GroupRowTemplate" : "HabitRowTemplate")
            : null;
}
```

- [x] **Step 2**: in `HomeView.xaml` l'istanza del selettore (riga ~176) perde le property e si sposta nei Resources della UserControl per riuso; l'ItemsControl binda `Roots`:

nei `UserControl.Resources` (dopo i merged dictionaries):

```xml
        <v:RowTemplateSelector x:Key="RowSelector"/>
```

e il blocco ItemsControl del DataTrigger `Loaded` diventa:

```xml
                                        <ScrollViewer VerticalScrollBarVisibility="Auto">
                                            <ItemsControl ItemsSource="{Binding Roots}"
                                                          b:TreeDragDropBehavior.Enabled="True"
                                                          KeyboardNavigation.TabNavigation="Continue"
                                                          ItemTemplateSelector="{StaticResource RowSelector}"/>
                                        </ScrollViewer>
```

- [x] **Step 3**: build `dotnet build src/Recordum.WPF -v q` 0/0 (la GroupRowTemplate attuale funziona ancora: ignora Children); commit `refactor(wpf): key-based row template selector; tree renders from Roots`.

---

### Task 3: GroupRowTemplate — cornice + chevron + ricorsione

**Files:** Modify `src/Recordum.WPF/Views/GroupRowTemplate.xaml`

- [x] **Step 1**: riscrivere il DataTemplate:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:v="clr-namespace:Recordum.WPF.Views"
                    xmlns:vm="clr-namespace:Recordum.WPF.ViewModels"
                    xmlns:res="clr-namespace:Recordum.WPF.Resources">

    <!-- Chevron expander: chrome-free toggle, ▸ rotating to ▾ when expanded. -->
    <Style x:Key="Group.Chevron" TargetType="ToggleButton">
        <Setter Property="Width" Value="22"/>
        <Setter Property="Height" Value="22"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ToggleButton">
                    <Border x:Name="bd" Background="Transparent" CornerRadius="4">
                        <Path x:Name="icon" Width="13" Height="13" Stretch="Uniform"
                              Data="{StaticResource Icon.ChevronRight}"
                              Stroke="{DynamicResource Brush.TextMuted}" StrokeThickness="1.8"
                              StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
                              RenderTransformOrigin="0.5,0.5">
                            <Path.RenderTransform>
                                <RotateTransform x:Name="rot" Angle="0"/>
                            </Path.RenderTransform>
                        </Path>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="rot" Property="Angle" Value="90"/>
                            <Setter TargetName="icon" Property="Stroke" Value="{DynamicResource Brush.Text}"/>
                        </Trigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <DataTemplate x:Key="GroupRowTemplate" DataType="{x:Type vm:HomeRowViewModel}">
        <!-- BC3 container frame: when expanded with children, card + children live inside a
             rounded border tinted with the group color; collapsed → just the card. -->
        <Border x:Name="Frame" CornerRadius="8" BorderThickness="1" Margin="0,1"
                BorderBrush="Transparent" Background="Transparent" Padding="3">
            <StackPanel>
                <Border Style="{StaticResource Recordum.HabitBlock}" Margin="0">
                    <Grid Height="{StaticResource Layout.RowHeight}">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>

                        <ToggleButton Grid.Column="0" Style="{StaticResource Group.Chevron}"
                                      Margin="4,0,0,0" VerticalAlignment="Center"
                                      IsChecked="{Binding IsExpanded, Mode=TwoWay}" />

                        <Rectangle Grid.Column="1"
                                   Width="{StaticResource Layout.AccentBarWidth}"
                                   Opacity="0.6"
                                   Fill="{Binding ColorHex, Converter={StaticResource HexToBrush}}" />

                        <TextBlock Grid.Column="2"
                                   Margin="8,0,0,0"
                                   VerticalAlignment="Center"
                                   FontSize="{StaticResource FontSize.Body}"
                                   FontWeight="SemiBold"
                                   Foreground="{DynamicResource Brush.Text}"
                                   Text="{Binding Name}" />

                        <TextBlock Grid.Column="3"
                                   Margin="0,0,12,0"
                                   VerticalAlignment="Center"
                                   FontSize="{StaticResource FontSize.Small}"
                                   Opacity="0.5"
                                   Foreground="{DynamicResource Brush.TextMuted}"
                                   Text="{Binding ChildCount, StringFormat={x:Static res:Strings.Home_GroupChildCount_Format}}" />

                        <Grid.ContextMenu>
                            <ContextMenu DataContext="{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}">
                                <MenuItem Header="{x:Static res:Strings.Home_ContextMenu_Rename}"
                                          Command="{Binding BeginRenameCommand}"
                                          Visibility="{Binding ShowRenameMenu, Converter={StaticResource BoolToVisible}}" />
                                <MenuItem Header="{x:Static res:Strings.Home_ContextMenu_Archive}"
                                          Command="{Binding ArchiveCommand}"
                                          Visibility="{Binding ShowArchive, Converter={StaticResource BoolToVisible}}" />
                                <MenuItem Header="{x:Static res:Strings.Home_ContextMenu_Unarchive}"
                                          Command="{Binding UnarchiveCommand}"
                                          Visibility="{Binding ShowUnarchive, Converter={StaticResource BoolToVisible}}" />
                                <MenuItem Header="{x:Static res:Strings.Home_ContextMenu_SoftDelete}"
                                          Command="{Binding SoftDeleteCommand}"
                                          Visibility="{Binding ShowSoftDelete, Converter={StaticResource BoolToVisible}}" />
                                <MenuItem Header="{x:Static res:Strings.Home_ContextMenu_Restore}"
                                          Command="{Binding RestoreCommand}"
                                          Visibility="{Binding ShowRestore, Converter={StaticResource BoolToVisible}}" />
                                <MenuItem Header="{x:Static res:Strings.Home_ContextMenu_Purge}"
                                          Command="{Binding PurgeCommand}"
                                          Visibility="{Binding ShowPurge, Converter={StaticResource BoolToVisible}}" />
                            </ContextMenu>
                        </Grid.ContextMenu>
                    </Grid>
                </Border>

                <!-- Children, indented inside the frame; recursive for nested groups. -->
                <ItemsControl x:Name="ChildList"
                              ItemsSource="{Binding Children}"
                              Margin="20,2,2,1"
                              Visibility="Collapsed">
                    <ItemsControl.ItemTemplateSelector>
                        <v:RowTemplateSelector/>
                    </ItemsControl.ItemTemplateSelector>
                </ItemsControl>
            </StackPanel>
        </Border>

        <DataTemplate.Triggers>
            <MultiDataTrigger>
                <MultiDataTrigger.Conditions>
                    <Condition Binding="{Binding IsExpanded}" Value="True"/>
                    <Condition Binding="{Binding HasVisibleChildren}" Value="True"/>
                </MultiDataTrigger.Conditions>
                <Setter TargetName="ChildList" Property="Visibility" Value="Visible"/>
                <Setter TargetName="Frame" Property="BorderBrush"
                        Value="{Binding ColorHex, Converter={StaticResource HabitTint}, ConverterParameter=80}"/>
                <Setter TargetName="Frame" Property="Background"
                        Value="{Binding ColorHex, Converter={StaticResource HabitTint}, ConverterParameter=10}"/>
            </MultiDataTrigger>
        </DataTemplate.Triggers>
    </DataTemplate>
</ResourceDictionary>
```

NB:
- `HasVisibleChildren`: aggiungere a `HomeRowViewModel` una property `public bool HasVisibleChildren => Children.Count > 0;` notificata sottoscrivendo `Children.CollectionChanged` nel ctor (`Children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasVisibleChildren));`). I figli sono popolati subito dopo la costruzione in `Rebuild`, quindi la notifica serve.
- Verificare che `HabitTint` accetti il parametro come stringa numerica alpha (è usato con `26`/`51` in App.xaml): per la cornice usare `80` (bordo) e `10` (velo) — se la resa fosse troppo forte/debole, aggiustare i due valori.
- `Layout.RowHeight`, `Layout.AccentBarWidth`, `Recordum.HabitBlock`, `HexToBrush`, `BoolToVisible` già in scope applicativo.

- [x] **Step 2**: build 0/0 + suite WPF completa verde.

- [x] **Step 3: commit** `style(wpf): group rows — chevron + BC3 container frame with indented children`.

---

### Task 4: verifica visiva + tracker

- [ ] skill `run` + checklist: chevron ▸/▾ ruota e niente più quadrato; gruppo chiuso = card pulita con conteggio; aperto = cornice nel colore del gruppo con figli indentati; gruppi annidati = cornici annidate; drag-drop dentro la cornice funziona e l'highlight drop-target resta visibile; selezione/multi-selezione sulle righe interne; click-a-vuoto; collasso/espansione fluidi; due temi.
- [x] aggiornare `docs/phase-4-issues.md` + memoria; commit docs.

---

## Self-review

- Spec §1 (chevron, grassetto, conteggio, cornice, indent, annidati)→T3; §2 (Roots/Children, selettore per chiave, eventi bubbling)→T1/T2; §4 testing→T1 + T4 visivo. Coverage ok.
- `Rows` flat intatta ⇒ selezione/shift/drag/`SelectById` non cambiano (suite esistente li copre).
- Trigger cornice usa `HasVisibleChildren` (non `ChildCount`): un gruppo con figli ma collassato non mostra cornice, e un gruppo i cui figli sono tutti filtrati via (search/type filter) nemmeno.
- Tipi coerenti: `Roots`, `Children`, `HasVisibleChildren`, selettore keyless `RowTemplateSelector`.
- Rischio noto: il `ContextMenu` era sul Grid interno — resta lì, invariato.
