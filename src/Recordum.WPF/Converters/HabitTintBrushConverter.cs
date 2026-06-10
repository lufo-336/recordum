using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

/// <summary>Converts a habit hex color (#RRGGBB) plus an alpha (ConverterParameter, decimal byte
/// 0-255) into a semi-transparent <see cref="SolidColorBrush"/> for the tinted habit blocks.
/// Returns <see cref="Brushes.Transparent"/> for malformed/empty input.</summary>
public sealed class HabitTintBrushConverter : IValueConverter
{
    private const byte DefaultAlpha = 0x1A; // ~10%

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(Color.FromArgb(ParseAlpha(parameter), c.R, c.G, c.B));
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    private static byte ParseAlpha(object? parameter) =>
        parameter is string s && byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
            ? a
            : DefaultAlpha;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
