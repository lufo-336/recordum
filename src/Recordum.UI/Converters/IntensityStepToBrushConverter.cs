using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Recordum.UI.Converters;

/// <summary>Porting diretto del gemello WPF. Due differenze di meccanismo, nessuna di logica:
/// IValueConverter viene da Avalonia.Data.Converters, e la ricerca della risorsa passa da
/// Application.Current.TryGetResource(key, theme, out value), che vuole la variante di tema.
/// Limite ereditato (in WPF era lo stesso): la variante è quella dell'APPLICAZIONE e il valore
/// non si ricalcola quando il tema cambia a runtime.</summary>
public sealed class IntensityStepToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            int i and >= 0 and <= 4 => $"Brush.Heatmap.Level{i}",
            _ => "Brush.Heatmap.Level0",
        };

        var app = global::Avalonia.Application.Current;
        if (app is not null && app.TryGetResource(key, app.ActualThemeVariant, out var found) && found is IBrush brush)
            return brush;

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
