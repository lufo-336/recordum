using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Recordum.Core.Abstractions;

namespace Recordum.WPF.Converters;

/// <summary>Multi-binding [hexColor, intStep 0..4, MarkerTint?] → SolidColorBrush del colore
/// abitudine (o del brush di pericolo, tema corrente) ad alpha crescente. Step 0 = trasparente
/// (giorno non fatto).</summary>
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

        // Terzo valore: la tinta richiesta. Danger risolve Brush.Danger contro il tema CORRENTE,
        // invece di ricevere un esadecimale fotografato al caricamento dei marker — che è il
        // motivo per cui, cambiando tema, il calendario teneva il colore vecchio.
        if (values.Length > 2 && values[2] is MarkerTint.Danger)
        {
            if (Application.Current?.TryFindResource("Brush.Danger") is SolidColorBrush danger)
                hex = danger.Color.ToString(CultureInfo.InvariantCulture);
        }

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
