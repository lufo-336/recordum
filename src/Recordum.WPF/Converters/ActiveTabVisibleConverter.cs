using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Recordum.Presentation.ViewModels;

namespace Recordum.WPF.Converters;

public sealed class ActiveTabVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is HomeTab.Active ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
