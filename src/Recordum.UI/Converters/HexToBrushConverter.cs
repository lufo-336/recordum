using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Recordum.UI.Converters;

/// <summary>Porting del gemello WPF: un colore esadecimale ("#RRGGBB" o "#AARRGGBB") diventa un pennello
/// pieno. Un valore non stringa o non interpretabile dà grigio, non un'eccezione: il colore di una
/// riga non deve poter far cadere la Home.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && Color.TryParse(hex, out var color))
            return new ImmutableSolidColorBrush(color);

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
