using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Composition;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>Costruttore block-first: assembli mattoni (campi, serie, modo), vedi l'anteprima dal vivo
/// e Lanci la creazione atomica. Il <see cref="DerivedType"/> è dedotto dalla grammatica pura in Core.</summary>
public sealed partial class HabitBuilderViewModel : ObservableObject
{
    private readonly IComposedHabitService _composed;
    private readonly IHabitTemplateService _templates;
    private readonly IClock _clock;
    private Guid? _parentId;

    public HabitBuilderViewModel(IComposedHabitService composed, IHabitTemplateService templates, IClock clock)
    {
        _composed = composed;
        _templates = templates;
        _clock = clock;
    }

    public ObservableCollection<FieldBlockViewModel> Fields { get; } = new();
    public RecordingPreviewViewModel Preview { get; } = new();

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _color = "#808080";
    [ObservableProperty] private string? _icon;
    [ObservableProperty] private bool _seriesEnabled;
    [ObservableProperty] private int _seriesCount = 3;
    [ObservableProperty] private RecordingMode _mode = RecordingMode.Manual;
    [ObservableProperty] private HabitType _derivedType = HabitType.Action;
    [ObservableProperty] private Guid? _createdHabitNodeId;

    public void SetParent(Guid? parentId) => _parentId = parentId;

    private HabitComposition Snapshot() =>
        new(Fields.Select(f => f.Nature).ToList(), SeriesEnabled, Mode);

    public bool CanAddField(FieldNature nature) => HabitCompositionRules.CanAddField(Snapshot(), nature);

    [RelayCommand]
    private void AddField(FieldNature nature)
    {
        if (!CanAddField(nature)) return;
        Fields.Add(new FieldBlockViewModel(nature, DefaultName(nature)));
        Recompute();
    }

    [RelayCommand]
    private void RemoveField(FieldBlockViewModel field)
    {
        if (field is null) return;
        Fields.Remove(field);
        Recompute();
    }

    [RelayCommand]
    private void ToggleSeries(bool enabled)
    {
        if (enabled && !HabitCompositionRules.CanEnableSeries(Snapshot())) return;
        SeriesEnabled = enabled;
        Recompute();
    }

    partial void OnSeriesCountChanged(int value) => Recompute();
    partial void OnModeChanged(RecordingMode value) => Recompute();
    partial void OnNameChanged(string value) => LaunchCommand.NotifyCanExecuteChanged();

    private void Recompute()
    {
        DerivedType = HabitCompositionRules.ResolveType(Snapshot());
        Preview.Rebuild(Fields, SeriesEnabled, SeriesCount);
        LaunchCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Lancia abilitato solo con nome valido e ogni campo ben formato
    /// (Quantity → unità scelta; Scale → scala scelta). Spec §8.</summary>
    public bool CanLaunch =>
        !string.IsNullOrWhiteSpace(Name)
        && Fields.All(f =>
            (f.Nature != FieldNature.Quantity || f.UnitOfMeasureId is not null) &&
            (f.Nature != FieldNature.Scale || f.ScaleTemplateId is not null));

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync(CancellationToken ct)
    {
        CreatedHabitNodeId = await _composed.CreateComposedHabitAsync(BuildRequest(), ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveAsTemplateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Name)) return;
        var template = new HabitTemplate
        {
            Id = Guid.NewGuid(),
            Name = Name,
            IsOfficial = false,
            PayloadJson = BuildTemplatePayloadJson(),
            UpdatedAtMs = _clock.UtcNowMs,
        };
        await _templates.CreateAsync(template, ct).ConfigureAwait(true);
    }

    private ComposedHabitRequest BuildRequest()
    {
        var nodeId = Guid.NewGuid();
        var node = new HabitNode
        {
            Id = nodeId,
            ParentId = _parentId,
            NodeType = NodeType.Habit,
            HabitType = DerivedType,
            Name = Name.Trim(),
            Color = Color,
            Icon = Icon,
        };
        var metrics = Fields.Where(f => f.Nature != FieldNature.Text).Select((f, i) => new MetricDefinition
        {
            Id = f.Id,
            HabitNodeId = nodeId,
            Name = f.Name,
            Type = f.MetricType,
            UnitOfMeasureId = f.UnitOfMeasureId,
            ScaleTemplateId = f.ScaleTemplateId,
            SortOrder = i,
        }).ToList();
        var config = BuildConfig(nodeId);
        return new ComposedHabitRequest(node, config, metrics);
    }

    // TODO timer-mode: mappa RecordingMode → HabitConfig.TimerMode (Stopwatch/Countdown);
    // per questo dispatch la config è di default (null) — rifinita nell'editor post-lancio.
#pragma warning disable CA1822 // diventerà istanza con il mapping timer-mode
    private HabitConfig? BuildConfig(Guid nodeId) => null;

    // TODO Task 12: serializza la composizione corrente nella forma reale di TemplatePayload
    // (round-trip con HabitTemplateService.InstantiateAsync). Placeholder minimale per questo dispatch.
    private string BuildTemplatePayloadJson() => "{}";
#pragma warning restore CA1822

    private static string DefaultName(FieldNature n) => n switch
    {
        FieldNature.Count => "Conteggio",
        FieldNature.Quantity => "Quantità",
        FieldNature.Time => "Tempo",
        FieldNature.Scale => "Scala",
        FieldNature.Text => "Note",
        _ => "Campo",
    };
}
