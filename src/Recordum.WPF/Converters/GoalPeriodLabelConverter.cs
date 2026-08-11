using System.Globalization;
using System.Windows.Data;
using Recordum.Core.Enums;

namespace Recordum.WPF.Converters;

/// <summary>Localizes <see cref="GoalPeriodType"/> for the builder's Goal brick (Onda 2.5 T4, F0-11:
/// never show a raw enum name). Delegates to <see cref="GoalPeriodLabels"/>.</summary>
public sealed class GoalPeriodLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GoalPeriodType type ? GoalPeriodLabels.For(type) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
