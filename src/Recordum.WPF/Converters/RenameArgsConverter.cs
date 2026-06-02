using System.Globalization;
using System.Windows.Data;
using Recordum.WPF.ViewModels.HabitEditor;

namespace Recordum.WPF.Converters;

/// <summary>Packs a <see cref="PickerOption"/> + the new-name text into the tuple expected by
/// <c>HabitTemplatePickerViewModel.RenameCustomCommand</c>. Used as a MultiBinding on the Rename button's
/// CommandParameter.</summary>
public sealed class RenameArgsConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is { Length: 2 } && values[0] is PickerOption option)
            return (option, values[1] as string ?? string.Empty);
        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
