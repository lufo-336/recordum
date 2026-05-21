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

    public event EventHandler<Guid>? EditRequested;
    public event EventHandler<Guid>? DeleteRequested;

    public CompletionListViewModel(ICompletionService completions, IMetricDefinitionService metrics)
    {
        _completions = completions;
        _metrics = metrics;
    }

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var list = await _completions.ListByHabitAsync(habitNodeId, take: 50, ct: ct).ConfigureAwait(true);
        Rows.Clear();
        foreach (var c in list)
        {
            var row = new CompletionRowViewModel(c);
            row.EditRequested += (_, id) => EditRequested?.Invoke(this, id);
            row.DeleteRequested += (_, id) => OnDelete(id);
            Rows.Add(row);
        }
        _ = _metrics;
    }

    private async void OnDelete(Guid id)
    {
        await _completions.DeleteAsync(id).ConfigureAwait(true);
        var row = Rows.FirstOrDefault(r => r.Id == id);
        if (row is not null) Rows.Remove(row);
        DeleteRequested?.Invoke(this, id);
    }
}
