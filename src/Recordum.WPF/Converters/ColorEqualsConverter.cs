using System;
using System.Globalization;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>True when two #RRGGBB strings name the same colour (case-insensitive). Drives the
/// selection ring on palette / recent / favourite swatches.</summary>
public sealed class ColorEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [string a, string b]) return false;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
