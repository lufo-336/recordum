using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

public sealed class IntensityStepToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            int i and >= 0 and <= 4 => $"Brush.Heatmap.Level{i}",
            _ => "Brush.Heatmap.Level0",
        };
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
