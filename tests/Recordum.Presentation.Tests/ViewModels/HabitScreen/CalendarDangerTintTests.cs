using FluentAssertions;
using Recordum.Core.Abstractions;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class CalendarDangerTintTests
{
    private static CalendarNavigatorViewModel NewSut(CalendarMarkerProvider provider)
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        return new CalendarNavigatorViewModel(clock)
        {
            CompletedColorHex = "#e08a4a",
            NoteColorHex = "#e0b341",
            MarkerProvider = provider,
        };
    }

    [Fact]
    public async Task A_lapse_day_keeps_the_habit_color_hex_and_carries_the_danger_tint()
    {
        // Il punto delicato del task: TintColorHex resta SEMPRE il colore dell'abitudine (un dato
        // vero, scelto dall'utente) — anche il giorno di una ricaduta. È Tint a dire alla view di
        // sovrascriverlo col brush di pericolo, non un esadecimale fotografato qui dentro il
        // ViewModel. Questo test attraversa ApplyMarkersAsync (via LoadAsync, come già fanno i test
        // in CalendarNavigatorViewModelTests.cs) e legge la CELLA, non il marker: se
        // ApplyMarkersAsync tornasse a scrivere l'hex di pericolo dentro TintColorHex — l'anti-
        // pattern "cell.TintColorHex = m.Tint == MarkerTint.Danger ? dangerHex : CompletedColorHex"
        // — la seconda asserzione lo scopre, perché TintColorHex smetterebbe di essere
        // CompletedColorHex. Un test sul solo CalendarDayMarker non se ne accorgerebbe: il marker
        // dichiara la tinta correttamente anche nel codice rotto, è la cella (dopo ApplyMarkersAsync)
        // a tradirla.
        CalendarMarkerProvider provider = (from, to, ct) => Task.FromResult(
            new CalendarMarkers(new[]
            {
                new CalendarDayMarker(new DateOnly(2026, 6, 10), TintStep: 4, Number: null, NoteCount: 0, Tint: MarkerTint.Danger),
            }));
        var sut = NewSut(provider);

        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);

        var cell = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 10));
        cell.Tint.Should().Be(MarkerTint.Danger);
        cell.TintColorHex.Should().Be("#e08a4a"); // mai un hex di pericolo: sempre il colore dell'abitudine
    }

    [Fact]
    public async Task An_ordinary_day_gets_the_default_tint_and_the_habit_color_hex()
    {
        // Stesso percorso reale di sopra, giorno non tinto di pericolo. Il marker non specifica Tint
        // — esattamente come fa HabitScreenViewModel per un "clean day" (new CalendarDayMarker(day,
        // TintStep: 3, Number: null, NoteCount: noteCount), senza Tint:) — quindi questo test copre
        // anche il default del record, ma attraversando ApplyMarkersAsync invece che per sola
        // costruzione diretta: prova la propagazione alla cella, non solo la dichiarazione.
        CalendarMarkerProvider provider = (from, to, ct) => Task.FromResult(
            new CalendarMarkers(new[]
            {
                new CalendarDayMarker(new DateOnly(2026, 6, 11), TintStep: 3, Number: null, NoteCount: 0),
            }));
        var sut = NewSut(provider);

        await sut.LoadAsync(2026, 6, selected: null, DayBoundary.Utc);

        var cell = sut.Cells.Single(c => c.Date == new DateOnly(2026, 6, 11));
        cell.Tint.Should().Be(MarkerTint.Default);
        cell.TintColorHex.Should().Be("#e08a4a");
    }
}
