using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using Recordum.UI.Theming;

namespace Recordum.UI.Converters;

/// <summary>[gradino 0–4, colore dell'abitudine (hex), variante del tema] → il colore del numero del giorno sopra la
/// cella di <see cref="HeatLevelBrushConverter"/>: fra il testo del tema chiaro e quello del tema scuro, quello che
/// contrasta di più con la cella (GIO-24: il testo scuro su un cremisi pieno faceva 3,5:1). Su una cella vuota non
/// risponde, e il numero tiene il colore del suo stile (fuori mese, ricaduta).</summary>
public sealed class HeatLevelTextBrushConverter : IMultiValueConverter
{
    private static readonly HeatLevelBrushConverter Fill = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = Application.Current;
        if (app is null || values.Count < 2 || values[0] is not int step || step is < 1 or > 4
            || !HabitColors.TryParse(values[1] as string, out _)
            || Fill.Convert(values, typeof(IBrush), null, culture) is not ISolidColorBrush cell)
            return AvaloniaProperty.UnsetValue;

        var dark = HabitTextBrushConverter.Resource<ISolidColorBrush>(app, "Brush.Text", ThemeVariant.Light);
        var light = HabitTextBrushConverter.Resource<ISolidColorBrush>(app, "Brush.Text", ThemeVariant.Dark);
        if (dark is null || light is null) return AvaloniaProperty.UnsetValue;
        return HabitColors.Contrast(dark.Color, cell.Color) >= HabitColors.Contrast(light.Color, cell.Color) ? dark : light;
    }
}
