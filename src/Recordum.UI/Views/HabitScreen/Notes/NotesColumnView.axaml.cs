using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;

namespace Recordum.UI.Views.HabitScreen.Notes;

public partial class NotesColumnView : UserControl
{
    public static IReadOnlyList<EntriesPeriod> Periods { get; } = Enum.GetValues<EntriesPeriod>();

    public static readonly FuncValueConverter<EntriesPeriod, string> PeriodLabel = new(p => p switch
    {
        EntriesPeriod.Settimana => Strings.Period_Week,
        EntriesPeriod.Mese => Strings.Period_Month,
        EntriesPeriod.Anno => Strings.Period_Year,
        EntriesPeriod.Tutto => Strings.Period_All,
        _ => Strings.Period_Custom,
    });

    /// <summary>"Dal… al…": il giorno del ViewModel ↔ la data del selettore (DateTime senza ora).</summary>
    public static readonly IValueConverter DayToDate = new DayDateConverter();

    // Esc chiude il visore: lo gestisce MainWindow (in tunnel, prima di chiudere una colonna).
    public NotesColumnView() => InitializeComponent();

    private sealed class DayDateConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is DateTime dt ? DateOnly.FromDateTime(dt) : null;
    }
}
