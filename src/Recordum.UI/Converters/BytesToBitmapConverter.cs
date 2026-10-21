using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace Recordum.UI.Converters;

/// <summary>I byte di una miniatura (dal database) in un <see cref="Bitmap"/> decodificato a 160 px di larghezza:
/// la miniatura non resta mai in memoria piu' grande di quanto si disegna. Null o byte illeggibili → null (la view
/// mostra l'icona "foto non disponibile").</summary>
public sealed class BytesToBitmapConverter : IValueConverter
{
    public static readonly BytesToBitmapConverter Instance = new();
    public const int DecodeWidth = 160;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            return Bitmap.DecodeToWidth(stream, DecodeWidth);
        }
#pragma warning disable CA1031 // una miniatura rotta non deve rompere la lista
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
