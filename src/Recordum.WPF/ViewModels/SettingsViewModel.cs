using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _theme;
    private readonly IAppFontService _fonts;
    private bool _initializing;

    [ObservableProperty] private ThemeMode _themeMode;
    [ObservableProperty] private AppFont _appFont;

    public SettingsViewModel(IThemeService theme, IAppFontService fonts)
    {
        _theme = theme;
        _fonts = fonts;
    }

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        _initializing = true;
        try
        {
            ThemeMode = _theme.CurrentMode;
            AppFont = _fonts.CurrentFont;
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

    partial void OnAppFontChanged(AppFont value)
    {
        if (_initializing) return;
        _ = _fonts.SetFontAsync(value).AsTask();
    }
}
