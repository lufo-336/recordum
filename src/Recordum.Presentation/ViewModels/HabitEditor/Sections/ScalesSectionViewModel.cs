using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

/// <summary>
/// Scales section for HabitType.State. Edits the primary metric's ScaleTemplateId + LabelsJson override.
/// Scale-template picker dropdown deferred to a follow-up iteration.
/// </summary>
public sealed partial class ScalesSectionViewModel(IMetricDefinitionService metrics, IReferenceDataQueryService refData) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Scales;

    private readonly IMetricDefinitionService _metrics = metrics;
    private readonly IReferenceDataQueryService _refData = refData;
    private bool _loading;
    private MetricDefinition? _primary;
    internal Guid NodeId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePreview))]
    private Guid? _selectedScaleTemplateId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePreview))]
    private string? _labelsJson;

    public ObservableCollection<ScaleTemplate> AvailableScales { get; } = new();

    /// <summary>Readable preview of the effective scale labels ("value  label" per step), so the user can see
    /// what the scale means without parsing the raw JSON. The local override wins over the template's labels
    /// (same precedence as recording); null when no scale / no labels so the view collapses it.</summary>
    public string? ScalePreview
    {
        get
        {
            var tmpl = AvailableScales.FirstOrDefault(s => s.Id == SelectedScaleTemplateId);
            if (tmpl is null) return null;
            var labels = ParseLabels(LabelsJson) ?? ParseLabels(tmpl.LabelsJson);
            if (labels is not { Length: > 0 }) return null;
            return string.Join("    ", labels.Select((l, i) => $"{tmpl.MinValue + i}  {l}"));
        }
    }

    private static string[]? ParseLabels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<string[]>(json); }
        catch (JsonException) { return null; }
    }

    public async Task LoadAsync(HabitNode node)
    {
        // _loading must already cover the Clear(): emptying the bound ItemsSource makes the
        // ComboBox coerce SelectedValue to null, and the changed-handler would persist that
        // null onto the previous load's _primary — silently wiping the metric's scale.
        _loading = true;
        try
        {
            AvailableScales.Clear();
            foreach (var s in await _refData.ListScalesAsync()) AvailableScales.Add(s);

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
