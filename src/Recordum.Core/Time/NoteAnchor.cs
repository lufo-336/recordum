using Recordum.Core.Models;

namespace Recordum.Core.Time;

/// <summary>
/// A quale giorno appartiene una nota: la data dichiarata se c'è, altrimenti il giorno in cui è
/// stata creata — secondo il confine dell'abitudine, non la data UTC.
/// <para>Esisteva come <c>note.TargetDate ?? DateOnly.FromDateTime(note.CreatedAt)</c> copiata in
/// <b>quattro</b> posti diversi. Quattro copie della stessa riga sono quattro occasioni di
/// divergere, ed è già successo con <c>LocalDayWindow</c> nella fascia "Oggi".</para>
/// </summary>
public static class NoteAnchor
{
    public static DateOnly For(Note note, DayBoundary day)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(day);
        return note.TargetDate ?? day.DayOf(note.CreatedAt);
    }
}
