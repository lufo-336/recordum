using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>A workspace pane's grid column: <c>*</c> (star) when the pane is open so open panes share
/// the tab width equally, <c>0</c> when closed so it reserves no space. This is what makes a single open
/// column fill the whole tab and multiple columns split it (no leftover empty gap).</summary>
public sealed class OpenColumnWidthConverter : IValueConverter
{
    private static readonly GridLength Star = new(1, GridUnitType.Star);
    private static readonly GridLength Zero = new(0);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var open = value is true;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase)) open = !open;
        return open ? Star : Zero;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
