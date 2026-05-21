using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class HeatmapViewModel : ObservableObject
{
    private readonly ICompletionService _completions;
    private readonly IClock _clock;

    public Guid? NodeId { get; private set; }
    public ObservableCollection<HeatmapCell> Cells { get; } = [];

    public event EventHandler<DateOnly>? DateClicked;

    public HeatmapViewModel(ICompletionService completions, IClock clock)
    {
        _completions = completions;
        _clock = clock;
    }

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var from = today.AddDays(-90);
        var cells = await _completions.GetHeatmapAsync(habitNodeId, from, today, ct).ConfigureAwait(true);
        Cells.Clear();
        foreach (var c in cells) Cells.Add(c);
    }

    public void RaiseDateClicked(DateOnly date) => DateClicked?.Invoke(this, date);
}
