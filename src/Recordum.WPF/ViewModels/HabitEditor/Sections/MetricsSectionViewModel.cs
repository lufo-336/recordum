using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Formatting;
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
        // A snapshot, not AvailableUnits itself: each row owns its list, so emptying the section's
        // collection on the next load can never coerce a live row's SelectedValue to null (4d01f9c).
        var units = AvailableUnits.ToList();
        foreach (var m in (await _metrics.ListByHabitAsync(node.Id)).OrderBy(m => m.SortOrder))
            Metrics.Add(new MetricRowViewModel(m, _metrics, units));
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
        Metrics.Add(new MetricRowViewModel(draft, _metrics, AvailableUnits.ToList()));
    }

    [RelayCommand]
    private async Task RemoveMetricAsync(MetricRowViewModel row)
    {
        if (row is null) return;
        await ApplyAsync(() => _metrics.DeleteAsync(row.Id).AsTask());
        Metrics.Remove(row);
    }
}

public sealed partial class MetricRowViewModel : ObservableObject
{
    private readonly IMetricDefinitionService _metrics;
    private readonly IReadOnlyList<UnitOfMeasure> _allUnits;
    private bool _detached;
    private bool _suppressUnitPersist;

    public Guid Id { get; }

    /// <summary>The units this row's picker offers. Per-row, not shared with the section: a shared
    /// ItemsSource that gets emptied coerces every live ComboBox's SelectedValue to null, and an
    /// attached row persists that null — the 4d01f9c data-wipe. Filtered to the three time units on a
    /// duration, plus whatever foreign unit a legacy duration already carries (dropping it would
    /// trigger the very same coercion).</summary>
    public ObservableCollection<UnitOfMeasure> Units { get; } = new();

    [ObservableProperty] private string _name;
    [ObservableProperty] private MetricType _type;
    [ObservableProperty] private Guid? _unitOfMeasureId;
    [ObservableProperty] private int _sortOrder;

    public MetricRowViewModel(MetricDefinition metric, IMetricDefinitionService metrics, IReadOnlyList<UnitOfMeasure> allUnits)
    {
        ArgumentNullException.ThrowIfNull(metric);
        _metrics = metrics;
        _allUnits = allUnits;
        Id = metric.Id;
        _name = metric.Name;
        _type = metric.Type;
        _unitOfMeasureId = metric.UnitOfMeasureId;
        _sortOrder = metric.SortOrder;
        // Construction never coerces: an existing unit-less duration stays unit-less until the user
        // acts on it. There is no backfill, by design.
        RebuildUnits();
    }

    /// <summary>Stops persisting changes. Called on the old rows before a reload, so view-side
    /// side effects (ComboBox SelectedValue coercion when ItemsSource empties) can't write back.</summary>
    internal void Detach() => _detached = true;

    partial void OnNameChanged(string value) { if (!_detached) _ = _metrics.UpdateAsync(Id, m => m.Name = value).AsTask(); }
    partial void OnSortOrderChanged(int value) { if (!_detached) _ = _metrics.UpdateAsync(Id, m => m.SortOrder = value).AsTask(); }

    partial void OnUnitOfMeasureIdChanged(Guid? value)
    {
        if (_detached || _suppressUnitPersist) return;
        _ = _metrics.UpdateAsync(Id, m => m.UnitOfMeasureId = value).AsTask();
    }

    partial void OnTypeChanged(MetricType value)
    {
        // A duration must carry a time unit: keep one already chosen, otherwise assign minutes and
        // drop a unit that made sense for the old type (litres on what is now a duration).
        var unit = value == MetricType.Seconds ? CoerceToTimeUnit(UnitOfMeasureId) : UnitOfMeasureId;
        if (unit != UnitOfMeasureId)
        {
            // Suppress the unit's own write: type and unit go out as ONE update below, so two
            // concurrent read-modify-write calls can't race and lose one of the two changes.
            _suppressUnitPersist = true;
            UnitOfMeasureId = unit;
            _suppressUnitPersist = false;
        }
        RebuildUnits();
        if (_detached) return;
        _ = _metrics.UpdateAsync(Id, m => { m.Type = value; m.UnitOfMeasureId = unit; }).AsTask();
    }

    private Guid? CoerceToTimeUnit(Guid? current)
    {
        var chosen = _allUnits.FirstOrDefault(u => u.Id == current);
        if (chosen is not null && SecondsDisplay.IsTimeUnit(chosen.Code)) return current;
        return _allUnits.FirstOrDefault(u => string.Equals(u.Code, "minutes", StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private void RebuildUnits()
    {
        Units.Clear();
        if (Type != MetricType.Seconds)
        {
            foreach (var u in _allUnits) Units.Add(u);
            return;
        }
        foreach (var u in _allUnits.Where(u => SecondsDisplay.IsTimeUnit(u.Code))) Units.Add(u);
        // Keep a legacy duration's foreign unit selectable so the binding still finds its item.
        if (_allUnits.FirstOrDefault(u => u.Id == UnitOfMeasureId) is { } current && !Units.Contains(current))
            Units.Add(current);
    }
}
