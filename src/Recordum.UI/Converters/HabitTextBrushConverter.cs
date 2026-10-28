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
        var app = global::Avalonia.Application.Current;
        var variant = values.Count > 1 && values[1] is ThemeVariant v ? v : app?.ActualThemeVariant ?? ThemeVariant.Light;
        if (app is null) return Brushes.Black;
        if (values.Count == 0 || !HabitColors.TryParse(values[0] as string, out var habit))
            return Resource<IBrush>(app, "Brush.Text", variant) ?? Brushes.Black;

        var pane = (Resource<ISolidColorBrush>(app, "Brush.Pane", variant) ?? Brushes.White).Color;
        var tint = Resource<object>(app, "Opacity.RowTint", variant) is double d ? d : 0.09;
        var dark = variant == ThemeVariant.Dark;
        return new ImmutableSolidColorBrush(HabitColors.TextVariant(habit, pane, tint, dark));
    }

    private static T? Resource<T>(global::Avalonia.Application app, string key, ThemeVariant variant) where T : class
        => app.TryGetResource(key, variant, out var value) ? value as T : null;
}
