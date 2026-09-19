using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Recordum.WPF.Controls;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.Workspace;

namespace Recordum.WPF;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    // The two workspace columns whose Width is bound to VM state. A GridSplitter drag with
    // ResizeBehavior=PreviousAndNext writes ColumnDefinition.Width as a LOCAL value, which in WPF
    // permanently replaces (kills) the binding. After that, returning to the panoramica state can't
    // re-collapse the columns and the Home stops filling the window. We capture both bindings on load
    // and re-apply them after every drag so VM state stays the source of truth.
    private MultiBinding? _sidebarColumnWidthBinding;
    private BindingBase? _workspaceColumnWidthBinding;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        InitializeComponent();
        FitToScreen();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Ensures the window never opens larger than the screen's work area. The default
    /// 1280x760 size overflowed small/RDP displays (e.g. a 1280x672 work area), pushing
    /// the top nav and the bottom Save buttons / status bar off-screen. We also relax the
    /// minimums so the window stays fully on-screen even on very small displays.
    /// </summary>
    private void FitToScreen()
    {
        var wa = SystemParameters.WorkArea;
        (Width, Height) = ClampToWorkArea(Width, Height, wa.Width, wa.Height);
        MinWidth = Math.Min(MinWidth, wa.Width);
        MinHeight = Math.Min(MinHeight, wa.Height);
    }

    /// <summary>Pure size-clamp: never larger than the work area. Unit-tested.</summary>
    internal static (double Width, double Height) ClampToWorkArea(
        double desiredWidth, double desiredHeight, double workAreaWidth, double workAreaHeight)
        => (Math.Min(desiredWidth, workAreaWidth), Math.Min(desiredHeight, workAreaHeight));

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        CaptureColumnWidthBindings();
        await _viewModel.InitializeAsync().ConfigureAwait(true);
    }

    /// <summary>Snapshots the original Width bindings of the two splitter-adjacent columns so a drag
    /// (which clobbers them with a local value) can be undone via <see cref="ReapplyColumnWidthBindings"/>.</summary>
    private void CaptureColumnWidthBindings()
    {
        _sidebarColumnWidthBinding ??=
            BindingOperations.GetMultiBindingExpression(SidebarColumn, ColumnDefinition.WidthProperty)?.ParentMultiBinding;
        _workspaceColumnWidthBinding ??=
            BindingOperations.GetBindingExpression(WorkspaceColumn, ColumnDefinition.WidthProperty)?.ParentBinding;
    }

    private void ReapplyColumnWidthBindings()
    {
        if (_sidebarColumnWidthBinding is not null)
            BindingOperations.SetBinding(SidebarColumn, ColumnDefinition.WidthProperty, _sidebarColumnWidthBinding);
        if (_workspaceColumnWidthBinding is not null)
            BindingOperations.SetBinding(WorkspaceColumn, ColumnDefinition.WidthProperty, _workspaceColumnWidthBinding);
    }

    // ---- custom title bar (WindowChrome) ----

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
        // compensate with a margin, and swap the maximize glyph for the restore one.
        RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaxIcon.Data = Geometry.Parse(WindowState == WindowState.Maximized
            ? "M8 5h11v11 M5 8h11v11H5z"   // restore: two offset squares
            : "M5 5h14v14H5z");            // maximize: single square
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Esc with a multi-selection: clear it (spec §C.1) before any column handling.
        if (e.Key == Key.Escape && _viewModel.Home.HasMultiSelection)
        {
            _viewModel.Home.ClearSelection();
            e.Handled = true;
            return;
        }

        // Esc on the active tab's screen: leave focus mode if active, else close the most recently
        // opened view column (nav-model §3.5 / focus §3.6).
        if (e.Key == Key.Escape && _viewModel.Workspace.ActiveSubjectTab?.Screen is { } screen)
        {
            if (screen.IsFocusActive) screen.ExitFocusCommand.Execute(null);
            else screen.CloseLastColumn();
            e.Handled = true;
            return;
        }

        // Ctrl+1/2/3 select a Home sub-tab (via the XAML KeyBindings) AND return to the
        // Home workspace. We only switch the workspace here and leave e.Handled false so
        // the tab KeyBinding still fires. Ctrl+4 (Charts) is handled by its own KeyBinding.
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
        if (e.Key is Key.D1 or Key.D2 or Key.D3 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3)
        {
            if (_viewModel.ShowHomeCommand.CanExecute(null))
                _viewModel.ShowHomeCommand.Execute(null);
        }
    }

    private async void OnSidebarSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        // Read the rendered width BEFORE re-applying the binding (the drag left it as a local value),
        // push it to the VM, then restore the bindings so VM state drives the columns again. Without
        // the restore, the dragged local Width persists and the Home never re-fills on panoramica.
        _viewModel.Workspace.SidebarWidth = Math.Clamp(SidebarColumn.ActualWidth, 0, 460);
        ReapplyColumnWidthBindings();
        await _viewModel.Workspace.SaveStateAsync().ConfigureAwait(true);
    }
}
