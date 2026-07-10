using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class MetricsSectionViewModel(IMetricDefinitionService metrics, IReferenceDataQueryService refData) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Metrics;

    private readonly IMetricDefinitionService _metrics = metrics;
    private readonly IReferenceDataQueryService _refData = refData;
    internal Guid NodeId { get; private set; }

    public ObservableCollection<MetricRowViewModel> Metrics { get; } = new();
    public ObservableCollection<UnitOfMeasure> AvailableUnits { get; } = new();

    public async Task LoadAsync(HabitNode node)
    {
        // Detach the previous load's rows before touching AvailableUnits: emptying the shared
        // ItemsSource makes every live unit ComboBox coerce SelectedValue to null, and an
        // attached row would persist that null — silently wiping the metric's unit.
        foreach (var row in Metrics) row.Detach();
        Metrics.Clear();
        AvailableUnits.Clear();
        foreach (var u in await _refData.ListUnitsAsync()) AvailableUnits.Add(u);

        NodeId = node.Id;
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
            Name = Strings.Editor_Default_NewMetric,
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
    private bool _detached;
    public Guid Id { get; } = metric.Id;

    [ObservableProperty] private string _name = metric.Name;
    [ObservableProperty] private MetricType _type = metric.Type;
    [ObservableProperty] private Guid? _unitOfMeasureId = metric.UnitOfMeasureId;
    [ObservableProperty] private int _sortOrder = metric.SortOrder;

    /// <summary>Stops persisting changes. Called on the old rows before a reload, so view-side
    /// side effects (ComboBox SelectedValue coercion when ItemsSource empties) can't write back.</summary>
    internal void Detach() => _detached = true;

    partial void OnNameChanged(string value) { if (!_detached) _ = _metrics.UpdateAsync(Id, m => m.Name = value).AsTask(); }
    partial void OnTypeChanged(MetricType value) { if (!_detached) _ = _metrics.UpdateAsync(Id, m => m.Type = value).AsTask(); }
    partial void OnUnitOfMeasureIdChanged(Guid? value) { if (!_detached) _ = _metrics.UpdateAsync(Id, m => m.UnitOfMeasureId = value).AsTask(); }
    partial void OnSortOrderChanged(int value) { if (!_detached) _ = _metrics.UpdateAsync(Id, m => m.SortOrder = value).AsTask(); }
}
