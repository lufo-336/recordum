using System.Windows;
using System.Windows.Media.Animation;

namespace Recordum.WPF.Controls;

/// <summary>
/// Animates a pixel <see cref="GridLength"/> (WPF can't animate GridLength out of the box).
/// Used to slide the workspace/editor columns open/closed instead of snapping.
/// </summary>
public sealed class GridLengthAnimation : AnimationTimeline
{
    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(nameof(From), typeof(double), typeof(GridLengthAnimation));

    public double From
    {
        get => (double)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(nameof(To), typeof(double), typeof(GridLengthAnimation));

    public double To
    {
        get => (double)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction { get; set; }

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        ArgumentNullException.ThrowIfNull(animationClock);
        double progress = animationClock.CurrentProgress ?? 1.0;
        if (EasingFunction is { } ease) progress = ease.Ease(progress);
        var current = From + (To - From) * progress;
        return new GridLength(current, GridUnitType.Pixel);
    }
}
