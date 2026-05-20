using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;

namespace Recordum.WPF.Theming;

public sealed class ThemeService : IThemeService, IDisposable
{
    private const string PersistenceKey = "theme.mode";
    private readonly IAppSettingService _settings;
    private readonly ILogger<ThemeService> _logger;
    private readonly Func<bool> _systemIsLight;
    private ThemeMode _mode;
    private ThemeMode _effective;
    private bool _initialized;
    private bool _disposed;

    public ThemeMode CurrentMode => _mode;
    public ThemeMode EffectiveTheme => _effective;
    public event EventHandler<ThemeMode>? EffectiveThemeChanged;

    public ThemeService(IAppSettingService settings, ILogger<ThemeService> logger)
        : this(settings, logger, ReadSystemIsLight) { }

    // Test-friendly ctor — internal visibility, exposed via InternalsVisibleTo.
    internal ThemeService(IAppSettingService settings, ILogger<ThemeService> logger, Func<bool> systemIsLight)
    {
        _settings = settings;
        _logger = logger;
        _systemIsLight = systemIsLight;
    }

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        _initialized = true;
        _mode = (await _settings.GetEnumAsync<ThemeMode>(PersistenceKey, ct)) ?? ThemeMode.System;
        RecomputeEffective(raiseEvent: false);
        SystemEvents.UserPreferenceChanged += OnUserPrefChanged;
    }

    public async ValueTask SetModeAsync(ThemeMode mode, CancellationToken ct = default)
    {
        if (_mode == mode) return;
        _mode = mode;
        await _settings.SetEnumAsync(PersistenceKey, mode, ct);
        RecomputeEffective(raiseEvent: true);
    }

    internal void OnSystemPreferenceChanged() => RecomputeEffective(raiseEvent: true);

    private void OnUserPrefChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.Category == UserPreferenceCategory.General) RecomputeEffective(raiseEvent: true);
    }

    private void RecomputeEffective(bool raiseEvent)
    {
        var next = _mode switch
        {
            ThemeMode.Light => ThemeMode.Light,
            ThemeMode.Dark => ThemeMode.Dark,
            _ => _systemIsLight() ? ThemeMode.Light : ThemeMode.Dark,
        };
        if (next == _effective) return;
        _effective = next;
        if (raiseEvent) EffectiveThemeChanged?.Invoke(this, next);
    }

    private static bool ReadSystemIsLight()
    {
        try
        {
            using var rk = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = rk?.GetValue("AppsUseLightTheme");
            return val is int i ? i == 1 : true;
        }
        catch
        {
            return true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        SystemEvents.UserPreferenceChanged -= OnUserPrefChanged;
        _disposed = true;
    }
}
