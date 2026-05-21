using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class HeatmapCellViewModel(DateOnly date, int count) : ObservableObject
{
    public DateOnly Date { get; } = date;
    public int Count { get; } = count;

    /// <summary>5-step ramp 0..4 per CLAUDE.md §9: 0, 1, 2-3, 4-7, ≥8.</summary>
    public int IntensityStep => Count switch
    {
        0 => 0,
        1 => 1,
        <= 3 => 2,
        <= 7 => 3,
        _ => 4,
    };
}
