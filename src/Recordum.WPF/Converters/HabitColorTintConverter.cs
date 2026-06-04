using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

/// <summary>Multi-binding [hexColor, intStep 0..4] → SolidColorBrush del colore abitudine
/// ad alpha crescente. Step 0 = trasparente (giorno non fatto).</summary>
public sealed class HabitColorTintConverter : IMultiValueConverter
{
    // Softer ramp (R-tintRamp): gentler toe + lower ceiling so a full day reads as a tint, not a
    // heavy slab. Steps stay perceptually even without the old hard jumps. step 0..4.
    private static readonly byte[] Alpha = { 0x00, 0x26, 0x4a, 0x78, 0xae };

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
