using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Recordum.UI.Converters;

/// <summary>A number box whose empty value means "none" (<c>int?</c>): empty text writes null instead of being rejected,
/// a whole number writes itself, anything else is a validation error (the box turns red, <c>RejectedTextGuard</c> and the
/// creator's launch gate see it). Without it Avalonia refuses "" for an <c>int?</c> and clearing the box is an error.</summary>
public sealed class OptionalIntConverter : IValueConverter
{
    public static readonly OptionalIntConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? i.ToString(culture) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string)?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return int.TryParse(text, NumberStyles.Integer, culture, out var n)
            ? n
            : new BindingNotification(new FormatException(text), BindingErrorType.DataValidationError);
    }
}
