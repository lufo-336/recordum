using System.Globalization;
using System.Windows.Data;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;

namespace Recordum.WPF.Converters;

public sealed class HabitTypeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            HabitType.Action => Strings.Home_HabitType_Action,
            HabitType.Counter => Strings.Home_HabitType_Counter,
            HabitType.Resistance => Strings.Home_HabitType_Resistance,
            HabitType.Journal => Strings.Home_HabitType_Journal,
            HabitType.State => Strings.Home_HabitType_State,
            _ => string.Empty,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
