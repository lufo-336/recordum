using System.Globalization;
using System.Windows.Data;
using Recordum.Core.Enums;
using Recordum.WPF.Resources;

namespace Recordum.WPF.Converters;

/// <summary>Localizes the four <see cref="ScheduleType"/> values the builder's Schedule brick offers
/// (Onda 2.5 T3, F0-11: never show a raw enum name). Monthly/Custom stay Configura-only — the brick's
/// ComboBox never binds them, so they fall through to <see cref="string.Empty"/> here.</summary>
public sealed class ScheduleTypeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            ScheduleType.Daily => Strings.HabitBuilder_Schedule_Daily,
            ScheduleType.SpecificDays => Strings.HabitBuilder_Schedule_SpecificDays,
            ScheduleType.WeeklyX => Strings.HabitBuilder_Schedule_WeeklyX,
            ScheduleType.EveryXDays => Strings.HabitBuilder_Schedule_EveryXDays,
            _ => string.Empty,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
