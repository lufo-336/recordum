using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Recordum.UI.Theming;

namespace Recordum.UI.Converters;

/// <summary>[colore dell'abitudine (hex), variante del tema] → il pennello dei valori nella variante "testo"
/// (direzione visiva §4.3). Il fondo è il <c>Brush.Pane</c> della variante con sopra la tinta <c>Opacity.RowTint</c>.
/// Un colore rotto dà il testo normale.</summary>
public sealed class HabitTextBrushConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var variant = values.Count > 1 && values[1] is ThemeVariant v
            ? v : global::Avalonia.Application.Current?.ActualThemeVariant ?? ThemeVariant.Light;
        return BrushFor(values.Count > 0 ? values[0] as string : null, variant);
    }

    /// <summary>Lo stesso calcolo per chi disegna in code-behind (la striscia della Home).</summary>
    public static IBrush BrushFor(string? hex, ThemeVariant variant)
    {
        var app = global::Avalonia.Application.Current;
        if (app is null) return Brushes.Black;
        if (!HabitColors.TryParse(hex, out var habit))
            return Resource<IBrush>(app, "Brush.Text", variant) ?? Brushes.Black;

        var pane = (Resource<ISolidColorBrush>(app, "Brush.Pane", variant) ?? Brushes.White).Color;
        var tint = Resource<object>(app, "Opacity.RowTint", variant) is double d ? d : 0.09;
        return new ImmutableSolidColorBrush(HabitColors.TextVariant(habit, pane, tint, variant == ThemeVariant.Dark));
    }

    internal static T? Resource<T>(global::Avalonia.Application app, string key, ThemeVariant variant) where T : class
        => app.TryGetResource(key, variant, out var value) ? value as T : null;
}
