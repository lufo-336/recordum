using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Recordum.WPF.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public HomeViewModel Home { get; }
    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(HomeViewModel home, SettingsViewModel settings)
    {
        Home = home;
        Settings = settings;
    }

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        await Settings.InitializeAsync(ct);
        await Home.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private static void Quit() => System.Windows.Application.Current?.Shutdown();
}
