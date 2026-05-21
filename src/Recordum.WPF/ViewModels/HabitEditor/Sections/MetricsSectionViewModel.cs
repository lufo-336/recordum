using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class MetricsSectionViewModel(IMetricDefinitionService metrics) : SectionViewModelBase
{
    private readonly IMetricDefinitionService _metrics = metrics;
    internal Guid NodeId { get; private set; }

    public Task LoadAsync(HabitNode node)
    {
        NodeId = node.Id;
        _ = _metrics;
        return Task.CompletedTask;
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) =>
        node == NodeType.Habit && habit is HabitType.Counter or HabitType.Resistance;
}
