using Recordum.Core.Abstractions;
using Recordum.WPF.ViewModels.HabitScreen.Calendar;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class CalendarDangerTintTests
{
    [Fact]
    public void A_lapse_day_declares_the_danger_tint_without_naming_a_color()
    {
        // Il giorno di una ricaduta dichiara la TINTA, non l'esadecimale: risolverlo qui
        // significherebbe (a) chiedere un brush a Application.Current da un ViewModel, e
        // (b) fotografare il colore al caricamento, così che un cambio di tema non lo aggiorni
        // finché il calendario non ricarica.
        var marker = new CalendarDayMarker(
            new DateOnly(2026, 9, 10), TintStep: 4, Number: null, NoteCount: 0, Tint: MarkerTint.Danger);

        Assert.Equal(MarkerTint.Danger, marker.Tint);
    }

    [Fact]
    public void An_ordinary_day_defaults_to_the_neutral_tint()
    {
        var marker = new CalendarDayMarker(new DateOnly(2026, 9, 10), TintStep: 2, Number: null, NoteCount: 0);

        Assert.Equal(MarkerTint.Default, marker.Tint);
    }
}
