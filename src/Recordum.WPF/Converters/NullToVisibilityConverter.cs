using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>
/// null → Collapsed, anything else → Visible. Used to show/hide the editor pane based on
/// HomeViewModel.SelectedRow.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
