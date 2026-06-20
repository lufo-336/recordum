using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>Maps (SidebarCollapsed, SidebarWidth) → a column <see cref="GridLength"/>: a collapsed
/// sidebar yields width 0 so the workspace reclaims the space (otherwise the column keeps reserving
/// its width and the hidden sidebar leaves an empty gap).</summary>
public sealed class SidebarWidthConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var collapsed = values.Length > 0 && values[0] is true;
        if (collapsed) return new GridLength(0);
        var width = values.Length > 1 && values[1] is double w && double.IsFinite(w) && w > 0 ? w : 280d;
        return new GridLength(width);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
