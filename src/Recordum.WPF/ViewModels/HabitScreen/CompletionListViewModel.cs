using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class CompletionListViewModel : ObservableObject
{
    private readonly ICompletionService _completions;

    public Guid? NodeId { get; private set; }
    public ObservableCollection<CompletionRowViewModel> Rows { get; } = [];
    public int Take { get; init; } = 20;

    // _metrics kept in the constructor signature for future per-row metric resolution
    // (e.g. resolve metric.Name for display); not stored — pass-through to CompletionRow when needed.
    public CompletionListViewModel(ICompletionService completions, IMetricDefinitionService metrics)
    {
        _completions = completions;
        _ = metrics;
    }

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var list = await _completions.ListByHabitAsync(habitNodeId, take: Take, ct: ct).ConfigureAwait(true);
        Rows.Clear();
        foreach (var c in list)
            Rows.Add(new CompletionRowViewModel(c, _completions, values: c.Values, onDeleted: row => Rows.Remove(row)));
    }
}
