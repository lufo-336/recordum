# Workspace a colonne-toggle (nav-model parte 1) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Le viste di una habit (Cronologia/Dettaglio/Note/Configura) diventano colonne affiancate aperte/chiuse da chip toggle, al posto delle 3 tab + pannello editor separato; set di colonne persistito e mantenuto al cambio habit.

**Architecture:** Lo stato colonne vive in `HabitScreenViewModel` come 4 bool osservabili + ordine-di-apertura interno (per Esc) + persistenza CSV su AppSetting `ui.open_columns`. La view sostituisce il `TabControl` con una chip-bar + 4 colonne fisse in `ScrollViewer` orizzontale, ognuna con header e ✕; la colonna Configura ospita l'attuale `EditorPane` (che esce da `MainWindow` insieme a splitter e larghezza persistita `ui.editor_pane_width`). `HabitEditorViewModel` passa di proprietà a `HabitScreenViewModel` (istanza unica); `MainWindowViewModel.Editor` diventa un passthrough.

**Tech Stack:** WPF + CommunityToolkit.Mvvm; xUnit/FluentAssertions; nessuna migrazione (AppSetting key nuova creata on-write).

**Spec:** `docs/superpowers/archive/specs/2026-05-29-navigation-model-design.md` §3–§4, §7 (la parte gruppi §3.2/§5 è il piano successivo).

---

## Mappa file

| Azione | Path | Responsabilità |
|---|---|---|
| Create | `src/Recordum.WPF/ViewModels/HabitScreen/HabitColumn.cs` | enum colonne |
| Modify | `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` | stato colonne + persistenza + Editor di proprietà (ctor +2: `IAppSettingService`, `HabitEditorViewModel`) |
| Modify | `src/Recordum.WPF/ViewModels/MainWindowViewModel.cs` | via EditorPaneWidth; `Editor` passthrough; ctor −1 |
| Modify | `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` | chip-bar + host colonne (via TabControl) |
| Modify | `src/Recordum.WPF/MainWindow.xaml(.cs)` | via colonna/splitter/animazione EditorPane; Esc chiude ultima colonna |
| Modify | `src/Recordum.WPF/Resources/Strings(.resx/.it.resx/.Designer.cs)` | stringa colonna "Cronologia" |
| Modify | `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` | nessun cambio atteso (DI risolve il nuovo ctor) — verificare |
| Test | `tests/.../ViewModels/HabitScreen/HabitScreenColumnsTests.cs` (new) + fixture/factory esistenti aggiornate | spec §7 |

**Convenzioni**: build 0 warning; flaky noto `CompletionRecorded_event_reloads…` (verificare isolato); commit per task.

---

### Task 1: stringa "Cronologia"

**Files:** Modify `Strings.resx`, `Strings.it.resx`, `Strings.Designer.cs`

- [x] **Step 1**: EN — dopo `HabitScreen.Tab.Abitudine` (cercare la chiave esatta esistente):

```xml
  <data name="HabitScreen.Column.Cronologia" xml:space="preserve">
    <value>Log</value>
  </data>
```

IT:

```xml
  <data name="HabitScreen.Column.Cronologia" xml:space="preserve">
    <value>Cronologia</value>
  </data>
```

Designer (accanto alle property `HabitScreen_Tab_*`):

```csharp
    public static string HabitScreen_Column_Cronologia => Get("HabitScreen.Column.Cronologia");
```

Le altre colonne riusano `HabitScreen_Tab_Detail`, `HabitScreen_Tab_Notes`, `HabitScreen_Configure`.

- [x] **Step 2**: build 0/0, commit `feat(wpf): resx — Cronologia column label`.

---

### Task 2: stato colonne in `HabitScreenViewModel` (TDD)

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/HabitColumn.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/HabitScreenColumnsTests.cs` (new)

- [x] **Step 1: enum**

```csharp
namespace Recordum.WPF.ViewModels.HabitScreen;

public enum HabitColumn { Cronologia, Dettaglio, Note, Configura }
```

- [x] **Step 2: test (RED)** — il file di test costruisce il SUT come gli altri test di HabitScreen (cercare l'helper esistente in `HabitScreenViewModelTests` e riusarne la costruzione, aggiungendo i 2 nuovi parametri: `FakeAppSettingService` e un `HabitEditorViewModel` costruito coi fake già usati in `MainWindowViewModelTests`):

```csharp
using FluentAssertions;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitScreenColumnsTests
{
    // CreateSut(): replica la costruzione usata in HabitScreenViewModelTests + FakeAppSettingService
    // condiviso esposto per le assert di persistenza. Vedi nota sopra.

    [Fact]
    public async Task First_load_with_no_columns_opens_cronologia()
    {
        var (sut, _, nodes) = CreateSut();
        var id = AddHabit(nodes);

        await sut.LoadAsync(id);

        sut.IsCronologiaOpen.Should().BeTrue();
        sut.IsDettaglioOpen.Should().BeFalse();
        sut.IsNoteOpen.Should().BeFalse();
        sut.IsConfiguraOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Open_set_survives_switching_habit()
    {
        var (sut, _, nodes) = CreateSut();
        var a = AddHabit(nodes); var b = AddHabit(nodes);
        await sut.LoadAsync(a);
        sut.IsNoteOpen = true;

        await sut.LoadAsync(b);

        sut.IsCronologiaOpen.Should().BeTrue();
        sut.IsNoteOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Toggle_persists_csv_roundtrip()
    {
        var (sut, settings, nodes) = CreateSut();
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);
        sut.IsDettaglioOpen = true;

        (await settings.GetAsync("ui.open_columns")).Should().Be("Cronologia,Dettaglio");

        // round-trip: un nuovo SUT con lo stesso store riparte con lo stesso set
        var (sut2, _, nodes2) = CreateSut(settings);
        var id2 = AddHabit(nodes2);
        await sut2.RestoreOpenColumnsAsync();
        await sut2.LoadAsync(id2);
        sut2.IsDettaglioOpen.Should().BeTrue();
        sut2.IsCronologiaOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Opening_configura_raises_ConfigureRequested()
    {
        var (sut, _, nodes) = CreateSut();
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);
        var raised = 0;
        sut.ConfigureRequested += (_, _) => raised++;

        sut.IsConfiguraOpen = true;

        raised.Should().Be(1);
        sut.IsConfiguraOpen.Should().BeTrue();
    }

    [Fact]
    public async Task CloseLastColumn_closes_most_recently_opened()
    {
        var (sut, _, nodes) = CreateSut();
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);   // Cronologia
        sut.IsNoteOpen = true;     // poi Note
        sut.IsDettaglioOpen = true; // poi Dettaglio

        sut.CloseLastColumn();
        sut.IsDettaglioOpen.Should().BeFalse();
        sut.CloseLastColumn();
        sut.IsNoteOpen.Should().BeFalse();
        sut.IsCronologiaOpen.Should().BeTrue();
    }
}
```

- [x] **Step 3: RED** — `dotnet test ... --filter HabitScreenColumns` (errori di compilazione attesi).

- [x] **Step 4: implementare** in `HabitScreenViewModel`:

```csharp
    internal const string OpenColumnsSettingKey = "ui.open_columns";
    private readonly List<HabitColumn> _openOrder = [];
    private bool _restoringColumns;

    public HabitEditorViewModel Editor { get; }   // ctor: nuovo parametro, ThrowIfNull, assegnazione

    [ObservableProperty] private bool _isCronologiaOpen;
    [ObservableProperty] private bool _isDettaglioOpen;
    [ObservableProperty] private bool _isNoteOpen;
    [ObservableProperty] private bool _isConfiguraOpen;

    partial void OnIsCronologiaOpenChanged(bool value) => OnColumnFlag(HabitColumn.Cronologia, value);
    partial void OnIsDettaglioOpenChanged(bool value) => OnColumnFlag(HabitColumn.Dettaglio, value);
    partial void OnIsNoteOpenChanged(bool value) => OnColumnFlag(HabitColumn.Note, value);
    partial void OnIsConfiguraOpenChanged(bool value)
    {
        OnColumnFlag(HabitColumn.Configura, value);
        if (value && !_restoringColumns) ConfigureRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnColumnFlag(HabitColumn column, bool open)
    {
        if (open) { if (!_openOrder.Contains(column)) _openOrder.Add(column); }
        else _openOrder.Remove(column);
        if (!_restoringColumns) _ = PersistOpenColumnsAsync();
    }

    private Task PersistOpenColumnsAsync()
        => _appSettings.SetAsync(OpenColumnsSettingKey,
            string.Join(",", CanonicalOpen().Select(c => c.ToString()))).AsTask();

    private IEnumerable<HabitColumn> CanonicalOpen()
    {
        if (IsCronologiaOpen) yield return HabitColumn.Cronologia;
        if (IsDettaglioOpen) yield return HabitColumn.Dettaglio;
        if (IsNoteOpen) yield return HabitColumn.Note;
        if (IsConfiguraOpen) yield return HabitColumn.Configura;
    }

    /// <summary>Restores the persisted open-column set (startup, before the first LoadAsync).</summary>
    public async Task RestoreOpenColumnsAsync(CancellationToken ct = default)
    {
        var raw = await _appSettings.GetAsync(OpenColumnsSettingKey, ct).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(raw)) return;
        _restoringColumns = true;
        try
        {
            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (Enum.TryParse<HabitColumn>(token, out var c))
                    SetFlag(c, true);
        }
        finally { _restoringColumns = false; }
    }

    private void SetFlag(HabitColumn c, bool v)
    {
        switch (c)
        {
            case HabitColumn.Cronologia: IsCronologiaOpen = v; break;
            case HabitColumn.Dettaglio: IsDettaglioOpen = v; break;
            case HabitColumn.Note: IsNoteOpen = v; break;
            case HabitColumn.Configura: IsConfiguraOpen = v; break;
        }
    }

    [RelayCommand]
    private void ToggleColumn(HabitColumn column)
    {
        switch (column)
        {
            case HabitColumn.Cronologia: IsCronologiaOpen = !IsCronologiaOpen; break;
            case HabitColumn.Dettaglio: IsDettaglioOpen = !IsDettaglioOpen; break;
            case HabitColumn.Note: IsNoteOpen = !IsNoteOpen; break;
            case HabitColumn.Configura: IsConfiguraOpen = !IsConfiguraOpen; break;
        }
    }

    /// <summary>Esc: closes the most recently opened column.</summary>
    public void CloseLastColumn()
    {
        if (_openOrder.Count == 0) return;
        SetFlag(_openOrder[^1], false);
    }
```

In `LoadAsync` (ramo `habitNodeId != null`, dopo l'assegnazione di `Node`): se nessuna colonna aperta → `IsCronologiaOpen = true;` (default spec §3.7). `OpenConfigure` (command esistente) diventa `IsConfiguraOpen = true;` (l'evento parte dal partial). Ctor: aggiungere `IAppSettingService appSettings` e `HabitEditorViewModel editor` (campo `_appSettings`).

- [x] **Step 5**: aggiornare TUTTE le costruzioni di `HabitScreenViewModel` (HabitScreenViewModelTests helper, `MainWindowViewModelTests.CreateSut`, `MainWindowViewModelFactory`, eventuali altre — `grep -rn "new HabitScreenViewModel" tests src`) coi 2 nuovi parametri.

- [x] **Step 6: GREEN** — filtro `HabitScreenColumns` + suite HabitScreen completa.

- [x] **Step 7**: commit `feat(wpf): HabitScreenViewModel column-toggle state with persistence`.

---

### Task 3: `MainWindowViewModel` — Editor passthrough, via EditorPaneWidth (TDD sugli esistenti)

**Files:** Modify `MainWindowViewModel.cs`; Test: aggiornare `MainWindowViewModelTests`

- [x] **Step 1**: rimuovere da `MainWindowViewModel`: costanti/property/Load/Commit di `EditorPaneWidth` (`EditorPaneWidthSettingKey`, `DefaultEditorPaneWidth`, `MinEditorPaneWidth`, `MaxEditorPaneWidth`, `_editorPaneWidth`, `LoadEditorPaneWidthAsync`, `CommitEditorPaneWidthAsync` e la chiamata in `InitializeAsync`); parametro ctor `HabitEditorViewModel editor` e property `Editor` → sostituire con passthrough:

```csharp
    public HabitEditorViewModel Editor => HabitScreen.Editor;
```

(`OnHomePropertyChanged` e `OnHabitScreenConfigureRequested` continuano a funzionare sul passthrough.)

- [x] **Step 2**: test — eliminare i test su EditorPaneWidth (cercare `EditorPaneWidth` nel file) e togliere il parametro editor dalle costruzioni; RED→GREEN su filtro `MainWindowViewModel`.

- [x] **Step 3**: commit `refactor(wpf): editor owned by HabitScreen; drop editor pane width plumbing`.

---

### Task 4: HabitScreen.xaml — chip-bar + colonne

**Files:** Modify `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` (+ `.xaml.cs` se servono using)

- [x] **Step 1**: sostituire l'intero blocco `<TabControl Grid.Row="3" …>…</TabControl>` con:

```xml
        <Grid Grid.Row="3">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- Chip bar: view toggles (nav-model). Chips use the shared Segment pill but as
                 independent toggles (not radio): IsChecked two-way to the per-column flags. -->
            <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="12,8,12,4">
                <CheckBox Style="{StaticResource Recordum.SegmentCheck}"
                          Content="{x:Static res:Strings.HabitScreen_Column_Cronologia}"
                          IsChecked="{Binding IsCronologiaOpen}"/>
                <CheckBox Style="{StaticResource Recordum.SegmentCheck}" Margin="4,0,0,0"
                          Content="{x:Static res:Strings.HabitScreen_Tab_Detail}"
                          IsChecked="{Binding IsDettaglioOpen}"/>
                <CheckBox Style="{StaticResource Recordum.SegmentCheck}" Margin="4,0,0,0"
                          Content="{x:Static res:Strings.HabitScreen_Tab_Notes}"
                          IsChecked="{Binding IsNoteOpen}"/>
                <CheckBox Style="{StaticResource Recordum.SegmentCheck}" Margin="4,0,0,0"
                          Content="{x:Static res:Strings.HabitScreen_Configure}"
                          IsChecked="{Binding IsConfiguraOpen}"/>
            </StackPanel>

            <!-- Open columns, side by side, horizontal scroll when they overflow (spec §3.8). -->
            <ScrollViewer Grid.Row="1" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled">
                <StackPanel Orientation="Horizontal">

                    <Border Width="460" Style="{StaticResource HabitScreen.Column}"
                            Visibility="{Binding IsCronologiaOpen, Converter={StaticResource BoolToVisible}}">
                        <DockPanel>
                            <Grid DockPanel.Dock="Top" Style="{StaticResource HabitScreen.ColumnHeader}">
                                <TextBlock Text="{x:Static res:Strings.HabitScreen_Column_Cronologia}" Style="{StaticResource HabitScreen.ColumnTitle}"/>
                                <Button Style="{StaticResource HabitScreen.ColumnClose}"
                                        Command="{Binding ToggleColumnCommand}"
                                        CommandParameter="{x:Static vm:HabitColumn.Cronologia}"/>
                            </Grid>
                            <ScrollViewer VerticalScrollBarVisibility="Auto">
                                <StackPanel>
                                    <cal:CalendarNavigator DataContext="{Binding Calendar}"/>
                                    <ContentControl Content="{Binding QuickAdd}"
                                                    ContentTemplateSelector="{StaticResource QuickAddSelector}"/>
                                    <ContentControl Content="{Binding TimerControl}"/>
                                    <v:DayNoteView DataContext="{Binding DayNote}"/>
                                    <StackPanel Margin="12,4,12,12">
                                        <TextBlock Text="{x:Static res:Strings.HabitScreen_DayRecords}"
                                                   FontWeight="SemiBold"
                                                   Foreground="{DynamicResource Brush.Text}"
                                                   Margin="0,0,0,2"/>
                                        <TextBlock Text="{x:Static res:Strings.HabitScreen_DayRecords_Empty}"
                                                   Foreground="{DynamicResource Brush.TextMuted}"
                                                   FontSize="{StaticResource FontSize.Small}"
                                                   Visibility="{Binding DayCompletions.Rows.Count, Converter={StaticResource ZeroToVisible}}"/>
                                        <ContentControl Content="{Binding DayCompletions}"/>
                                    </StackPanel>
                                </StackPanel>
                            </ScrollViewer>
                        </DockPanel>
                    </Border>

                    <Border Width="520" Style="{StaticResource HabitScreen.Column}"
                            Visibility="{Binding IsDettaglioOpen, Converter={StaticResource BoolToVisible}}">
                        <DockPanel>
                            <Grid DockPanel.Dock="Top" Style="{StaticResource HabitScreen.ColumnHeader}">
                                <TextBlock Text="{x:Static res:Strings.HabitScreen_Tab_Detail}" Style="{StaticResource HabitScreen.ColumnTitle}"/>
                                <Button Style="{StaticResource HabitScreen.ColumnClose}"
                                        Command="{Binding ToggleColumnCommand}"
                                        CommandParameter="{x:Static vm:HabitColumn.Dettaglio}"/>
                            </Grid>
                            <ContentControl Content="{Binding Detail}"/>
                        </DockPanel>
                    </Border>

                    <Border Width="460" Style="{StaticResource HabitScreen.Column}"
                            Visibility="{Binding IsNoteOpen, Converter={StaticResource BoolToVisible}}">
                        <DockPanel>
                            <Grid DockPanel.Dock="Top" Style="{StaticResource HabitScreen.ColumnHeader}">
                                <TextBlock Text="{x:Static res:Strings.HabitScreen_Tab_Notes}" Style="{StaticResource HabitScreen.ColumnTitle}"/>
                                <Button Style="{StaticResource HabitScreen.ColumnClose}"
                                        Command="{Binding ToggleColumnCommand}"
                                        CommandParameter="{x:Static vm:HabitColumn.Note}"/>
                            </Grid>
                            <ContentControl Content="{Binding Notes}"/>
                        </DockPanel>
                    </Border>

                    <Border Width="480" Style="{StaticResource HabitScreen.Column}"
                            Visibility="{Binding IsConfiguraOpen, Converter={StaticResource BoolToVisible}}">
                        <DockPanel>
                            <Grid DockPanel.Dock="Top" Style="{StaticResource HabitScreen.ColumnHeader}">
                                <TextBlock Text="{x:Static res:Strings.HabitScreen_Configure}" Style="{StaticResource HabitScreen.ColumnTitle}"/>
                                <Button Style="{StaticResource HabitScreen.ColumnClose}"
                                        Command="{Binding ToggleColumnCommand}"
                                        CommandParameter="{x:Static vm:HabitColumn.Configura}"/>
                            </Grid>
                            <ve:EditorPane DataContext="{Binding Editor}"/>
                        </DockPanel>
                    </Border>

                </StackPanel>
            </ScrollViewer>
        </Grid>
```

Namespace: aggiungere a HabitScreen.xaml `xmlns:ve="clr-namespace:Recordum.WPF.Views"` se `EditorPane` non è già raggiungibile (è `Recordum.WPF.Views.EditorPane` — verificare il namespace esatto in `EditorPane.xaml.cs`). NB: `EditorPane.xaml` potrebbe avere larghezze/chrome da pannello standalone — verificare e neutralizzare (la colonna fornisce già header e larghezza).

- [x] **Step 2**: stili locali in `UserControl.Resources` (al posto di `Recordum.HabitTab`, che si elimina):

```xml
        <!-- Column chrome (nav-model). -->
        <Style x:Key="HabitScreen.Column" TargetType="Border">
            <Setter Property="BorderThickness" Value="0,0,1,0"/>
            <Setter Property="BorderBrush" Value="{DynamicResource Brush.Border}"/>
        </Style>
        <Style x:Key="HabitScreen.ColumnHeader" TargetType="Grid">
            <Setter Property="Height" Value="30"/>
            <Setter Property="Background" Value="{DynamicResource Brush.Surface}"/>
        </Style>
        <Style x:Key="HabitScreen.ColumnTitle" TargetType="TextBlock">
            <Setter Property="Margin" Value="12,0"/>
            <Setter Property="VerticalAlignment" Value="Center"/>
            <Setter Property="FontSize" Value="{StaticResource FontSize.Small}"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Foreground" Value="{DynamicResource Brush.TextMuted}"/>
        </Style>
        <Style x:Key="HabitScreen.ColumnClose" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Content" Value="✕"/>
            <Setter Property="HorizontalAlignment" Value="Right"/>
            <Setter Property="Width" Value="24"/>
            <Setter Property="Height" Value="22"/>
            <Setter Property="Margin" Value="0,0,6,0"/>
            <Setter Property="Padding" Value="0"/>
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="FontSize" Value="11"/>
        </Style>
```

e in `BaseStyles.xaml` la variante CheckBox del segment (le chip sono toggle indipendenti, non radio):

```xml
    <!-- Segment pill as independent toggle (chip bar of the column workspace). -->
    <Style x:Key="Recordum.SegmentCheck" TargetType="{x:Type CheckBox}">
        <Setter Property="Foreground" Value="{DynamicResource Brush.TextMuted}"/>
        <Setter Property="Padding" Value="11,4"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type CheckBox}">
                    <Border x:Name="bd" CornerRadius="4" Padding="{TemplateBinding Padding}"
                            Background="Transparent" BorderThickness="1" BorderBrush="Transparent">
                        <ContentPresenter VerticalAlignment="Center" RecognizesAccessKey="True"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                        </Trigger>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                            <Setter TargetName="bd" Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                            <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/>
                            <Setter Property="FontWeight" Value="SemiBold"/>
                        </Trigger>
                        <Trigger Property="IsKeyboardFocused" Value="True">
                            <Setter TargetName="bd" Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
```

Rimuovere anche il bottone "Configura" dall'header del soggetto (riga ~585: ora c'è la chip) lasciando Esporta.

- [x] **Step 3**: build 0/0; commit `feat(wpf): HabitScreen column workspace — chip bar + toggle columns`.

---

### Task 5: MainWindow — via EditorPane, Esc

**Files:** Modify `MainWindow.xaml`, `MainWindow.xaml.cs`

- [x] **Step 1**: in `MainWindow.xaml` rimuovere `EditorPaneColumn` (ColumnDefinition), `EditorPaneSplitter`, `<v:EditorPane …>`; la griglia workspace resta `[Home * | splitter | HabitScreenColumn]`.
- [x] **Step 2**: in `MainWindow.xaml.cs` rimuovere: `OnEditorPropertyChanged` (e la sottoscrizione `viewModel.Editor.PropertyChanged += …` nel ctor), `ApplyEditorPaneWidth`, `OnEditorPaneSplitterDragCompleted`, ogni riferimento a `EditorPaneColumn/EditorPaneContent`. ATTENZIONE: l'apertura del workspace su selezione (`AnimateColumn(HabitScreenColumn, …)`) resta invariata.
- [x] **Step 3**: Esc in `OnWindowPreviewKeyDown` (prima del check Ctrl):

```csharp
        if (e.Key == Key.Escape && _viewModel.Home.SelectedRow is not null)
        {
            _viewModel.HabitScreen.CloseLastColumn();
            e.Handled = true;
            return;
        }
```

- [x] **Step 4**: build 0/0 + suite completa; commit `feat(wpf): drop the standalone editor pane; Esc closes the last open column`.

---

### Task 6: verifica visiva + tracker

- [x] skill `run` + checklist (utente: ok 2026-06-11): chip toggle aprono/chiudono colonne (ordine canonico stabile); default Cronologia; ✕ e Esc; set mantenuto cambiando habit (rebind contenuti); persistenza tra riavvii; Configura carica l'editor (progressive disclosure intatta, autosave ok); scroll orizzontale con 3+ colonne; click-a-vuoto deseleziona e chiude il workspace; export funziona; due temi.
- [x] aggiornare `docs/phase-4-issues.md` + memoria (nav-model parte 1 fatta; parte 2 = gruppi-basi ＋Nuova▾ + drag-drop evidente); commit docs.

---

## Self-review

- Spec §3.3 (chip-bar+colonne)→T4; §3.4 (4 colonne, contenuti riusati)→T4; §3.5 (✕/Esc/click-a-vuoto: i primi due T4/T5, il terzo esiste già)→ok; §3.6 (rebind)→T2 (LoadAsync non tocca i flag) + test; §3.7 (default+persistenza+gancio preferenza futura)→T2; §3.8 (min-width+scroll orizzontale)→T4 (larghezze fisse per colonna = min-width di fatto); §4 (EditorPane via da MainWindow, editor come colonna, stato in HabitScreenViewModel, persistenza AppSetting)→T2/T3/T4/T5; §7 test→T2 (toggle/default/rebind/persistenza; deselezione già coperta dai test esistenti di LoadAsync(null)).
- Fuori scope (piano successivo): §3.2 ＋Nuova▾/gruppi, §5; larghezze colonna ridimensionabili (§8, esplicitamente rinviato).
- Tipi coerenti: `HabitColumn`, flag `Is*Open`, `ToggleColumnCommand`, `CloseLastColumn`, `RestoreOpenColumnsAsync`, chiave `ui.open_columns`.
- Rischio: `MainWindowViewModel.Editor` passthrough valutato nel ctor? No: property-get su HabitScreen — ok perché HabitScreen è assegnato prima. La sottoscrizione `Editor.PropertyChanged` nel code-behind si rimuove (T5) PRIMA che diventi rotta? T3 (VM) la lascia compilante perché la property esiste ancora; il code-behind l'agganciava per l'animazione del pane: va rimossa in T5 — se la suite/build di T3 si rompesse sul code-behind, anticipare quella singola rimozione a T3 (annotato).
