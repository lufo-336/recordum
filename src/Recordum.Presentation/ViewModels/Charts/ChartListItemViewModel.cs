using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.Charts;

public sealed class ChartListItemViewModel(Chart chart) : ObservableObject
{
    public Guid ChartId { get; } = chart.Id;
    public string Name { get; } = chart.Name;
    public string ChartType { get; } = chart.ChartType;
    public int SeriesCount { get; } = chart.Series.Count;
}
