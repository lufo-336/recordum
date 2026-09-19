using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.Presentation.ViewModels.HabitScreen;

public sealed partial class HeatmapCellViewModel : ObservableObject
{
    public HeatmapCellViewModel(DateOnly date, int count, int? overrideIntensityStep = null, bool isOutOfRange = false)
    {
        Date = date;
        Count = count;
        IsOutOfRange = isOutOfRange;
        IntensityStep = isOutOfRange ? 0 : overrideIntensityStep ?? ComputeFallback(count);
    }

    public DateOnly Date { get; }
    public int Count { get; }
    public bool IsOutOfRange { get; }

    public int IntensityStep { get; }

    public string TooltipText => $"{Date:yyyy-MM-dd} — {Count}";

    private static int ComputeFallback(int count) => count switch
    {
        0 => 0,
        1 => 1,
        <= 3 => 2,
        <= 7 => 3,
        _ => 4,
    };
}
