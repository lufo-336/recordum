using System.Globalization;
using Avalonia.Data.Converters;

namespace Recordum.UI.Converters;

/// <summary>Un intero maggiore di zero → <c>true</c> (per esempio: l'intestazione dei giorni c'è se c'è una striscia).</summary>
public sealed class IsPositiveConverter : IValueConverter
{
    public static readonly IsPositiveConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is int n && n > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
