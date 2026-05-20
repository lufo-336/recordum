using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _theme;
    private bool _initializing;

    [ObservableProperty] private ThemeMode _themeMode;

    public SettingsViewModel(IThemeService theme)
    {
        _theme = theme;
    }

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        _initializing = true;
        try
        {
            ThemeMode = _theme.CurrentMode;
        }
        finally
        {
            _initializing = false;
        }
        await ValueTask.CompletedTask;
    }

    partial void OnThemeModeChanged(ThemeMode value)
    {
        if (_initializing) return;
        _ = _theme.SetModeAsync(value).AsTask();
    }
}
