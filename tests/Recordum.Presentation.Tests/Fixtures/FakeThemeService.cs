using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeThemeService : IThemeService
{
    public ThemeMode CurrentMode { get; set; } = ThemeMode.System;
    public ThemeMode EffectiveTheme { get; set; } = ThemeMode.Light;
    private readonly List<ThemeMode> _setModeCalls = new();
    public IReadOnlyList<ThemeMode> SetModeCalls => _setModeCalls;
    public event EventHandler<ThemeMode>? EffectiveThemeChanged;

    public ValueTask InitializeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    public ValueTask SetModeAsync(ThemeMode mode, CancellationToken ct = default)
    {
        _setModeCalls.Add(mode);
        CurrentMode = mode;
        if (mode is ThemeMode.Light or ThemeMode.Dark)
        {
            EffectiveTheme = mode;
            EffectiveThemeChanged?.Invoke(this, mode);
        }
        return ValueTask.CompletedTask;
    }
}
