using Avalonia;
using Avalonia.Styling;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;

namespace Recordum.UI.Services;

/// <summary>Gemella Avalonia di <c>Recordum.WPF.Theming.ThemeService</c>. La differenza non è di
/// contratto (stessa interfaccia <see cref="IThemeService"/>, stesso `AppSetting` "theme.mode") ma di
/// meccanismo: WPF leggeva il registro di Windows e ascoltava <c>SystemEvents.UserPreferenceChanged</c>
/// per sapere se il sistema è chiaro o scuro; Avalonia lo sa già da sé — basta impostare
/// <see cref="ThemeVariant.Light"/>/<see cref="ThemeVariant.Dark"/>/<see cref="ThemeVariant.Default"/>
/// su <see cref="Application.RequestedThemeVariant"/> e leggere <see cref="Application.ActualThemeVariant"/>
/// per il valore risolto — <c>Default</c> risolve da sé all'OS, e <c>ActualThemeVariantChanged</c>
/// (verificato che l'evento esiste in 12.1.2) sostituisce l'ascolto Win32.</summary>
public sealed class AvaloniaThemeService : IThemeService, IDisposable
{
    private const string PersistenceKey = "theme.mode";

    private readonly IAppSettingService _settings;
    private readonly Application _app;
    private ThemeMode _mode;
    private bool _initialized;
    private bool _disposed;

    public ThemeMode CurrentMode => _mode;

    public ThemeMode EffectiveTheme => _app.ActualThemeVariant == ThemeVariant.Dark ? ThemeMode.Dark : ThemeMode.Light;

    public event EventHandler<ThemeMode>? EffectiveThemeChanged;

    public AvaloniaThemeService(IAppSettingService settings, Application app)
    {
        _settings = settings;
        _app = app;
    }

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        _initialized = true;
        _mode = await _settings.GetEnumAsync<ThemeMode>(PersistenceKey, ct) ?? ThemeMode.System;
        Apply(_mode);
        _app.ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    public async ValueTask SetModeAsync(ThemeMode mode, CancellationToken ct = default)
    {
        if (_mode == mode) return;
        _mode = mode;
        await _settings.SetEnumAsync(PersistenceKey, mode, ct);
        Apply(mode);
    }

    private void Apply(ThemeMode mode) => _app.RequestedThemeVariant = mode switch
    {
        ThemeMode.Light => ThemeVariant.Light,
        ThemeMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    // Scatta sia quando SetModeAsync cambia il RequestedThemeVariant, sia quando la modalità è
    // System e l'OS cambia tema da sotto: in entrambi i casi ActualThemeVariant è già il nuovo
    // valore quando l'evento arriva, quindi EffectiveTheme lo legge corretto.
    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        EffectiveThemeChanged?.Invoke(this, EffectiveTheme);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _app.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        _disposed = true;
    }
}
