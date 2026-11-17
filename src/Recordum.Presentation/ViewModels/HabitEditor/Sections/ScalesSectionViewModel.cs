using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Templates;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

/// <summary>
/// Scales section for HabitType.State. Edits the primary metric's ScaleTemplateId. The names are the scale's anchors (GIO-23: no per-metric
/// override any more); the rows (<see cref="LabelsEditor"/>) show them, read from <see cref="ScaleText"/>.
/// </summary>
public sealed partial class ScalesSectionViewModel(IMetricDefinitionService metrics, IReferenceDataQueryService refData) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Scales;

    public override string Summary =>
        AvailableScales.FirstOrDefault(s => s.Id == SelectedScaleTemplateId) is { } s ? OfficialTexts.ScaleName(s) : string.Empty;

    private readonly IMetricDefinitionService _metrics = metrics;
    private readonly IReferenceDataQueryService _refData = refData;
    private bool _loading;
    private MetricDefinition? _primary;
    internal Guid NodeId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePreview))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private Guid? _selectedScaleTemplateId;

    public ObservableCollection<ScaleTemplate> AvailableScales { get; } = new();

    public ScaleLabelsEditorViewModel LabelsEditor { get; } = new();

    /// <summary>Readable preview of the scale's named anchors ("value  name"); null when none, so the view collapses it.</summary>
    public string? ScalePreview
    {
        get
        {
            var tmpl = AvailableScales.FirstOrDefault(s => s.Id == SelectedScaleTemplateId);
            if (tmpl is null) return null;
            var named = ScaleText.Named(tmpl);
            return named.Count == 0 ? null : string.Join("    ", named.Select(a => $"{a.Value}  {a.Name}"));
        }
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
        }
        finally { _loading = false; }
        RebuildEditor();
        NotifySummary();
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) =>
        node == NodeType.Habit && habit == HabitType.State;

    /// <summary>Le righe seguono la scala scelta: una per valore, con i nomi degli scaglioni della scala
    /// (da <c>ScaleText.Named</c>, come la schermata). È un
    /// caricamento: il VM dell'elenco non solleva niente, quindi niente viene scritto.</summary>
    private void RebuildEditor()
    {
        var tmpl = AvailableScales.FirstOrDefault(s => s.Id == SelectedScaleTemplateId);
        if (tmpl is null) { LabelsEditor.LoadFixed(0, -1, []); return; }
        var named = ScaleText.Named(tmpl).ToDictionary(a => a.Value, a => a.Name);
        var labels = Enumerable.Range(tmpl.MinValue, tmpl.MaxValue - tmpl.MinValue + 1).Select(v => named.GetValueOrDefault(v, string.Empty)).ToArray();
        LabelsEditor.LoadFixed(tmpl.MinValue, tmpl.MaxValue, labels);
    }

    partial void OnSelectedScaleTemplateIdChanged(Guid? value)
    {
        if (_loading) return;
        RebuildEditor();
        if (_primary is null) return;
        _ = ApplyAsync(() => _metrics.UpdateAsync(_primary.Id, m => m.ScaleTemplateId = value).AsTask());
    }
}
