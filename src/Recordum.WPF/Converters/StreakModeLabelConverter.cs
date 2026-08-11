using System.Globalization;
using System.Windows.Data;
using Recordum.Core.Enums;
using Recordum.WPF.Resources;

namespace Recordum.WPF.Converters;

/// <summary>Localizes <see cref="StreakMode"/> for the builder's Schedule brick (Onda 2.5 T3, F0-11:
/// never show a raw enum name). Mirrors <see cref="HabitTypeLabelConverter"/>'s shape.</summary>
public sealed class StreakModeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            StreakMode.Daily => Strings.HabitBuilder_StreakMode_Daily,
            StreakMode.Weekly => Strings.HabitBuilder_StreakMode_Weekly,
            StreakMode.Occurrences => Strings.HabitBuilder_StreakMode_Occurrences,
            StreakMode.Both => Strings.HabitBuilder_StreakMode_Both,
            _ => string.Empty,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
