using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;

namespace Recordum.WPF.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _theme;
    private readonly IAppFontService _fonts;
    private readonly ICultureService _cultures;
    private bool _initializing;

    [ObservableProperty] private ThemeMode _themeMode;
    [ObservableProperty] private AppFont _appFont;

    /// <summary>Which .resx the UI reads (F0-6). Separate from <see cref="RegionalFormat"/> the way
    /// Windows separates them: an app in English still writes 2,5 and reads 17/08/2026 for an Italian
    /// user. Both land at the next start — the screen says so.</summary>
    [ObservableProperty] private AppCulture _uiLanguage;

    /// <summary>Which culture formats dates and numbers, and therefore which decimal separator the
    /// numeric fields accept (W2V-17c).</summary>
    [ObservableProperty] private AppCulture _regionalFormat;

    public SettingsViewModel(IThemeService theme, IAppFontService fonts, ICultureService cultures)
    {
        _theme = theme;
        _fonts = fonts;
        _cultures = cultures;
    }

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        _initializing = true;
        try
        {
            ThemeMode = _theme.CurrentMode;
            AppFont = _fonts.CurrentFont;
            UiLanguage = _cultures.Language;
            RegionalFormat = _cultures.Format;
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

    partial void OnUiLanguageChanged(AppCulture value)
    {
        if (_initializing) return;
        _ = _cultures.SetLanguageAsync(value).AsTask();
    }

    partial void OnRegionalFormatChanged(AppCulture value)
    {
        if (_initializing) return;
        _ = _cultures.SetFormatAsync(value).AsTask();
    }
}
