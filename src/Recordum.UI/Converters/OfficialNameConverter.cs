using System.Globalization;
using Avalonia.Data.Converters;
using Recordum.Core.Models;
using Recordum.Presentation.Templates;

namespace Recordum.UI.Converters;

/// <summary>Il nome di un'unità o di una scala nella lingua dell'interfaccia: le ufficiali vengono dai <c>.resx</c>,
/// quelle dell'utente restano come le ha scritte. Per i menu di unità e scala (creatore e Configura).</summary>
public sealed class OfficialNameConverter : IValueConverter
{
    public static readonly OfficialNameConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            UnitOfMeasure u => OfficialTexts.UnitName(u),
            ScaleTemplate s => OfficialTexts.ScaleName(s),
            _ => string.Empty,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
