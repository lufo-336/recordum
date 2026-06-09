using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Recordum.WPF.Converters;

/// <summary>Resolves an icon resource key (e.g. "Icon.Heart") to its <see cref="Geometry"/> from the
/// application resources. Returns null for unknown keys.</summary>
public sealed class IconKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string key && Application.Current?.TryFindResource(key) is Geometry g ? g : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
