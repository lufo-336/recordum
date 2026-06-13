using System.Globalization;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>"✓" when true, "○" when false. Used for the series validity marker in the
/// counter quick-add (spec multivalue §4.1): a row is valid when any cell carries a value.</summary>
public sealed class BoolToCheckMarkConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "✓" : "○";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
