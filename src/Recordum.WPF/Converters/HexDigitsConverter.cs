using System.Globalization;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary>"#3b82f6" ⇄ "3b82f6" for the colour picker's hex field (the "#" is a fixed label).</summary>
public sealed class HexDigitsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && s.StartsWith('#') ? s[1..] : value ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = (value as string ?? string.Empty).Trim().TrimStart('#');
        return "#" + s;
    }
}
