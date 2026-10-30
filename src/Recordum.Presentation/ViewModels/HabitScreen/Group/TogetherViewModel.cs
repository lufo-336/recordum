using System.Collections.ObjectModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.ViewModels.HabitScreen.History;

namespace Recordum.Presentation.ViewModels.HabitScreen.Group;

/// <summary>"Insieme" (spec §6.3): le figlie di un gruppo in righe, una cella per giorno del periodo. Non calcola altro:
/// previsto e fatto vengono dalla query (ogni figlia col proprio confine del giorno); "oggi" è quello del gruppo.
/// Senza query (WPF) resta vuoto.</summary>
public sealed class TogetherViewModel(IGroupHistoryQueryService? history)
{
    private int _generation;

    public ObservableCollection<TogetherRow> Rows { get; } = [];

    /// <summary>Celle: gradino 4 se fatta, 0 se prevista e non fatta; fuori periodo di vita (non prevista) <c>InRange</c> falso;
    /// dopo <paramref name="today"/> <c>IsFuture</c>. <paramref name="order"/>: gli id nell'ordine delle righe.</summary>
    public async Task LoadAsync(Guid groupId, DateOnly from, DateOnly to, DateOnly today,
        IReadOnlyList<Guid>? order = null, CancellationToken ct = default)
    {
        var generation = ++_generation;
        if (history is null) { Rows.Clear(); return; }
        var children = await history.GetAsync(groupId, from, to, ct).ConfigureAwait(true);
        if (generation != _generation) return; // un caricamento più nuovo ha vinto

        // Un solo ordine nella colonna: quello delle righe della Home (chi non c'è in <order> resta in coda, nell'ordine della query).
        var rank = order?.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var rows = children.OrderBy(c => rank is not null && rank.TryGetValue(c.HabitNodeId, out var r) ? r : int.MaxValue).Select(c =>
        {
            var cells = new List<HistoryDayCell>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                var done = c.Done.Contains(d);
                cells.Add(new HistoryDayCell(d, done || c.Scheduled.Contains(d), d == today, d > today) { Step = done ? 4 : 0 });
            }
            return new TogetherRow(c.HabitNodeId, c.Name, c.ColorHex, c.Depth, cells);
        }).ToList();

        Rows.Clear();
        foreach (var r in rows) Rows.Add(r);
    }
}
