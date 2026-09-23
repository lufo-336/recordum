using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;

namespace Recordum.Presentation.Services;

/// <summary>
/// Persists the two culture choices (AppSettings <c>ui.language</c> and <c>ui.format</c>). Mirrors
/// <see cref="AppFontService"/>, minus the change event: neither choice can be applied to a running
/// process, so there is nothing for a listener to do — <c>App.OnStartup</c> reads them once through
/// <c>CultureBootstrap</c> and Settings tells the user the change lands at the next
/// start.
/// </summary>
public sealed class CultureService(IAppSettingService settings) : ICultureService
{
    private const string LanguageKey = "ui.language";
    private const string FormatKey = "ui.format";
    private bool _initialized;

    public AppCulture Language { get; private set; } = AppCulture.System;

    public AppCulture Format { get; private set; } = AppCulture.System;

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        _initialized = true;
        Language = (await settings.GetEnumAsync<AppCulture>(LanguageKey, ct)) ?? AppCulture.System;
        Format = (await settings.GetEnumAsync<AppCulture>(FormatKey, ct)) ?? AppCulture.System;
    }

    public async ValueTask SetLanguageAsync(AppCulture culture, CancellationToken ct = default)
    {
        if (Language == culture) return;
        Language = culture;
        await settings.SetEnumAsync(LanguageKey, culture, ct);
    }

    public async ValueTask SetFormatAsync(AppCulture culture, CancellationToken ct = default)
    {
        if (Format == culture) return;
        Format = culture;
        await settings.SetEnumAsync(FormatKey, culture, ct);
    }
}
