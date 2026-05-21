using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

public sealed partial class SeriesRowViewModel : ObservableObject
{
    public ObservableCollection<SeriesCellViewModel> Cells { get; } = [];

    public SeriesRowViewModel(IEnumerable<MetricDefinition> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        foreach (var m in metrics) Cells.Add(new SeriesCellViewModel(m));
    }
}
