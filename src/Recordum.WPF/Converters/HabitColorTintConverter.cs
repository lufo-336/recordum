using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

/// <summary>Multi-binding [hexColor, intStep 0..4] → SolidColorBrush del colore abitudine
/// ad alpha crescente. Step 0 = trasparente (giorno non fatto).</summary>
public sealed class HabitColorTintConverter : IMultiValueConverter
{
    private static readonly byte[] Alpha = { 0x00, 0x38, 0x66, 0x9e, 0xd8 }; // step 0..4

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string hex || values[1] is not int step)
            return Brushes.Transparent;
        step = Math.Clamp(step, 0, 4);
        if (step == 0) return Brushes.Transparent;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(Color.FromArgb(Alpha[step], c.R, c.G, c.B));
            brush.Freeze();
            return brush;
        }
        catch (FormatException) { return Brushes.Transparent; }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
