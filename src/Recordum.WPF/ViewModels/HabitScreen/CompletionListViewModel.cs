using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class CompletionListViewModel : ObservableObject
{
    private readonly ICompletionService _completions;
    private readonly IMetricDefinitionService _metrics;

    public Guid? NodeId { get; private set; }
    public ObservableCollection<CompletionRowViewModel> Rows { get; } = [];
    public int Take { get; init; } = 20;

    public CompletionListViewModel(ICompletionService completions, IMetricDefinitionService metrics)
    {
        _completions = completions;
        _metrics = metrics;
    }

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var list = await _completions.ListByHabitAsync(habitNodeId, take: Take, ct: ct).ConfigureAwait(true);
        Rows.Clear();
        foreach (var c in list)
            Rows.Add(new CompletionRowViewModel(c, _completions, values: null, onDeleted: row => Rows.Remove(row)));
        _ = _metrics;
    }
}
