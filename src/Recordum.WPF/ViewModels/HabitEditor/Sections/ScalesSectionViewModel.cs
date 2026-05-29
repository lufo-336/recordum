using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

/// <summary>
/// Scales section for HabitType.State. Edits the primary metric's ScaleTemplateId + LabelsJson override.
/// Scale-template picker dropdown deferred to a follow-up iteration.
/// </summary>
public sealed partial class ScalesSectionViewModel(IMetricDefinitionService metrics) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Scales;

    private readonly IMetricDefinitionService _metrics = metrics;
    private bool _loading;
    private MetricDefinition? _primary;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private Guid? _selectedScaleTemplateId;
    [ObservableProperty] private string? _labelsJson;

    public async Task LoadAsync(HabitNode node)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            var list = await _metrics.ListByHabitAsync(node.Id);
            _primary = list.OrderBy(m => m.SortOrder).FirstOrDefault();
            SelectedScaleTemplateId = _primary?.ScaleTemplateId;
            LabelsJson = _primary?.LabelsJson;
        }
        finally { _loading = false; }
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) =>
        node == NodeType.Habit && habit == HabitType.State;

    partial void OnSelectedScaleTemplateIdChanged(Guid? value)
    {
        if (_loading || _primary is null) return;
        _ = ApplyAsync(() => _metrics.UpdateAsync(_primary.Id, m => m.ScaleTemplateId = value).AsTask());
    }

    partial void OnLabelsJsonChanged(string? value)
    {
        if (_loading || _primary is null) return;
        _ = ApplyAsync(() => _metrics.UpdateAsync(_primary.Id, m => m.LabelsJson = value).AsTask());
    }
}
