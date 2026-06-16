using System.Globalization;
using System.Windows.Data;

namespace Recordum.WPF.Converters;

/// <summary><c>DateOnly?</c> ↔ <c>DateTime?</c> per legare un DatePicker a una proprietà DateOnly.</summary>
public sealed class DateOnlyToDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? DateOnly.FromDateTime(dt) : null;
}
