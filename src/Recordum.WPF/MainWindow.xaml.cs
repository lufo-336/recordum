using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
        ApplyHabitScreenWidth();
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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.HabitScreenWidth))
            ApplyHabitScreenWidth();
    }

    private void OnHomePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.SelectedRow))
            ApplyHabitScreenWidth();
    }

    private void ApplyHabitScreenWidth()
        => AnimateColumn(HabitScreenColumn, HabitScreenContent, _viewModel.Home.SelectedRow is not null ? _viewModel.HabitScreenWidth : 0);

    // Opens a column by sliding its content in (animated TranslateTransform — composition only, no
    // per-frame layout), and closes it by shrinking the now-empty column with the cheap width animation.
    // Sets the target width as the base (local) value and uses Stop-fill animations so the GridSplitter
    // can still resize the column afterwards (a held animation would override later width changes).
    private static void AnimateColumn(ColumnDefinition column, FrameworkElement content, double target)
    {
        double current = column.ActualWidth;
        if (Math.Abs(current - target) < 0.5) return;

        if (target > 0.5)
        {
            // Open: reserve the final width once (single layout pass), then slide the content in from
            // the right. Content is ClipToBounds, so the off-screen start position is invisible.
            column.Width = new GridLength(target);
            var tt = EnsureTranslate(content);
            tt.X = target; // set before BeginAnimation to avoid a one-frame flash at the final position
            var slide = new DoubleAnimation
            {
                From = target,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            slide.Completed += (_, _) => tt.X = 0; // settle the base value after Stop-fill
            tt.BeginAnimation(TranslateTransform.XProperty, slide);
        }
        else
        {
            // Close: the content collapses via its Visibility binding; the empty column shrinks cheaply.
            column.Width = new GridLength(0);
            var collapse = new GridLengthAnimation
            {
                From = current,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            column.BeginAnimation(ColumnDefinition.WidthProperty, collapse);
        }
    }

    private static TranslateTransform EnsureTranslate(FrameworkElement content)
        => content.RenderTransform as TranslateTransform
           ?? (TranslateTransform)(content.RenderTransform = new TranslateTransform());

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Esc closes the most recently opened view column of the habit workspace (nav-model §3.5).
        if (e.Key == Key.Escape && _viewModel.Home.SelectedRow is not null)
        {
            _viewModel.HabitScreen.CloseLastColumn();
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

    private async void OnHabitScreenSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _viewModel.HabitScreenWidth = HabitScreenColumn.ActualWidth;
        await _viewModel.CommitHabitScreenWidthAsync().ConfigureAwait(true);
    }
}
