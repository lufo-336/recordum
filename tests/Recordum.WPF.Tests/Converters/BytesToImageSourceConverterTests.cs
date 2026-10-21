using System.Globalization;
using System.Windows.Media.Imaging;
using Recordum.WPF.Converters;

namespace Recordum.WPF.Tests.Converters;

public class BytesToImageSourceConverterTests
{
    private static byte[] Jpeg()
    {
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(new WriteableBitmap(8, 8, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null)));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    [WpfFact]
    public void Null_empty_or_garbage_bytes_convert_to_null()
    {
        var c = new BytesToImageSourceConverter();
        Assert.Null(c.Convert(null, typeof(System.Windows.Media.ImageSource), null!, CultureInfo.InvariantCulture));
        Assert.Null(c.Convert(Array.Empty<byte>(), typeof(System.Windows.Media.ImageSource), null!, CultureInfo.InvariantCulture));
        Assert.Null(c.Convert(new byte[] { 1, 2, 3 }, typeof(System.Windows.Media.ImageSource), null!, CultureInfo.InvariantCulture));
    }

    [WpfFact]
    public void Bytes_decode_to_a_frozen_bitmap_and_the_parameter_is_the_decode_width()
    {
        var c = new BytesToImageSourceConverter();
        var image = (BitmapImage?)c.Convert(Jpeg(), typeof(System.Windows.Media.ImageSource), "4", CultureInfo.InvariantCulture);
        Assert.NotNull(image);
        Assert.True(image!.IsFrozen);
        Assert.Equal(4, image.PixelWidth);
    }
}
