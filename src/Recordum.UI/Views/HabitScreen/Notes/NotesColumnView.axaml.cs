using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;

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

    public NotesColumnView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    // Esc chiude il visore; a visore chiuso lo lascia passare (Esc esce anche dal focus della schermata).
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not NotesViewModel { IsLightboxOpen: true } vm) return;
        vm.CloseLightboxCommand.Execute(null);
        e.Handled = true;
    }

    private sealed class DayDateConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is DateTime dt ? DateOnly.FromDateTime(dt) : null;
    }
}
