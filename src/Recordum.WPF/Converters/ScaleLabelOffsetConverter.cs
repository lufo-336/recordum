using System.Globalization;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>Centres a stepped-slider caption on its thumb stop. The WPF slider centres the thumb at
/// <c>[half, width-half]</c> (half = the 16px thumb's radius), distributing the N stops over N-1
/// intervals. Given the captions host width, the point's 0..1 fraction and the caption's own width,
/// returns the TranslateX that lands the caption's centre on the stop — so it lines up with the thumb
/// at every step, not just the middle one.</summary>
public sealed class ScaleLabelOffsetConverter : IMultiValueConverter
{
    private const double ThumbHalf = 8d; // half of Slider.Thumb (16px) — the track inset on each side

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [double hostWidth, double fraction, double ownWidth]
            || double.IsNaN(hostWidth) || hostWidth <= 0)
            return 0d;

        var center = ThumbHalf + (hostWidth - 2 * ThumbHalf) * fraction;
        return center - ownWidth / 2d;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
