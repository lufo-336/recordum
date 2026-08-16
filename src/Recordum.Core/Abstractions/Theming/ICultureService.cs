using Recordum.Core.Enums;

namespace Recordum.Core.Abstractions.Theming;

/// <summary>
/// Holds and persists the two culture choices. Mirrors <see cref="IAppFontService"/>, with one
/// difference that shapes the whole feature: there is no <c>Changed</c> event, because neither choice
/// can be applied to a running process — the .resx strings are resolved when the XAML loads, and the
/// visual tree's language comes from a metadata default that WPF allows to be overridden once. Both
/// take effect at the next start, and the Settings screen says so.
/// </summary>
public interface ICultureService
{
    /// <summary>Which .resx the UI strings come from.</summary>
    AppCulture Language { get; }

    /// <summary>Which culture formats dates and numbers — and therefore which decimal separator the
    /// numeric fields accept.</summary>
    AppCulture Format { get; }

    ValueTask InitializeAsync(CancellationToken ct = default);

    ValueTask SetLanguageAsync(AppCulture culture, CancellationToken ct = default);

    ValueTask SetFormatAsync(AppCulture culture, CancellationToken ct = default);
}
