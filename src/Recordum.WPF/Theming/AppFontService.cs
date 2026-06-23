using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;

namespace Recordum.WPF.Theming;

/// <summary>
/// Persists the UI font choice (AppSetting <c>ui.font</c>) and raises <see cref="FontChanged"/>.
/// The actual resource swap (<c>Font.Default</c>) lives in <c>App.ApplyFont</c>, mirroring how
/// <see cref="ThemeService"/> / <c>App.ApplyTheme</c> split state from the WPF resource update.
/// </summary>
public sealed class AppFontService(IAppSettingService settings) : IAppFontService
{
    private const string PersistenceKey = "ui.font";
    private AppFont _font = AppFont.Serif;   // Palatino is the shipped default
    private bool _initialized;

    public AppFont CurrentFont => _font;
    public event EventHandler<AppFont>? FontChanged;

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        _initialized = true;
        _font = (await settings.GetEnumAsync<AppFont>(PersistenceKey, ct)) ?? AppFont.Serif;
    }

    public async ValueTask SetFontAsync(AppFont font, CancellationToken ct = default)
    {
        if (_font == font) return;
        _font = font;
        await settings.SetEnumAsync(PersistenceKey, font, ct);
        FontChanged?.Invoke(this, font);
    }
}
