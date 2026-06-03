using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Composition;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>Costruttore block-first: assembli mattoni (campi, serie, modo), vedi l'anteprima dal vivo
/// e Lanci la creazione atomica. Il <see cref="DerivedType"/> è dedotto dalla grammatica pura in Core.</summary>
public sealed partial class HabitBuilderViewModel : ObservableObject
{
    private readonly IComposedHabitService _composed;
    private readonly IHabitTemplateService _templates;
    private readonly IReferenceDataQueryService _refData;
    private readonly IClock _clock;
    private Guid? _parentId;

    public HabitBuilderViewModel(IComposedHabitService composed, IHabitTemplateService templates,
        IReferenceDataQueryService refData, IClock clock)
    {
        _composed = composed;
        _templates = templates;
        _refData = refData;
        _clock = clock;
    }

    public ObservableCollection<FieldBlockViewModel> Fields { get; } = new();
    public RecordingPreviewViewModel Preview { get; } = new();

    /// <summary>Units and scales available to the Quantity / Scale field pickers, loaded by
    /// <see cref="InitializeAsync"/>. Without a pick, those fields keep <see cref="CanLaunch"/> false.</summary>
    public ObservableCollection<UnitOfMeasure> AvailableUnits { get; } = new();
    public ObservableCollection<ScaleTemplate> AvailableScales { get; } = new();

    /// <summary>Loads reference data for the field pickers. Called by the launcher before the dialog opens.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        AvailableUnits.Clear();
        foreach (var u in await _refData.ListUnitsAsync(ct).ConfigureAwait(true)) AvailableUnits.Add(u);
        AvailableScales.Clear();
        foreach (var s in await _refData.ListScalesAsync(ct).ConfigureAwait(true)) AvailableScales.Add(s);
    }

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

    /// <summary>True when the current composition allows the Series toggle (no Scale/Text field).
    /// Bound to the Series CheckBox <c>IsEnabled</c> so it can't be flipped into an invalid state.</summary>
    public bool CanEnableSeries => HabitCompositionRules.CanEnableSeries(Snapshot());

    [RelayCommand]
    private void AddField(FieldNature nature)
    {
        if (!CanAddField(nature)) return;
        var field = new FieldBlockViewModel(nature, DefaultName(nature));
        field.PropertyChanged += OnFieldChanged;
        Fields.Add(field);
        Recompute();
    }

    [RelayCommand]
    private void RemoveField(FieldBlockViewModel field)
    {
        if (field is null) return;
        field.PropertyChanged -= OnFieldChanged;
        Fields.Remove(field);
        Recompute();
    }

    // Picking a unit/scale on a field flips CanLaunch — re-query the Launch button (it watches CanExecuteChanged).
    private void OnFieldChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FieldBlockViewModel.UnitOfMeasureId) or nameof(FieldBlockViewModel.ScaleTemplateId))
        {
            RaiseLaunchState();
        }
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
    partial void OnNameChanged(string value) => RaiseLaunchState();

    private void Recompute()
    {
        DerivedType = HabitCompositionRules.ResolveType(Snapshot());
        Preview.Rebuild(Fields, SeriesEnabled, SeriesCount);
        OnPropertyChanged(nameof(CanEnableSeries));
        RaiseLaunchState();
    }

    private void RaiseLaunchState()
    {
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(LaunchBlocked));
        LaunchCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Lancia abilitato solo con nome valido e ogni campo ben formato
    /// (Quantity → unità scelta; Scale → scala scelta). Spec §8.</summary>
    public bool CanLaunch =>
        !string.IsNullOrWhiteSpace(Name)
        && Fields.All(f =>
            (f.Nature != FieldNature.Quantity || f.UnitOfMeasureId is not null) &&
            (f.Nature != FieldNature.Scale || f.ScaleTemplateId is not null));

    /// <summary>Inverse of <see cref="CanLaunch"/>, for the "why is Launch disabled" hint.</summary>
    public bool LaunchBlocked => !CanLaunch;

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
#pragma warning restore CA1822

    private static readonly JsonSerializerOptions s_templateJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes the current composition into the same payload shape that
    /// <c>HabitTemplateService.InstantiateAsync</c> reads (snake_case keys), so a saved custom
    /// template re-instantiates the same node + metrics. Series default → <see cref="SeriesCount"/>.</summary>
    private string BuildTemplatePayloadJson()
    {
        var unitCodeById = AvailableUnits.ToDictionary(u => u.Id, u => u.Code);
        var scaleCodeById = AvailableScales.ToDictionary(s => s.Id, s => s.Code);

        var payload = new TemplatePayload
        {
            Node = new NodePayload
            {
                Name = Name.Trim(),
                NodeType = NodeType.Habit.ToString(),
                HabitType = DerivedType.ToString(),
                Color = Color,
                Icon = Icon,
            },
            Metrics = Fields
                .Where(f => f.Nature != FieldNature.Text)
                .Select((f, i) => new MetricPayload
                {
                    Name = f.Name,
                    Type = f.MetricType.ToString(),
                    SortOrder = i,
                    // Write codes (not GUIDs) so HabitTemplateService.InstantiateAsync, which resolves
                    // unit / scale by Code, can round-trip the picked unit/scale on re-instantiate.
                    Unit = f.UnitOfMeasureId is { } uid && unitCodeById.TryGetValue(uid, out var uc) ? uc : null,
                    ScaleCode = f.ScaleTemplateId is { } sid && scaleCodeById.TryGetValue(sid, out var sc) ? sc : null,
                })
                .ToList(),
            SeriesCount = SeriesEnabled ? SeriesCount : null,
            Mode = Mode.ToString(),
        };
        return JsonSerializer.Serialize(payload, s_templateJsonOptions);
    }

    // Mirror of the payload shape consumed by HabitTemplateService.InstantiateAsync (snake_case via options).
    private sealed class TemplatePayload
    {
        public NodePayload? Node { get; set; }
        public List<MetricPayload>? Metrics { get; set; }
        public int? SeriesCount { get; set; }
        public string? Mode { get; set; }
    }

    private sealed class NodePayload
    {
        public string? Name { get; set; }
        public string? NodeType { get; set; }
        public string? HabitType { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
    }

    private sealed class MetricPayload
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public int SortOrder { get; set; }
        public string? Unit { get; set; }       // unit code (Quantity) → reader resolves to UnitOfMeasureId
        public string? ScaleCode { get; set; }  // scale code (Scale)  → reader resolves to ScaleTemplateId
    }

    private static string DefaultName(FieldNature n) => n switch
    {
        FieldNature.Count => Strings.HabitBuilder_DefaultField_Count,
        FieldNature.Quantity => Strings.HabitBuilder_DefaultField_Quantity,
        FieldNature.Time => Strings.HabitBuilder_DefaultField_Time,
        FieldNature.Scale => Strings.HabitBuilder_DefaultField_Scale,
        FieldNature.Text => Strings.HabitBuilder_DefaultField_Text,
        _ => Strings.HabitBuilder_Blocks,
    };
}
