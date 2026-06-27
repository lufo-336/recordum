using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>Centres a stepped-slider caption on its thumb stop. The WPF slider centres the thumb at
/// <c>[half, width-half]</c> (half = the 16px thumb's radius), distributing the N stops over N-1
/// intervals. Given the captions host width, the point's 0..1 fraction and the caption's own width,
/// returns a left-only <see cref="Thickness"/> margin that lands the caption's centre on the stop — so
/// it lines up with the thumb at every step, not just the middle one. The result is applied as Margin
/// (not a RenderTransform) on purpose: the width inputs come from RelativeSource bindings, which only
/// resolve on a FrameworkElement target — a Freezable like TranslateTransform sees hostWidth = 0 and
/// piles every caption at the same x.</summary>
public sealed class ScaleLabelOffsetConverter : IMultiValueConverter
{
    private const double ThumbHalf = 8d; // half of Slider.Thumb (16px) — the track inset on each side

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [double hostWidth, double fraction, double ownWidth]
            || double.IsNaN(hostWidth) || hostWidth <= 0)
            return new Thickness(0);

        var center = ThumbHalf + (hostWidth - 2 * ThumbHalf) * fraction;
        var left = center - ownWidth / 2d;
        // Clamp so the end captions stay fully on-screen instead of bleeding past the track edge
        // (a centred "Very good" on the last stop overflows the right side). The interior captions
        // never reach the clamp, so they stay centred on their thumb; only the extremes settle to the
        // edge — the usual first-left / last-right behaviour of a labelled slider.
        var maxLeft = hostWidth - ownWidth;
        if (maxLeft > 0) left = Math.Clamp(left, 0, maxLeft);
        return new Thickness(left, 0, 0, 0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
