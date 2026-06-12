using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>Converts an integer indent level into a left margin (16px per level) for
/// hierarchical list rows rendered in a flat ItemsControl.</summary>
public sealed class IndentToMarginConverter : IValueConverter
{
    private const double Step = 16;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Thickness(value is int level && level > 0 ? level * Step : 0, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
