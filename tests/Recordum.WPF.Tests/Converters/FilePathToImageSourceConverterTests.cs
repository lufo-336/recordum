using System.Globalization;
using System.Windows.Media.Imaging;
using Recordum.WPF.Converters;

namespace Recordum.WPF.Tests.Converters;

public class FilePathToImageSourceConverterTests
{
    private static string WriteTempJpeg()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
        var encoder = new JpegBitmapEncoder();
        var bitmap = new WriteableBitmap(2, 2, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var fs = File.Create(path))
            encoder.Save(fs);
        return path;
    }

    [WpfFact]
    public void Missing_file_converts_to_null()
    {
        var c = new FilePathToImageSourceConverter();
        var result = c.Convert("C:\\does\\not\\exist.jpg", typeof(System.Windows.Media.ImageSource), null!, CultureInfo.InvariantCulture);
        Assert.Null(result);
    }

    [WpfFact]
    public void Loaded_bitmap_does_not_lock_the_source_file()
    {
        var path = WriteTempJpeg();
        try
        {
            var c = new FilePathToImageSourceConverter();
            var image = (BitmapImage?)c.Convert(path, typeof(System.Windows.Media.ImageSource), null!, CultureInfo.InvariantCulture);

            Assert.NotNull(image);
            Assert.True(image!.IsFrozen);
            // CacheOption.OnLoad decodes fully during EndInit and releases the file handle —
            // if it didn't, this delete would throw IOException ("file in use by another process").
            File.Delete(path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
