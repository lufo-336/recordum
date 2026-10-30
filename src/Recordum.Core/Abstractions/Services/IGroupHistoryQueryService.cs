using System.Collections.Immutable;
using Recordum.Core.Enums;

namespace Recordum.Core.Abstractions.Services;

/// <summary>La storia di una figlia di un gruppo. <paramref name="StartsOn"/> è l'inizio dell'abitudine
/// (il più vecchio fra il giorno di creazione e quello della prima registrazione; per un'astinenza l'inizio
/// del primo periodo), nel confine di giornata della figlia. <paramref name="Depth"/> 0 = figlia diretta.</summary>
public sealed record GroupChildHistory(
    Guid HabitNodeId,
    string Name,
    string ColorHex,
    HabitType Type,
    int Depth,
    DateOnly StartsOn,
    ImmutableHashSet<DateOnly> Scheduled,
    ImmutableHashSet<DateOnly> Done);

public interface IGroupHistoryQueryService
{
    /// <summary>Le abitudini discendenti (non archiviate, non nel cestino) del gruppo, in ordine d'albero,
    /// con i giorni previsti e fatti dentro [from, to].</summary>
#pragma warning disable CA1716 // Parameter name 'to' conflicts with reserved keyword
    ValueTask<IReadOnlyList<GroupChildHistory>> GetAsync(
        Guid groupId, DateOnly from, DateOnly to, CancellationToken ct = default);
#pragma warning restore CA1716
}

/// <summary>La regola dell'inizio, scritta una volta sola (vedi <see cref="GroupChildHistory"/>).
/// Per un'astinenza mai iniziata torna il giorno di creazione: chi chiama decide cosa farne.</summary>
public interface IHabitStartQueryService
{
    ValueTask<DateOnly> GetStartDayAsync(Guid habitNodeId, CancellationToken ct = default);
}
