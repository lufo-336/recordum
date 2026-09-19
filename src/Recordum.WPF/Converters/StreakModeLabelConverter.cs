using System.Globalization;
using System.Windows.Data;
using Recordum.Core.Enums;
using Recordum.Presentation.Formatting;

namespace Recordum.WPF.Converters;

/// <summary>Localizes <see cref="StreakMode"/> for the builder's Schedule brick (Onda 2.5 T3, F0-11:
/// never show a raw enum name). Delegates to <see cref="StreakLabels"/> — the single source of truth
/// shared with <see cref="StreakRecoveryLabelConverter"/> and <c>ScheduleBrickViewModel.BuildSummary</c>.</summary>
public sealed class StreakModeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is StreakMode mode ? StreakLabels.For(mode) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
