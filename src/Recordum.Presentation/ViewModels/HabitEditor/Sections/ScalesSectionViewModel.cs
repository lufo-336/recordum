using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Templates;
using Recordum.Presentation.ViewModels.Scales;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

/// <summary>
/// Scales section for HabitType.State. Edits the primary metric's ScaleTemplateId; under the picker the chosen scale is edited in
/// place (<see cref="Editor"/>, GIO-23): a user's scale saves on every confirmed edit, an official one is read-only and is
/// duplicated first (<see cref="DuplicateAndEditCommand"/>).
/// </summary>
public sealed partial class ScalesSectionViewModel : SectionViewModelBase
{
    public ScalesSectionViewModel(IMetricDefinitionService metrics, IReferenceDataQueryService refData, IScaleTemplateService scales)
    {
        _metrics = metrics;
        _refData = refData;
        _scales = scales;
        Editor.Changed += (_, _) => OnEditorChanged();
    }

    public override string Title => Strings.Editor_Section_Scales;

    public override string Summary =>
        AvailableScales.FirstOrDefault(s => s.Id == SelectedScaleTemplateId) is { } s ? OfficialTexts.ScaleName(s) : string.Empty;

    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IScaleTemplateService _scales;
    private int _editorGeneration;
    private bool _loading;
    private MetricDefinition? _primary;
    internal Guid NodeId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePreview))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private Guid? _selectedScaleTemplateId;

    public ObservableCollection<ScaleTemplate> AvailableScales { get; } = new();

    public ScaleEditorViewModel Editor { get; } = new();

    /// <summary>The picked scale is an official one: the editor is read-only and offers <see cref="DuplicateAndEditCommand"/>.</summary>
    public bool CanDuplicate => AvailableScales.FirstOrDefault(s => s.Id == SelectedScaleTemplateId)?.IsOfficial == true && _primary is not null;

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
        await RebuildEditorAsync();
        NotifySummary();
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) =>
        node == NodeType.Habit && habit == HabitType.State;

    /// <summary>L'editor segue la scala scelta. E' un caricamento: l'editor non solleva niente, quindi niente viene scritto.
    /// L'uso (quanti la leggono, i valori registrati) e' una query: arriva dopo e rivaluta gli errori.</summary>
    private async Task RebuildEditorAsync()
    {
        var generation = ++_editorGeneration;
        var tmpl = AvailableScales.FirstOrDefault(s => s.Id == SelectedScaleTemplateId);
        OnPropertyChanged(nameof(CanDuplicate));
        DuplicateAndEditCommand.NotifyCanExecuteChanged();
        if (tmpl is null) { Editor.Clear(); return; }
        Editor.Load(tmpl, null);
        var usage = await _scales.GetUsageAsync(tmpl.Id).ConfigureAwait(true);
        if (generation == _editorGeneration) Editor.ApplyUsage(usage);
    }

    private void OnEditorChanged()
    {
        if (_loading || !Editor.CanConfirm) return;
        var tmpl = AvailableScales.FirstOrDefault(s => s.Id == SelectedScaleTemplateId);
        if (tmpl is null or { IsOfficial: true }) return;
        var draft = Editor.ToDraft();
        _ = ApplyAsync(async () =>
        {
            await _scales.UpdateAsync(tmpl.Id, draft).AsTask().ConfigureAwait(true);
            // L'oggetto del picker resta lo stesso (sostituirlo svuoterebbe il ComboBox: CLAUDE.md §8): si aggiorna sul posto.
            tmpl.Name = draft.Name;
            tmpl.MinValue = draft.MinValue;
            tmpl.MaxValue = draft.MaxValue;
            tmpl.Precision = draft.Precision;
            tmpl.Anchors = draft.Anchors.Select(a => new ScaleAnchor { Id = Guid.NewGuid(), ScaleTemplateId = tmpl.Id, Value = a.Value, Label = a.Label }).ToList();
            NotifySummary();
        });
    }

    /// <summary>Una scala ufficiale non si modifica: se ne fa una copia dell'utente con i nomi gia' tradotti, la metrica di
    /// questa abitudine passa alla copia, e l'editor la mostra modificabile.</summary>
    [RelayCommand(CanExecute = nameof(CanDuplicate))]
    private async Task DuplicateAndEditAsync()
    {
        if (!CanDuplicate || _primary is not { } primary) return;
        var source = AvailableScales.First(s => s.Id == SelectedScaleTemplateId);
        var anchors = ScaleText.Named(source).Select(a => new ScaleAnchorDraft(a.Value, a.Name)).ToList();
        Guid copyId = default;
        await ApplyAsync(async () =>
        {
            copyId = await _scales.DuplicateAsync(source.Id, OfficialTexts.ScaleName(source), anchors, primary.Id).AsTask().ConfigureAwait(true);
        }).ConfigureAwait(true);
        if (copyId == default) return;
        // Il picker si aggiunge, non si ricostruisce (un ComboBox svuotato azzera il suo SelectedValue).
        _loading = true;
        try
        {
            foreach (var s in await _refData.ListScalesAsync().ConfigureAwait(true))
                if (AvailableScales.All(x => x.Id != s.Id)) AvailableScales.Add(s);
            primary.ScaleTemplateId = copyId;
            SelectedScaleTemplateId = copyId;
        }
        finally { _loading = false; }
        await RebuildEditorAsync().ConfigureAwait(true);
        NotifySummary();
    }

    partial void OnSelectedScaleTemplateIdChanged(Guid? value)
    {
        if (_loading) return;
        _ = RebuildEditorAsync();
        if (_primary is null) return;
        _ = ApplyAsync(() => _metrics.UpdateAsync(_primary.Id, m => m.ScaleTemplateId = value).AsTask());
    }
}
