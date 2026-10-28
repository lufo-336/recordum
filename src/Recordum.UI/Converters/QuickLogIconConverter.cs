using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Recordum.Core.Enums;

namespace Recordum.UI.Converters;

/// <summary>Il tipo dell'abitudine → l'icona Lucide del suo log rapido in Home: azione check, contatore plus, stato
/// sliders-horizontal, diario pen-line, astinenza rotate-ccw. Un tipo assente (gruppo) non ha icona.</summary>
public sealed class QuickLogIconConverter : IValueConverter
{
    public static readonly QuickLogIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            HabitType.Action => "Icon.Check",
            HabitType.Counter => "Icon.Plus",
            HabitType.State => "Icon.SlidersHorizontal",
            HabitType.Journal => "Icon.PenLine",
            HabitType.Resistance => "Icon.RotateCcw",
            _ => null,
        };
        return key is not null && global::Avalonia.Application.Current?.TryFindResource(key, out var g) == true
            ? g as Geometry : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
