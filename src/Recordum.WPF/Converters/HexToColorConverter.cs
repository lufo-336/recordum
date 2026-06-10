using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

/// <summary>Converts a habit hex color (#RRGGBB) to a <see cref="Color"/> for binding a
/// SolidColorBrush.Color. Malformed/empty input falls back to the default accent (#3B82F6)
/// so the resulting brush is never null.</summary>
public sealed class HexToColorConverter : IValueConverter
{
    private static readonly Color Fallback = (Color)ColorConverter.ConvertFromString("#3B82F6");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch (FormatException) { return Fallback; }
        }
        return Fallback;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
