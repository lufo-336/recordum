using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Controls;

namespace Recordum.UI.Converters;

/// <summary>Il margine della card di una riga della Home piatta: a sinistra il rientro del livello e il padding
/// della propria cornice per un gruppo, a destra lo stesso per tutte (<see cref="GroupFrames.RightInset"/>), e sotto
/// lo spazio delle cornici che si chiudono su questa riga (vedi <see cref="GroupFrames"/>).</summary>
public sealed class RowCardMarginConverter : IValueConverter
{
    public static readonly RowCardMarginConverter Instance = new();

    // Un gruppo sta dentro la sua cornice anche quando è chiuso e la cornice è trasparente: bordo 1 +
    // padding 3 ai lati, e sopra e sotto anche il margine 1 della cornice.
    private const double GroupInset = 4;
    private const double GroupInsetVertical = 5;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not HomeRowViewModel row) return new Thickness(0, 1);
        var inset = row.IsGroup ? GroupInset : 0;
        var opens = row.FrameSlices.Any(s => s.IsTop);
        var closing = row.FrameSlices.Count(s => s.IsBottom);
        // Sotto il gruppo aperto, i suoi figli: 2 + 1 della prima card = i 3 del layout annidato.
        var bottom = row.IsGroup ? (opens ? 2 : GroupInsetVertical) : 1;
        return new Thickness(
            row.IndentLevel * GroupFrames.IndentStep + inset,
            row.IsGroup ? GroupInsetVertical : 1,
            GroupFrames.RightInset,
            bottom + GroupFrames.ClosingStep * closing);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
