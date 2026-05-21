using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class MetricsSectionViewModel(IMetricDefinitionService metrics) : SectionViewModelBase
{
    private readonly IMetricDefinitionService _metrics = metrics;
    internal Guid NodeId { get; private set; }

    public ObservableCollection<MetricRowViewModel> Metrics { get; } = new();

    public async Task LoadAsync(HabitNode node)
    {
        NodeId = node.Id;
        Metrics.Clear();
        foreach (var m in (await _metrics.ListByHabitAsync(node.Id)).OrderBy(m => m.SortOrder))
            Metrics.Add(new MetricRowViewModel(m, _metrics));
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) =>
        node == NodeType.Habit && habit is HabitType.Counter or HabitType.Resistance;

    [RelayCommand]
    private async Task AddMetricAsync()
    {
        if (NodeId == Guid.Empty) return;
        var draft = new MetricDefinition
        {
            Id = Guid.NewGuid(),
            HabitNodeId = NodeId,
            Name = "New metric",
            Type = MetricType.Integer,
            SortOrder = Metrics.Count,
        };
        await ApplyAsync(() => _metrics.CreateAsync(draft).AsTask());
        Metrics.Add(new MetricRowViewModel(draft, _metrics));
    }

    [RelayCommand]
    private async Task RemoveMetricAsync(MetricRowViewModel row)
    {
        if (row is null) return;
        await ApplyAsync(() => _metrics.DeleteAsync(row.Id).AsTask());
        Metrics.Remove(row);
    }
}

public sealed partial class MetricRowViewModel(MetricDefinition metric, IMetricDefinitionService metrics) : ObservableObject
{
    private readonly IMetricDefinitionService _metrics = metrics;
    public Guid Id { get; } = metric.Id;

    [ObservableProperty] private string _name = metric.Name;
    [ObservableProperty] private MetricType _type = metric.Type;
    [ObservableProperty] private Guid? _unitOfMeasureId = metric.UnitOfMeasureId;
    [ObservableProperty] private int _sortOrder = metric.SortOrder;

    partial void OnNameChanged(string value) => _ = _metrics.UpdateAsync(Id, m => m.Name = value).AsTask();
    partial void OnTypeChanged(MetricType value) => _ = _metrics.UpdateAsync(Id, m => m.Type = value).AsTask();
    partial void OnUnitOfMeasureIdChanged(Guid? value) => _ = _metrics.UpdateAsync(Id, m => m.UnitOfMeasureId = value).AsTask();
    partial void OnSortOrderChanged(int value) => _ = _metrics.UpdateAsync(Id, m => m.SortOrder = value).AsTask();
}
