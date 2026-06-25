using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>The sidebar↔workspace splitter is only meaningful when a subject tab is active (so the
/// window is split into sidebar + content) and the sidebar isn't collapsed. Hidden too in focus
/// "zen", where the sidebar is gone and the panel fills the window. Inputs:
/// [0] IsPanoramicaActive (bool), [1] SidebarCollapsed (bool), [2] IsFocusZen (bool).</summary>
public sealed class SidebarSplitterVisibilityConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var panoramica = values.Length > 0 && values[0] is true;
        var collapsed = values.Length > 1 && values[1] is true;
        var zen = values.Length > 2 && values[2] is true;
        return !panoramica && !collapsed && !zen ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
