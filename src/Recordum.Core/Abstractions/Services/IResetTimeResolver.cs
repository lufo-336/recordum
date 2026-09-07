using Recordum.Core.Time;

namespace Recordum.Core.Abstractions.Services;

/// <summary>
/// Risolve il confine di giornata di un'abitudine: cascata <c>reset_time</c> (habit → gruppi antenati →
/// AppSetting <c>daily_reset_time</c> → <c>00:00</c>) combinata col fuso locale dell'<see cref="IClock"/>.
/// <para>È il <b>produttore</b> di <see cref="DayBoundary"/>: si chiama una volta in cima a un'operazione,
/// mai dentro un ciclo. Per più abitudini insieme esiste <see cref="GetDayBoundariesAsync"/>, che legge
/// l'albero una volta invece di ripetere la cascata per ognuna.</para>
/// </summary>
public interface IResetTimeResolver
{
    ValueTask<TimeOnly> GetEffectiveResetTimeAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>Il confine di giornata di una singola abitudine.</summary>
    ValueTask<DayBoundary> GetDayBoundaryAsync(Guid habitNodeId, CancellationToken ct = default);

    /// <summary>I confini di più abitudini in una passata. Ogni id richiesto compare nel risultato:
    /// un id sconosciuto riceve il confine di default dell'app. Include i nodi soft-deleted
    /// (l'albero Cestino passa da qui).</summary>
    ValueTask<IReadOnlyDictionary<Guid, DayBoundary>> GetDayBoundariesAsync(
        IReadOnlyCollection<Guid> habitNodeIds, CancellationToken ct = default);

    /// <summary>Il confine dell'app, senza abitudine di riferimento (default di un dialog, seeder).</summary>
    ValueTask<DayBoundary> GetAppDayBoundaryAsync(CancellationToken ct = default);
}
