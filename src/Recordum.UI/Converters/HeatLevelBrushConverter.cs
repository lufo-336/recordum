using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Recordum.UI.Theming;

namespace Recordum.UI.Converters;

/// <summary>[gradino 0–4, colore dell'abitudine (hex), variante del tema] → il pennello della cella della mappa
/// (direzione visiva §4.3). Sostituisce i verdi fissi di GitHub.</summary>
public sealed class HeatLevelBrushConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = global::Avalonia.Application.Current;
        if (app is null) return Brushes.Transparent;
        var variant = values.Count > 2 && values[2] is ThemeVariant v ? v : app.ActualThemeVariant;
        var empty = HabitTextBrushConverter.Resource<IBrush>(app, "Brush.Cell", variant) ?? Brushes.Transparent;

        if (values.Count < 2 || values[0] is not int step || step is < 1 or > 4) return empty;
        if (!HabitColors.TryParse(values[1] as string, out var habit)) return empty;

        var pane = (HabitTextBrushConverter.Resource<ISolidColorBrush>(app, "Brush.Pane", variant) ?? Brushes.White).Color;
        var opacity = HabitTextBrushConverter.Resource<object>(app, $"Opacity.Heat{step}", variant) is double d ? d : step / 4d;
        return new ImmutableSolidColorBrush(HabitColors.Blend(habit, opacity, pane));
    }
}
