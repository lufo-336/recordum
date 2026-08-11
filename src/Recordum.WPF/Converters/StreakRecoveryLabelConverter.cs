using System.Globalization;
using System.Windows.Data;
using Recordum.Core.Enums;
using Recordum.WPF.Resources;

namespace Recordum.WPF.Converters;

/// <summary>Localizes <see cref="StreakRecovery"/> for the builder's Schedule brick (Onda 2.5 T3,
/// F0-11: never show a raw enum name). Mirrors <see cref="HabitTypeLabelConverter"/>'s shape.</summary>
public sealed class StreakRecoveryLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            StreakRecovery.None => Strings.HabitBuilder_Recovery_None,
            StreakRecovery.GraceDay => Strings.HabitBuilder_Recovery_GraceDay,
            StreakRecovery.Manual => Strings.HabitBuilder_Recovery_Manual,
            StreakRecovery.Retroactive => Strings.HabitBuilder_Recovery_Retroactive,
            _ => string.Empty,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
