using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>The Home/sidebar column width across the three workspace states:
/// (1) no subject tab active → <c>*</c> so the habit tree fills the whole window (like the old Home);
/// (2) a subject tab active + sidebar collapsed → 0;
/// (3) a subject tab active + sidebar expanded → the persisted sidebar width.
/// Inputs: [0] IsPanoramicaActive (bool), [1] SidebarCollapsed (bool), [2] SidebarWidth (double).</summary>
public sealed class HomeColumnWidthConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var panoramica = values.Length > 0 && values[0] is true;
        if (panoramica) return new GridLength(1, GridUnitType.Star);   // no tab → Home fills the window
        var collapsed = values.Length > 1 && values[1] is true;
        if (collapsed) return new GridLength(0);
        var width = values.Length > 2 && values[2] is double w && double.IsFinite(w) && w > 0 ? w : 280d;
        return new GridLength(width);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
