using System.Globalization;
using System.Windows.Data;
using Recordum.Core.Enums;
using Recordum.WPF.Resources;

namespace Recordum.WPF.Converters;

/// <summary>EntriesPeriod → etichetta localizzata per il ComboBox del filtro periodo.</summary>
public sealed class EntriesPeriodLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EntriesPeriod.Settimana => Strings.Period_Week,
        EntriesPeriod.Mese => Strings.Period_Month,
        EntriesPeriod.Anno => Strings.Period_Year,
        EntriesPeriod.Tutto => Strings.Period_All,
        EntriesPeriod.Personalizzato => Strings.Period_Custom,
        _ => string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
