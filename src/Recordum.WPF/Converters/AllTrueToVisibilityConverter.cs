using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>Visible only when every bound boolean is true; otherwise Collapsed. Used to show a column
/// resize splitter only when the two panes it sits between are both open (so it never tries to resize a
/// closed, zero-width column).</summary>
public sealed class AllTrueToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.All(v => v is true) ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
