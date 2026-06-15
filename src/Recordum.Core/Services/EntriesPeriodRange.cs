using Recordum.Core.Enums;

namespace Recordum.Core.Services;

/// <summary>Risolve un <see cref="EntriesPeriod"/> in un intervallo di date (estremi inclusivi).
/// Semantica "calendario corrente": Settimana = settimana ISO (lun→dom) di <paramref name="today"/>,
/// Mese/Anno = mese/anno di calendario, Tutto = illimitato, Personalizzato = gli estremi passati.
/// Puro: <paramref name="today"/> arriva dal chiamante (via IClock), nessuna dipendenza da clock/UI.</summary>
public static class EntriesPeriodRange
{
    public static (DateOnly? From, DateOnly? To) Resolve(
        EntriesPeriod period, DateOnly today, DateOnly? customFrom = null, DateOnly? customTo = null) => period switch
    {
        EntriesPeriod.Settimana => WeekOf(today),
        EntriesPeriod.Mese => (new DateOnly(today.Year, today.Month, 1),
                               new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),
        EntriesPeriod.Anno => (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
        EntriesPeriod.Personalizzato => (customFrom, customTo),
        _ => (null, null), // Tutto (e fallback difensivo)
    };

    private static (DateOnly?, DateOnly?) WeekOf(DateOnly today)
    {
        // DayOfWeek: Sunday=0..Saturday=6. (+6)%7 mappa Lunedì→0 … Domenica→6 (settimana ISO lun-first).
        int sinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-sinceMonday);
        return (monday, monday.AddDays(6));
    }
}
