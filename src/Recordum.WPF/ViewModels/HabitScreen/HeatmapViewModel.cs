using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class HeatmapViewModel : ObservableObject
{
    private const int Days = 91; // 13 cols × 7 rows ending today

    private readonly ICompletionService _completions;
    private readonly IClock _clock;

    public Guid? NodeId { get; private set; }
    public ObservableCollection<HeatmapCellViewModel> Cells { get; } = [];

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
        var from = today.AddDays(-(Days - 1));
        var raw = await _completions.GetHeatmapAsync(habitNodeId, from, today, ct).ConfigureAwait(true);
        var byDate = raw.ToDictionary(c => c.Date, c => c.Count);
        Cells.Clear();
        for (int i = 0; i < Days; i++)
        {
            var d = from.AddDays(i);
            Cells.Add(new HeatmapCellViewModel(d, byDate.GetValueOrDefault(d, 0)));
        }
    }

    [RelayCommand]
    private void ClickCell(HeatmapCellViewModel? cell)
    {
        if (cell is null) return;
        DateClicked?.Invoke(this, cell.Date);
    }
}
