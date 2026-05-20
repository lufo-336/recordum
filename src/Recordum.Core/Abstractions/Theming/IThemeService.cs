using Recordum.Core.Enums;

namespace Recordum.Core.Abstractions.Theming;

public interface IThemeService
{
    /// <summary>User-configured mode. <see cref="ThemeMode.System"/> means follow OS.</summary>
    ThemeMode CurrentMode { get; }

    /// <summary>The effective theme actually applied (Light or Dark) after resolving System.</summary>
    ThemeMode EffectiveTheme { get; }

    event EventHandler<ThemeMode>? EffectiveThemeChanged;

    ValueTask InitializeAsync(CancellationToken ct = default);

    ValueTask SetModeAsync(ThemeMode mode, CancellationToken ct = default);
}
