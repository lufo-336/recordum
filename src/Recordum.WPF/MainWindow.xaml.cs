using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using Recordum.WPF.ViewModels;

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
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync().ConfigureAwait(true);
        ApplyEditorPaneWidth(_viewModel.EditorPaneWidth);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.EditorPaneWidth))
            ApplyEditorPaneWidth(_viewModel.EditorPaneWidth);
    }

    private void ApplyEditorPaneWidth(double width)
    {
        EditorPaneColumn.Width = new GridLength(width);
    }

    private async void OnEditorPaneSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _viewModel.EditorPaneWidth = EditorPaneColumn.ActualWidth;
        await _viewModel.CommitEditorPaneWidthAsync().ConfigureAwait(true);
    }
}
