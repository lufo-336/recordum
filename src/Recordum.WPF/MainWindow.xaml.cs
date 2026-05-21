using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
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
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.Editor.PropertyChanged += OnEditorPropertyChanged;
        viewModel.Home.PropertyChanged += OnHomePropertyChanged;
        Loaded += OnLoaded;
    }

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
    {
        EditorPaneColumn.Width = _viewModel.Editor.HasNode
            ? new GridLength(_viewModel.EditorPaneWidth)
            : new GridLength(0);
    }

    private void ApplyHabitScreenWidth()
    {
        HabitScreenColumn.Width = _viewModel.Home.SelectedRow is not null
            ? new GridLength(_viewModel.HabitScreenWidth)
            : new GridLength(0);
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
