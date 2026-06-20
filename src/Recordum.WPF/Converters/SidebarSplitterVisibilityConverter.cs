using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>The sidebar↔workspace splitter is only meaningful when a subject tab is active (so the
/// window is split into sidebar + content) and the sidebar isn't collapsed. Inputs:
/// [0] IsPanoramicaActive (bool), [1] SidebarCollapsed (bool).</summary>
public sealed class SidebarSplitterVisibilityConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var panoramica = values.Length > 0 && values[0] is true;
        var collapsed = values.Length > 1 && values[1] is true;
        return !panoramica && !collapsed ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
