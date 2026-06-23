using Recordum.Core.Enums;

namespace Recordum.Core.Abstractions.Theming;

/// <summary>
/// Holds and persists the user's UI font choice and notifies when it changes. Mirrors
/// <see cref="IThemeService"/>: the WPF host applies the choice (swaps the <c>Font.Default</c>
/// resource) in response to <see cref="FontChanged"/>.
/// </summary>
public interface IAppFontService
{
    /// <summary>The currently selected font.</summary>
    AppFont CurrentFont { get; }

    event EventHandler<AppFont>? FontChanged;

    ValueTask InitializeAsync(CancellationToken ct = default);

    ValueTask SetFontAsync(AppFont font, CancellationToken ct = default);
}
