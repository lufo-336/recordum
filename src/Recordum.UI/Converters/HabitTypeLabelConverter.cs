using System.Globalization;
using Avalonia.Data.Converters;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;

namespace Recordum.UI.Converters;

/// <summary>Il tipo di abitudine nel suo nome leggibile (per il tooltip della riga Home). Un valore
/// che non è un <see cref="HabitType"/> dà stringa vuota.</summary>
public sealed class HabitTypeLabelConverter : IValueConverter
{
    public static readonly HabitTypeLabelConverter Instance = new();

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
