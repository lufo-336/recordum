using System.Globalization;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>Negates a bool. Used for the "either/or" radio whose checked state is the inverse of a flag
/// (e.g. the Year "Months" toggle = !YearAsHeatmap).</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}
