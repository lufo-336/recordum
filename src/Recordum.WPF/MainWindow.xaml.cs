using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Recordum.WPF.Controls;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Workspace;

namespace Recordum.WPF;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

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
        => await _viewModel.InitializeAsync().ConfigureAwait(true);

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

        // Esc closes the most recently opened view column of the active tab's habit screen (nav-model §3.5).
        // Task 2 will wire this through the per-tab screen; until then, no-op.
        if (e.Key == Key.Escape && _viewModel.Home.SelectedRow is not null)
        {
            (_viewModel.Workspace.ActiveSubjectTab?.Screen)?.CloseLastColumn();
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
        _viewModel.Workspace.SidebarWidth = Math.Clamp(SidebarColumn.ActualWidth, 0, 460);
        await _viewModel.Workspace.SaveStateAsync().ConfigureAwait(true);
    }
}
