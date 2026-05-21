using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.WPF.ViewModels.HabitEditor;

namespace Recordum.WPF.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public HomeViewModel Home { get; }
    public SettingsViewModel Settings { get; }
    public HabitEditorViewModel Editor { get; }

    public MainWindowViewModel(HomeViewModel home, SettingsViewModel settings, HabitEditorViewModel editor)
    {
        Home = home;
        Settings = settings;
        Editor = editor;
        Home.PropertyChanged += OnHomePropertyChanged;
    }

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        await Settings.InitializeAsync(ct);
        await Home.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private static void Quit() => System.Windows.Application.Current?.Shutdown();

    private async void OnHomePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.SelectedRow)) return;
        await Editor.LoadAsync(Home.SelectedRow?.HabitNodeId).ConfigureAwait(true);
    }
}
