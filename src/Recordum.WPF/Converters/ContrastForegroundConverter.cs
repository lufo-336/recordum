using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

/// <summary>Picks a readable foreground (near-black or near-white) for text drawn on top of an
/// arbitrary user-chosen colour (e.g. the per-habit note marker). Uses relative luminance so the
/// digit stays legible on both light and dark note colours. (R-noteCountContrast)</summary>
public sealed class ContrastForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush s_dark = Frozen(Color.FromRgb(0x1c, 0x1d, 0x22));
    private static readonly SolidColorBrush s_light = Frozen(Color.FromRgb(0xf7, 0xf7, 0xf8));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex)) return s_dark;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            // Perceived luminance (ITU-R BT.601 weights); >0.55 → light bg → dark text.
            var luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return luminance > 0.55 ? s_dark : s_light;
        }
        catch (FormatException)
        {
            return s_dark;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
