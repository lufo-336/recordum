using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Recordum.WPF.Converters;

/// <summary>
/// Decodes photo bytes (from the database) into a frozen, fully-decoded <see cref="BitmapImage"/>
/// (CacheOption.OnLoad: the stream is read during EndInit and not kept). An integer ConverterParameter
/// is the DecodePixelWidth. Null, empty or undecodable bytes give null (no image).
/// </summary>
public sealed class BytesToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            if (parameter is not null
                && int.TryParse(parameter.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                && width > 0)
                bitmap.DecodePixelWidth = width;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
