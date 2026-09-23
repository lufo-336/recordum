using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Recordum.UI.Converters;

/// <summary>Colore dell'abitudine (#RRGGBB) più un alpha (<c>ConverterParameter</c>, byte decimale
/// 0-255) → pennello semitrasparente per i blocchi tinti. Un colore assente o malformato dà
/// <see cref="Brushes.Transparent"/>; un alpha assente o non valido dà 0x1A (~10%). Il canale alpha
/// eventualmente presente nell'esadecimale viene scartato: comanda il parametro.</summary>
public sealed class HabitTintConverter : IValueConverter
{
    public static readonly HabitTintConverter Instance = new();

    private const byte DefaultAlpha = 0x1A;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;
        if (!Color.TryParse(hex, out var c)) return Brushes.Transparent;

        return new ImmutableSolidColorBrush(Color.FromArgb(ParseAlpha(parameter), c.R, c.G, c.B));
    }

    // Il parametro arriva come stringa da `ConverterParameter=26` e come intero da un x:Static:
    // WPF conosceva solo la stringa, qui si accettano tutte e due perché il costo è una riga.
    private static byte ParseAlpha(object? parameter) => parameter switch
    {
        string s when byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) => a,
        byte b => b,
        int i and >= 0 and <= 255 => (byte)i,
        _ => DefaultAlpha,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
