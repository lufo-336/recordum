using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Recordum.Presentation.ViewModels;

namespace Recordum.WPF.Converters;

public sealed class StateToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not HomeState state) return Visibility.Collapsed;
        if (parameter is string s && Enum.TryParse<HomeState>(s, ignoreCase: true, out var target))
            return state == target ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
