using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Recordum.WPF.Controls;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.HabitScreen;

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
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.Editor.PropertyChanged += OnEditorPropertyChanged;
        viewModel.Home.PropertyChanged += OnHomePropertyChanged;
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
        await _viewModel.InitializeAsync().ConfigureAwait(true);
        ApplyEditorPaneWidth();
        ApplyHabitScreenWidth();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.EditorPaneWidth))
            ApplyEditorPaneWidth();
        else if (e.PropertyName == nameof(MainWindowViewModel.HabitScreenWidth))
            ApplyHabitScreenWidth();
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitEditorViewModel.HasNode))
            ApplyEditorPaneWidth();
    }

    private void OnHomePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.SelectedRow))
            ApplyHabitScreenWidth();
    }

    private void ApplyEditorPaneWidth()
        => AnimateColumn(EditorPaneColumn, _viewModel.Editor.HasNode ? _viewModel.EditorPaneWidth : 0);

    private void ApplyHabitScreenWidth()
        => AnimateColumn(HabitScreenColumn, _viewModel.Home.SelectedRow is not null ? _viewModel.HabitScreenWidth : 0);

    // Slides a column open/closed instead of snapping. Sets the target as the base (local) value and
    // plays a Stop-fill animation over it, so the GridSplitter can still resize the column afterwards
    // (a held animation would otherwise override later direct/​splitter width changes).
    private static void AnimateColumn(ColumnDefinition column, double target)
    {
        double from = column.ActualWidth;
        column.Width = new GridLength(target);
        if (Math.Abs(from - target) < 0.5) return;

        var animation = new GridLengthAnimation
        {
            From = from,
            To = target,
            Duration = TimeSpan.FromMilliseconds(180),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        column.BeginAnimation(ColumnDefinition.WidthProperty, animation);
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
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

    private async void OnEditorPaneSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _viewModel.EditorPaneWidth = EditorPaneColumn.ActualWidth;
        await _viewModel.CommitEditorPaneWidthAsync().ConfigureAwait(true);
    }

    private async void OnHabitScreenSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _viewModel.HabitScreenWidth = HabitScreenColumn.ActualWidth;
        await _viewModel.CommitHabitScreenWidthAsync().ConfigureAwait(true);
    }
}
