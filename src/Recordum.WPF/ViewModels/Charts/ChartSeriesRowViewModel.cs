using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.Charts;

public sealed partial class ChartSeriesRowViewModel : ObservableObject
{
    [ObservableProperty] private Guid _habitNodeId;
    [ObservableProperty] private Guid? _metricDefinitionId;
    [ObservableProperty] private string? _label;
    [ObservableProperty] private string? _color;
}
