using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Recordum.WPF.Converters;

/// <summary>
/// Loads a file path into a frozen, fully-decoded <see cref="BitmapImage"/> (CacheOption.OnLoad).
/// WPF's default string-to-ImageSource conversion decodes lazily and keeps the source file's
/// handle open for as long as the Image is in the visual tree, which blocks a later File.Delete
/// on the same path (e.g. deleting a note attachment whose thumbnail/lightbox is still rendered).
/// </summary>
public sealed class FilePathToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
