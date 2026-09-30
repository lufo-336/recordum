# Guscio: title bar custom + ribbon + status bar (Fase B rimodernizzazione UI) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire la barra superiore testuale di `MainWindow` con una title bar custom (WindowChrome) + una ribbon verticale a icone Lucide (workspace, azioni, sistema), e compattare la `TimerStatusBar`.

**Architecture:** Nessuna logica VM nuova: la ribbon si lega ai command esistenti di `MainWindowViewModel` (`ShowHome/ShowCharts/BackupCreate/BackupRestore/ShowBackupHistory/ExportLibrary/ShowDiagnosticInfo/ShowSettings`). `MainWindow` passa a righe `[titlebar 36px / contenuto / statusbar]`, contenuto a colonne `[ribbon 44px | workspaces]`. Title bar via `WindowChrome` (caption drag, snap nativi, bottoni custom). L'icona Backup della ribbon apre un ContextMenu (Crea/Ripristina/Cronologia) perché restore e cronologia perdono il menu testuale — **rifinitura concordabile della variante B** (nessun punto d'ingresso perso).

**Tech Stack:** WPF `System.Windows.Shell.WindowChrome`, `SystemCommands`, icone `Geometry` in `Icons.xaml`, stili in-file per i bottoni ribbon/caption.

**Spec:** `docs/superpowers/archive/specs/2026-06-11-ui-modernization-plan-design.md` §Fase B.

---

## Mappa file

| Azione | Path | Responsabilità |
|---|---|---|
| Modify | `src/Recordum.WPF/Resources/Icons.xaml` | +3 geometrie (BarChart3, Upload, CircleHelp); `Icon.Settings` completato a ingranaggio pieno |
| Create | `src/Recordum.WPF/Views/WorkspaceRibbon.xaml(.cs)` | colonna icone: workspace + azioni + sistema |
| Modify | `src/Recordum.WPF/MainWindow.xaml` | WindowChrome, title bar, ribbon, rimozione barra/menu testuali |
| Modify | `src/Recordum.WPF/MainWindow.xaml.cs` | handler caption buttons + fix margine maximize |
| Modify | `src/Recordum.WPF/Views/Timer/TimerStatusBar.xaml` | restyle compatto |

Nessun test nuovo: zero logica nuova (command esistenti già coperti; `ClampToWorkArea` già testato). Verifica = build 0/0 + suite invariata + visiva.

---

### Task 1: Icone

**Files:** Modify `src/Recordum.WPF/Resources/Icons.xaml`

- [ ] **Step 1**: nella sezione chrome, sostituire la geometria di `Icon.Settings` (oggi solo il cerchio interno) con l'ingranaggio Lucide completo:

```xml
    <Geometry x:Key="Icon.Settings">M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z</Geometry>
```

- [ ] **Step 2**: aggiungere dopo `Icon.Sparkles`:

```xml
    <Geometry x:Key="Icon.BarChart3">M3 3v16a2 2 0 0 0 2 2h16 M18 17V9 M13 17V5 M8 17v-3</Geometry>
    <Geometry x:Key="Icon.Upload">M12 3v12 M17 8l-5-5-5 5 M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4</Geometry>
    <Geometry x:Key="Icon.CircleHelp">M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3 M12 17h.01</Geometry>
```

- [ ] **Step 3**: build 0/0, commit `feat(wpf): ribbon chrome icons (full gear, bar-chart, upload, circle-help)`.

---

### Task 2: `WorkspaceRibbon`

**Files:** Create `src/Recordum.WPF/Views/WorkspaceRibbon.xaml`, `WorkspaceRibbon.xaml.cs`

- [ ] **Step 1**: XAML completo (DataContext = `MainWindowViewModel` ereditato):

```xml
<UserControl x:Class="Recordum.WPF.Views.WorkspaceRibbon"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="clr-namespace:Recordum.WPF.ViewModels"
             xmlns:res="clr-namespace:Recordum.WPF.Resources"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance vm:MainWindowViewModel, IsDesignTimeCreatable=False}"
             Width="44">
    <UserControl.Resources>
        <!-- Icona ribbon: 32x32, Path stroke-based; stato workspace attivo = sfondo elevato +
             barretta accent 2px a sinistra (Tag='active' settato da DataTrigger sul wrapper). -->
        <Style x:Key="Ribbon.Button" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Width" Value="32"/>
            <Setter Property="Height" Value="32"/>
            <Setter Property="Margin" Value="0,2"/>
            <Setter Property="Padding" Value="0"/>
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="HorizontalAlignment" Value="Center"/>
            <Setter Property="ToolTipService.InitialShowDelay" Value="350"/>
            <Setter Property="ToolTipService.Placement" Value="Right"/>
        </Style>
        <Style x:Key="Ribbon.Icon" TargetType="Path">
            <Setter Property="Width" Value="19"/>
            <Setter Property="Height" Value="19"/>
            <Setter Property="Stretch" Value="Uniform"/>
            <Setter Property="Stroke" Value="{DynamicResource Brush.TextMuted}"/>
            <Setter Property="StrokeThickness" Value="1.7"/>
            <Setter Property="StrokeStartLineCap" Value="Round"/>
            <Setter Property="StrokeEndLineCap" Value="Round"/>
            <Setter Property="StrokeLineJoin" Value="Round"/>
        </Style>
        <!-- Wrapper del bottone-workspace: Border che mostra lo stato attivo. -->
        <Style x:Key="Ribbon.ActiveHost" TargetType="Border">
            <Setter Property="CornerRadius" Value="6"/>
            <Setter Property="Margin" Value="6,2"/>
        </Style>
    </UserControl.Resources>

    <Border Background="{DynamicResource Brush.Surface}"
            BorderThickness="0,0,1,0" BorderBrush="{DynamicResource Brush.Border}">
        <DockPanel LastChildFill="False">
            <!-- Workspace (in alto) -->
            <StackPanel DockPanel.Dock="Top" Margin="0,8,0,0">
                <Border Style="{StaticResource Ribbon.ActiveHost}">
                    <Border.Background>
                        <SolidColorBrush Color="Transparent"/>
                    </Border.Background>
                    <Border.Style>
                        <Style TargetType="Border" BasedOn="{StaticResource Ribbon.ActiveHost}">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsHomeWorkspace}" Value="True">
                                    <Setter Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </Border.Style>
                    <Grid>
                        <Rectangle Width="2" HorizontalAlignment="Left" RadiusX="1" RadiusY="1"
                                   Fill="{DynamicResource Brush.Accent}"
                                   Visibility="{Binding IsHomeWorkspace, Converter={StaticResource BoolToVisible}}"/>
                        <Button Style="{StaticResource Ribbon.Button}" Command="{Binding ShowHomeCommand}"
                                ToolTip="{x:Static res:Strings.Workspace_Home}"
                                AutomationProperties.Name="{x:Static res:Strings.Workspace_Home}">
                            <Path Style="{StaticResource Ribbon.Icon}" Data="{StaticResource Icon.House}">
                                <Path.Resources/>
                            </Path>
                        </Button>
                    </Grid>
                </Border>
                <Border>
                    <Border.Style>
                        <Style TargetType="Border" BasedOn="{StaticResource Ribbon.ActiveHost}">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsChartsWorkspace}" Value="True">
                                    <Setter Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </Border.Style>
                    <Grid>
                        <Rectangle Width="2" HorizontalAlignment="Left" RadiusX="1" RadiusY="1"
                                   Fill="{DynamicResource Brush.Accent}"
                                   Visibility="{Binding IsChartsWorkspace, Converter={StaticResource BoolToVisible}}"/>
                        <Button Style="{StaticResource Ribbon.Button}" Command="{Binding ShowChartsCommand}"
                                ToolTip="{x:Static res:Strings.Workspace_Charts}"
                                AutomationProperties.Name="{x:Static res:Strings.Workspace_Charts}">
                            <Path Style="{StaticResource Ribbon.Icon}" Data="{StaticResource Icon.BarChart3}"/>
                        </Button>
                    </Grid>
                </Border>

                <Rectangle Height="1" Margin="12,8" Fill="{DynamicResource Brush.Border}"/>

                <!-- Azioni -->
                <Button x:Name="BackupButton" Style="{StaticResource Ribbon.Button}"
                        Click="OnBackupClick"
                        ToolTip="{x:Static res:Strings.Workspace_Backup}"
                        AutomationProperties.Name="{x:Static res:Strings.Workspace_Backup}">
                    <Path Style="{StaticResource Ribbon.Icon}" Data="{StaticResource Icon.LifeBuoy}"/>
                    <Button.ContextMenu>
                        <ContextMenu>
                            <MenuItem Header="{x:Static res:Strings.Backup_Menu_Create}"  Command="{Binding BackupCreateCommand}"/>
                            <MenuItem Header="{x:Static res:Strings.Backup_Menu_Restore}" Command="{Binding BackupRestoreCommand}"/>
                            <Separator/>
                            <MenuItem Header="{x:Static res:Strings.Backup_Menu_History}" Command="{Binding ShowBackupHistoryCommand}"/>
                        </ContextMenu>
                    </Button.ContextMenu>
                </Button>
                <Button Style="{StaticResource Ribbon.Button}" Command="{Binding ExportLibraryCommand}"
                        ToolTip="{x:Static res:Strings.Export_Library}"
                        AutomationProperties.Name="{x:Static res:Strings.Export_Library}">
                    <Path Style="{StaticResource Ribbon.Icon}" Data="{StaticResource Icon.Upload}"/>
                </Button>
            </StackPanel>

            <!-- Sistema (in basso) -->
            <StackPanel DockPanel.Dock="Bottom" Margin="0,0,0,8">
                <Button Style="{StaticResource Ribbon.Button}" Command="{Binding ShowDiagnosticInfoCommand}"
                        ToolTip="{x:Static res:Strings.Diagnostic_Menu_Open}"
                        AutomationProperties.Name="{x:Static res:Strings.Diagnostic_Menu_Open}">
                    <Path Style="{StaticResource Ribbon.Icon}" Data="{StaticResource Icon.CircleHelp}"/>
                </Button>
                <Button Style="{StaticResource Ribbon.Button}" Command="{Binding ShowSettingsCommand}"
                        ToolTip="{x:Static res:Strings.Settings_Title}"
                        AutomationProperties.Name="{x:Static res:Strings.Settings_Title}">
                    <Path Style="{StaticResource Ribbon.Icon}" Data="{StaticResource Icon.Settings}"/>
                </Button>
            </StackPanel>
        </DockPanel>
    </Border>
</UserControl>
```

Nota: il primo `<Border ...>` workspace ha lo Style inline doppio nel listato sopra per errore di stesura — in implementazione usare UN solo `Border.Style` con DataTrigger (come il secondo). Rimuovere `<Path.Resources/>` vuoto.

- [ ] **Step 2**: code-behind — il click sull'icona Backup apre il ContextMenu (anche con click sinistro):

```csharp
using System.Windows;
using System.Windows.Controls;

namespace Recordum.WPF.Views;

public partial class WorkspaceRibbon : UserControl
{
    public WorkspaceRibbon()
    {
        InitializeComponent();
    }

    private void OnBackupClick(object sender, RoutedEventArgs e)
    {
        if (BackupButton.ContextMenu is not { } menu) return;
        menu.PlacementTarget = BackupButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
        menu.DataContext = DataContext; // i MenuItem bindano i command del MainWindowViewModel
        menu.IsOpen = true;
    }
}
```

- [ ] **Step 3**: build 0/0, commit `feat(wpf): WorkspaceRibbon — vertical icon rail`.

---

### Task 3: MainWindow — WindowChrome + title bar + ribbon

**Files:** Modify `src/Recordum.WPF/MainWindow.xaml`, `MainWindow.xaml.cs`

- [ ] **Step 1**: su `<Window>` aggiungere:

```xml
        WindowStyle="SingleBorderWindow"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
```

e come primo figlio di Window:

```xml
    <shell:WindowChrome.WindowChrome>
        <shell:WindowChrome CaptionHeight="36" ResizeBorderThickness="6"
                            GlassFrameThickness="0" CornerRadius="0" UseAeroCaptionButtons="False"/>
    </shell:WindowChrome.WindowChrome>
```

(In .NET moderno `WindowChrome` è anche esposto senza namespace custom: se `shell:` dà problemi, usare `WindowChrome.WindowChrome` direttamente — è in `System.Windows.Shell`, assembly PresentationFramework, riconosciuto dal default namespace XAML.)

- [ ] **Step 2**: ristrutturare il layout radice:

```xml
    <Grid x:Name="RootGrid">
        <Grid.RowDefinitions>
            <RowDefinition Height="36"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Title bar custom -->
        <Border Grid.Row="0" Background="{DynamicResource Brush.Surface}"
                BorderThickness="0,0,0,1" BorderBrush="{DynamicResource Brush.Border}">
            <Grid>
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="12,0">
                    <Path Width="15" Height="15" Stretch="Uniform" Margin="0,0,8,0"
                          Stroke="{DynamicResource Brush.Accent}" StrokeThickness="1.8"
                          StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
                          Data="{StaticResource Icon.Activity}"/>
                    <TextBlock Text="{x:Static res:Strings.AppTitle}" VerticalAlignment="Center"
                               FontWeight="SemiBold" FontSize="12.5"
                               Foreground="{DynamicResource Brush.TextMuted}"/>
                </StackPanel>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <Button x:Name="MinButton" Style="{StaticResource Caption.Button}" Click="OnMinimizeClick"
                            shell:WindowChrome.IsHitTestVisibleInChrome="True">
                        <Path Style="{StaticResource Caption.Icon}" Data="M5 12h14"/>
                    </Button>
                    <Button x:Name="MaxButton" Style="{StaticResource Caption.Button}" Click="OnMaximizeRestoreClick"
                            shell:WindowChrome.IsHitTestVisibleInChrome="True">
                        <Path x:Name="MaxIcon" Style="{StaticResource Caption.Icon}"
                              Data="M5 5h14v14H5z"/>
                    </Button>
                    <Button x:Name="CloseButton" Style="{StaticResource Caption.CloseButton}" Click="OnCloseClick"
                            shell:WindowChrome.IsHitTestVisibleInChrome="True">
                        <Path Style="{StaticResource Caption.Icon}" Data="M6 6l12 12 M18 6L6 18"/>
                    </Button>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Contenuto: ribbon | workspaces -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="44"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <v:WorkspaceRibbon Grid.Column="0"/>
            <Grid Grid.Column="1">
                <!-- ⟵ QUI dentro va l'attuale "Content area" (Grid con Home panes / ChartsView /
                     BackupHistoryView) INVARIATA; la vecchia StackPanel top-nav + Menu si ELIMINA. -->
            </Grid>
        </Grid>

        <vt:TimerStatusBar Grid.Row="2" DataContext="{Binding TimerStatusBar}"/>
    </Grid>
```

Stili caption (in `Window.Resources`):

```xml
        <Style x:Key="Caption.Icon" TargetType="Path">
            <Setter Property="Width" Value="11"/>
            <Setter Property="Height" Value="11"/>
            <Setter Property="Stretch" Value="Uniform"/>
            <Setter Property="Stroke" Value="{DynamicResource Brush.Text}"/>
            <Setter Property="StrokeThickness" Value="1.4"/>
        </Style>
        <Style x:Key="Caption.Button" TargetType="Button">
            <Setter Property="Width" Value="46"/>
            <Setter Property="Height" Value="36"/>
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="bd" Background="{TemplateBinding Background}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="bd" Property="Background" Value="{DynamicResource Brush.SurfaceElevated}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key="Caption.CloseButton" TargetType="Button" BasedOn="{StaticResource Caption.Button}">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="bd" Background="{TemplateBinding Background}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="bd" Property="Background" Value="#c42b1c"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

- [ ] **Step 3**: code-behind — handler caption + fix overflow maximize + glyph max/restore:

```csharp
    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // With a custom chrome a maximized window overflows the work area by the resize border:
        // compensate with padding, and swap the maximize glyph for the restore one.
        RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaxIcon.Data = Geometry.Parse(WindowState == WindowState.Maximized
            ? "M8 5h11v11 M5 8h11v11H5z"   // restore: two offset squares
            : "M5 5h14v14H5z");            // maximize: single square
    }
```

(`SystemCommands` è in `System.Windows`; `Geometry.Parse` richiede `System.Windows.Media` già importato.)

- [ ] **Step 4**: eliminare dal vecchio layout la `StackPanel` top-nav completa (RadioButton Home/Grafici, bottone Esporta libreria, `Menu` Backup/Aiuto/Impostazioni). I KeyBinding Ctrl+1..5 restano invariati.

- [ ] **Step 5**: build 0/0 + suite WPF; commit `feat(wpf): custom title bar + icon ribbon shell (WindowChrome)`.

---

### Task 4: TimerStatusBar compatta

**Files:** Modify `src/Recordum.WPF/Views/Timer/TimerStatusBar.xaml`

- [ ] **Step 1**: restyle (stessa struttura, metriche compatte): `ItemsControl Padding="10,3"`; chip `Padding="6,2"` `Margin="0,0,8,0"` `CornerRadius="3"`; `HabitName` `FontSize={StaticResource FontSize.Small}`; `ElapsedDisplay` `FontSize=11.5`; bottoni `Width/Height=20` `FontSize=11`.

- [ ] **Step 2**: build, commit `style(wpf): compact timer status bar`.

---

### Task 5: verifica + tracker

- [ ] **Step 1**: skill `run` + checklist utente: drag title bar; doppio-clic max/restore; snap Win+frecce e bordi; min/max/close custom (close hover rosso); ribbon tooltip + stati attivi Home/Grafici; menu Backup dall'icona (Crea/Ripristina/Cronologia); Esporta; Aiuto→Diagnostica; ⚙→Impostazioni; Ctrl+1..5; entrambi i temi; finestra massimizzata senza tagli; status bar compatta con un timer attivo.
- [ ] **Step 2**: aggiornare `docs/phase-4-issues.md` + memoria; commit docs.

---

## Self-review

- Spec §B coperta: title bar custom (T3), ribbon variante B con deroga documentata ContextMenu backup (T2), rimozione barra/menu (T3.4), status bar restyle (T4), Ctrl+1..5 invariati (T3.4), brush a tema ovunque. Icone: House/LifeBuoy esistono, BarChart3/Upload/CircleHelp nuove, Settings completato (T1) — coerenti coi nomi usati in T2/T3.
- Nessun placeholder; l'unico "QUI dentro" di T3.2 è un move 1:1 di markup esistente, delimitato.
- Rischio noto: comportamento WindowChrome su multi-monitor/DPI — coperto dalla checklist visiva.
