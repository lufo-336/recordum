using System.Windows;

namespace Recordum.WPF.Helpers;

/// <summary>A <see cref="Freezable"/> that carries an inherited DataContext into places that
/// have none (e.g. resource dictionaries), so a binding can reach the host element's DataContext.
/// Standard WPF pattern; used to bind the habit color into HabitScreen's overridden Brush.Accent.</summary>
public sealed class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new PropertyMetadata(null));
}
