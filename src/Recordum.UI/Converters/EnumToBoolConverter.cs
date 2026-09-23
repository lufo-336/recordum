using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Recordum.UI.Converters;

/// <summary>Un valore enum uguale al parametro → <c>true</c>. Al ritorno, <c>true</c> restituisce il
/// parametro (è ciò che fa funzionare un gruppo di RadioButton legato a un enum), <c>false</c> non
/// scrive nulla: deselezionare un radio non deve azzerare la proprietà. Il parametro va passato
/// tipizzato (<c>{x:Static vm:HomeTab.Active}</c>), non come stringa: il confronto è per uguaglianza.</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public static readonly EnumToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && value.Equals(parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter : BindingOperations.DoNothing;
}
