using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Composition;
using Recordum.Core.Enums;
using Recordum.Core.Formatting;
using Recordum.Core.Models;
using Recordum.WPF.Resources;
using Recordum.WPF.Services;
using Recordum.WPF.ViewModels.HabitEditor.Sections;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>One named entry of a repeatable composition (Onda 2.5 T6) — "Night", "Nap"… The names
/// travel in <c>HabitConfig.SeriesLabelsJson</c> and only alongside the Repeatable flag.</summary>
public sealed partial class SeriesLabelRowViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
}

/// <summary>Costruttore block-first: assembli mattoni (campi, contatore, astinenza), vedi l'anteprima dal vivo
/// e Lanci la creazione atomica. Il <see cref="DerivedType"/> è dedotto dalla grammatica pura in Core.</summary>
public sealed partial class HabitBuilderViewModel : ObservableObject
{
    private readonly IComposedHabitService _composed;
    private readonly IHabitTemplateService _templates;
    private readonly IReferenceDataQueryService _refData;
    private readonly IClock _clock;
    private readonly IRecentColorStore _recentColors;
    private readonly IFavoriteColorStore _favoriteColors;
    private readonly IScreenColorPicker _screenPicker;
    private readonly IHabitUiPrefsService _uiPrefs;
    private Guid? _parentId;

    public HabitBuilderViewModel(IComposedHabitService composed, IHabitTemplateService templates,
        IReferenceDataQueryService refData, IClock clock, IRecentColorStore recentColors,
        IFavoriteColorStore favoriteColors, IScreenColorPicker screenPicker, IHabitUiPrefsService uiPrefs)
    {
        _composed = composed;
        _templates = templates;
        _refData = refData;
        _clock = clock;
        _recentColors = recentColors;
        _favoriteColors = favoriteColors;
        _screenPicker = screenPicker;
        _uiPrefs = uiPrefs;
        Schedule.Changed += (_, _) => TemplateSaved = false;
        // Unlike Schedule, the Goal brick's Target/HasGoal feed CanLaunch (an incomplete goal blocks
        // launch, R7-style) — so its Changed also has to re-query the Launch button, not only clear
        // the "Saved" badge.
        GoalBrick.Changed += (_, _) => { TemplateSaved = false; RaiseLaunchState(); };
        SessionBrick.Changed += (_, _) => TemplateSaved = false;
        SeriesLabels.CollectionChanged += OnSeriesLabelsChanged;
    }

    public ObservableCollection<FieldBlockViewModel> Fields { get; } = new();
    public RecordingPreviewViewModel Preview { get; } = new();

    /// <summary>Mattone Programma (Onda 2.5 T3): schedule + streak + recovery del draft, applicato
    /// all'<see cref="HabitConfig"/> da <see cref="BuildConfig"/>.</summary>
    public ScheduleBrickViewModel Schedule { get; } = new();

    /// <summary>Mattone Obiettivo (Onda 2.5 T4): goal opzionale del draft, applicato alla
    /// <see cref="ComposedHabitRequest.Goal"/> da <see cref="BuildRequest"/>. Disponibile solo quando
    /// la composizione deriva Counter — <see cref="Recompute"/> ne ricalcola <c>IsAvailable</c>.</summary>
    public GoalBrickViewModel GoalBrick { get; } = new();

    /// <summary>Mattone Sessione (Onda 2.5 T5): integrated timer + named segments of the draft,
    /// applied to the launched <see cref="HabitConfig"/> by <see cref="BuildConfig"/> and to
    /// <see cref="ComposedHabitRequest.Segments"/> by <see cref="BuildRequest"/>. Available only when
    /// the composition derives a non-tally Counter (fields to record per segment) — <see cref="Recompute"/>
    /// recalculates <c>IsAvailable</c>.</summary>
    public SessionBrickViewModel SessionBrick { get; } = new();

    /// <summary>Units and scales available to the Quantity / Scale field pickers, loaded by
    /// <see cref="InitializeAsync"/>.</summary>
    public ObservableCollection<UnitOfMeasure> AvailableUnits { get; } = new();
    public ObservableCollection<ScaleTemplate> AvailableScales { get; } = new();

    /// <summary>Recently picked habit colors, loaded from <see cref="IRecentColorStore"/> during
    /// <see cref="InitializeAsync"/>. Bound to the <c>ColorSwatchPicker.RecentColors</c> DP.</summary>
    public ObservableCollection<string> RecentColors { get; } = new();

    /// <summary>User-pinned favourite colours (bound to ColorSwatchPicker.FavoriteColors).</summary>
    public ObservableCollection<string> FavoriteColors { get; } = new();

    /// <summary>Units offered for a Time field (hours / minutes / seconds). Picking one is required:
    /// a duration with no unit has nothing to express its totals, its goal or its export rows in.
    /// The list comes from Core so it cannot drift from what the formatters treat as a time unit.</summary>
    public ObservableCollection<UnitOfMeasure> AvailableTimeUnits { get; } = new();

    // ===== Unità e scale custom staged (Onda 2.5 T7) =====
    // Definizioni create dentro il builder e non ancora nel DB: viaggiano nel ComposedHabitRequest
    // (Lancia le materializza nella stessa transazione) o incorporate nel payload del template
    // (InstantiateAsync le risolve-o-crea). Restano nel draft finché non le referenzia una metrica.
    private readonly List<UnitOfMeasure> _stagedUnits = new();
    private readonly List<ScaleTemplate> _stagedScales = new();
    private FieldBlockViewModel? _formTarget;

    public IReadOnlyList<UnitOfMeasure> StagedUnits => _stagedUnits;
    public IReadOnlyList<ScaleTemplate> StagedScales => _stagedScales;

    /// <summary>The open micro-form, or null when none is. Bound to the popup's IsOpen; only one of
    /// the two is ever non-null.</summary>
    [ObservableProperty] private NewUnitFormViewModel? _unitForm;
    [ObservableProperty] private NewScaleFormViewModel? _scaleForm;

    [RelayCommand]
    public void BeginAddCustomUnit(FieldBlockViewModel? field)
    {
        if (field is null) return;
        ScaleForm = null;
        _formTarget = field;
        UnitForm = new NewUnitFormViewModel();
    }

    [RelayCommand]
    public void BeginAddCustomScale(FieldBlockViewModel? field)
    {
        if (field is null) return;
        UnitForm = null;
        _formTarget = field;
        ScaleForm = new NewScaleFormViewModel();
    }

    [RelayCommand]
    private void CancelCustomForm()
    {
        UnitForm = null;
        ScaleForm = null;
        _formTarget = null;
    }

    [RelayCommand]
    private void ConfirmCustomUnit()
    {
        // Belt and braces (R7): the disabled button is not the guard — an incomplete form has to be
        // refused here too, and refusing leaves the form open rather than losing what was typed.
        if (UnitForm is not { CanConfirm: true } form || _formTarget is null) return;
        var unit = new UnitOfMeasure
        {
            Id = Guid.NewGuid(),
            Code = ReferenceCodeSlug.From(form.Name, AvailableUnits.Select(u => u.Code).ToList()),
            Name = form.Name.Trim(),
            Symbol = string.IsNullOrWhiteSpace(form.Symbol) ? null : form.Symbol.Trim(),
            IsOfficial = false,
        };
        _stagedUnits.Add(unit);
        // Add, never rebuild: AvailableUnits is the live ItemsSource of the field pickers and clearing
        // it would coerce their two-way SelectedValue to null (4d01f9c / 1d96967). A staged unit stays
        // out of AvailableTimeUnits on purpose — the time units are closed in Core.
        AvailableUnits.Add(unit);
        _formTarget.UnitOfMeasureId = unit.Id;
        CancelCustomForm();
    }

    [RelayCommand]
    private void ConfirmCustomScale()
    {
        if (ScaleForm is not { CanConfirm: true } form || _formTarget is null) return;
        var labels = form.Labels;
        var scale = new ScaleTemplate
        {
            Id = Guid.NewGuid(),
            Code = ReferenceCodeSlug.From(form.Name, AvailableScales.Select(s => s.Code).ToList()),
            Name = form.Name.Trim(),
            MinValue = 1,
            MaxValue = labels.Count,
            LabelsJson = JsonSerializer.Serialize(labels),
            IsOfficial = false,
        };
        _stagedScales.Add(scale);
        AvailableScales.Add(scale);
        _formTarget.ScaleTemplateId = scale.Id;
        CancelCustomForm();
    }

    /// <summary>Loads reference data for the field pickers. Called by the launcher before the dialog opens.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        AvailableUnits.Clear();
        foreach (var u in await _refData.ListUnitsAsync(ct).ConfigureAwait(true)) AvailableUnits.Add(u);
        AvailableScales.Clear();
        foreach (var s in await _refData.ListScalesAsync(ct).ConfigureAwait(true)) AvailableScales.Add(s);
        AvailableTimeUnits.Clear();
        foreach (var u in AvailableUnits.Where(u => Recordum.Core.Formatting.SecondsDisplay.IsTimeUnit(u.Code)))
            AvailableTimeUnits.Add(u);
        RecentColors.Clear();
        foreach (var c in await _recentColors.GetAsync(ct).ConfigureAwait(true)) RecentColors.Add(c);
        await ReloadFavoritesAsync(ct).ConfigureAwait(true);
    }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _color = "#808080";
    [ObservableProperty] private string? _icon;
    [ObservableProperty] private bool _countsOccurrences;
    [ObservableProperty] private bool _abstinence;
    [ObservableProperty] private HabitType _derivedType = HabitType.Action;
    [ObservableProperty] private Guid? _createdHabitNodeId;

    /// <summary>Counter-only knob (U8): can this habit be recorded multiple times a day (named
    /// series) or once (a second same-day save sums into the day total)? Flows into the launched
    /// <see cref="HabitConfig"/> and the saved template payload; refined later in Configura.</summary>
    [ObservableProperty] private bool _repeatable;

    /// <summary>Names of the daily entries of a repeatable composition (Onda 2.5 T6) — the "Night" /
    /// "Nap" of the Sleep seed. Serialized into <c>HabitConfig.SeriesLabelsJson</c> by
    /// <see cref="BuildConfig"/>, and only together with <see cref="Repeatable"/>: without the flag
    /// there is one entry a day and nothing to name.</summary>
    public ObservableCollection<SeriesLabelRowViewModel> SeriesLabels { get; } = new();

    [RelayCommand]
    private void AddSeriesLabel() => SeriesLabels.Add(new SeriesLabelRowViewModel());

    [RelayCommand]
    private void RemoveSeriesLabel(SeriesLabelRowViewModel? row)
    {
        if (row is null) return;
        SeriesLabels.Remove(row);
    }

    private void OnSeriesLabelsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (SeriesLabelRowViewModel row in e.OldItems) row.PropertyChanged -= OnSeriesLabelRowChanged;
        if (e.NewItems is not null)
            foreach (SeriesLabelRowViewModel row in e.NewItems) row.PropertyChanged += OnSeriesLabelRowChanged;
        TemplateSaved = false;
    }

    private void OnSeriesLabelRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        TemplateSaved = false;

    /// <summary>Gesto primario di una composizione journal (Onda 2.4). Serializzato in ui_prefs sia
    /// quando la composizione si salva come template, sia quando Lancia crea l'abitudine (I3: prima
    /// non veniva applicata, solo scritta nel template); Configura resta l'unico posto dove si cambia
    /// dopo la creazione.</summary>
    [ObservableProperty] private JournalPrimaryGesture _journalGesture = JournalPrimaryGesture.Diary;

    public bool ShowJournalGesture => DerivedType == HabitType.Journal;

    public IReadOnlyList<JournalGestureOption> JournalGestureOptions { get; } =
    [
        new(JournalPrimaryGesture.Diary, Strings.Editor_Registration_Gesture_Diary),
        new(JournalPrimaryGesture.QuickNote, Strings.Editor_Registration_Gesture_QuickNote),
    ];

    /// <summary>True briefly after "Save as template" succeeds; cleared on any edit that would
    /// change the payload, so the "Saved ✓" badge reflects the current composition. (R-builderSaveFeedback)</summary>
    [ObservableProperty] private bool _templateSaved;

    public void SetParent(Guid? parentId) => _parentId = parentId;

    private HabitComposition Snapshot() =>
        new(Fields.Select(f => f.Nature).ToList(), CountsOccurrences, Abstinence);

    public bool CanAddField(FieldNature nature) => HabitCompositionRules.CanAddField(Snapshot(), nature);

    /// <summary>True when the current composition allows the Counter block (empty canvas, no abstinence).
    /// Bound to the Counter CheckBox <c>IsEnabled</c> so it can't be flipped into an invalid state.</summary>
    public bool CanEnableCounter => HabitCompositionRules.CanEnableCounter(Snapshot());

    /// <summary>True when the current composition allows the Abstinence block (no text field, no counter).</summary>
    public bool CanEnableAbstinence => HabitCompositionRules.CanEnableAbstinence(Snapshot());

    /// <summary>False when the bare "Counter (+1)" block is on — a tap counter has no fields, so the
    /// builder disables the whole field palette (instead of letting clicks silently no-op) and shows
    /// a hint. Bound to the palette <c>IsEnabled</c>.</summary>
    public bool FieldsAllowed => !CountsOccurrences;

    partial void OnCountsOccurrencesChanged(bool value) => Recompute();
    partial void OnAbstinenceChanged(bool value) => Recompute();

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
            TemplateSaved = false;
            RaiseLaunchState();
        }
    }

    partial void OnNameChanged(string value) { TemplateSaved = false; RaiseLaunchState(); }
    partial void OnColorChanged(string value) => TemplateSaved = false;
    partial void OnIconChanged(string? value) => TemplateSaved = false;
    partial void OnRepeatableChanged(bool value) => TemplateSaved = false;
    partial void OnJournalGestureChanged(JournalPrimaryGesture value) => TemplateSaved = false;

    [RelayCommand]
    private async Task AddFavoriteAsync(string? hex, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        await _favoriteColors.AddAsync(hex, ct).ConfigureAwait(true);
        await ReloadFavoritesAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RemoveFavoriteAsync(string? hex, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        await _favoriteColors.RemoveAsync(hex, ct).ConfigureAwait(true);
        await ReloadFavoritesAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task EyedropAsync()
    {
        var picked = await _screenPicker.PickAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(picked)) Color = picked;
    }

    private async Task ReloadFavoritesAsync(CancellationToken ct = default)
    {
        FavoriteColors.Clear();
        foreach (var c in await _favoriteColors.GetAsync(ct).ConfigureAwait(true)) FavoriteColors.Add(c);
    }

    private void Recompute()
    {
        TemplateSaved = false;
        DerivedType = HabitCompositionRules.ResolveType(Snapshot());
        // GoalBrickViewModel.OnIsAvailableChanged forces HasGoal off when it goes false — the brick
        // switches itself off rather than leaving an orphaned goal in a draft that stopped deriving
        // Counter (e.g. adding a Text field flips it to Journal).
        GoalBrick.IsAvailable = DerivedType == HabitType.Counter;
        // SessionBrickViewModel.OnIsAvailableChanged forces HasSession off (and clears the seeded
        // segments) when it goes false — a tap counter has no per-segment fields to record, so the
        // brick switches itself off rather than leaving orphaned segments in a draft that no longer
        // supports them (same pattern as GoalBrick).
        SessionBrick.IsAvailable = DerivedType == HabitType.Counter && !CountsOccurrences;
        bool hasCells = Fields.Any(f => f.Nature != FieldNature.Text);
        Preview.Rebuild(Fields, CountsOccurrences, DerivedType == HabitType.Counter && hasCells);
        OnPropertyChanged(nameof(CanEnableCounter));
        OnPropertyChanged(nameof(CanEnableAbstinence));
        OnPropertyChanged(nameof(FieldsAllowed));
        OnPropertyChanged(nameof(ShowJournalGesture));
        RaiseLaunchState();
    }

    private void RaiseLaunchState()
    {
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(LaunchBlocked));
        LaunchCommand.NotifyCanExecuteChanged();
        SaveAsTemplateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Gates both Launch and Save-as-template: a valid name, a scale on every Scale field and
    /// a time unit on every Time field. A Quantity field's unit stays optional — a bare number reads
    /// fine — but a duration without a unit has no unit to render its totals in. Saving is gated by the
    /// same rule because a template is a saved composition and <c>InstantiateAsync</c> materialises it
    /// with no gate of its own: a template you cannot launch would create the malformed habit later.</summary>
    public bool CanLaunch =>
        !string.IsNullOrWhiteSpace(Name)
        && Fields.All(f => f.Nature != FieldNature.Scale || f.ScaleTemplateId is not null)
        && Fields.All(f => f.Nature != FieldNature.Time || f.UnitOfMeasureId is not null)
        && (!GoalBrick.HasGoal || GoalBrick.Target is > 0);

    /// <summary>Inverse of <see cref="CanLaunch"/>, for the "why is Launch disabled" hint.</summary>
    public bool LaunchBlocked => !CanLaunch;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync(CancellationToken ct)
    {
        // Belt and braces (R7 precedent): AsyncRelayCommand.ExecuteAsync does not consult CanExecute,
        // so an incomplete goal (HasGoal with no positive target) has to be refused here too, not
        // only on the greyed-out button.
        if (!CanLaunch) return;
        var habitId = await _composed.CreateComposedHabitAsync(BuildRequest(), ct).ConfigureAwait(true);
        CreatedHabitNodeId = habitId;

        // I3: the gesture knob previously only ever reached a saved template's payload — Lancia itself
        // never applied it, so a "Quick note with photo" composition still launched with the Markdown
        // editor. Mirrors HabitTemplateService.InstantiateAsync's post-create ui_prefs write.
        if (DerivedType == HabitType.Journal)
        {
            await _uiPrefs.UpdateAsync(habitId, p => p with { JournalGesture = JournalGesture }, ct)
                .ConfigureAwait(true);
        }

        // Recording the picked color is a best-effort MRU side effect — never let it undo a created
        // habit or crash the app if the write hiccups (e.g. a transient DB lock). See B-dbLock.
        try
        {
            await _recentColors.AddAsync(Color, ct).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // ignore: the habit is already persisted; the recent-color list is cosmetic.
        }
    }

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task SaveAsTemplateAsync(CancellationToken ct)
    {
        // Belt and braces: AsyncRelayCommand.ExecuteAsync does not consult CanExecute, so the rule has
        // to hold here too, not only on the greyed-out button.
        if (!CanLaunch) return;
        var template = new HabitTemplate
        {
            Id = Guid.NewGuid(),
            Name = Name,
            IsOfficial = false,
            PayloadJson = BuildTemplatePayloadJson(),
            UpdatedAtMs = _clock.UtcNowMs,
        };
        await _templates.CreateAsync(template, ct).ConfigureAwait(true);
        TemplateSaved = true;
    }

    /// <summary>Test seam: <see cref="BuildRequest"/> is otherwise private (Launch is the only
    /// production caller); tests need to inspect the composed request without going through the
    /// DB-backed <see cref="IComposedHabitService"/>.</summary>
    internal ComposedHabitRequest BuildRequestForTest() => BuildRequest();

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
            // Snapshot of the scale's steps, as the template reader does — the State quick-add reads
            // the labels from the metric, not from the scale.
            LabelsJson = LabelsOfScale(f.ScaleTemplateId),
            SortOrder = i,
        }).ToList();
        var config = BuildConfig(nodeId);
        var goal = GoalBrick.Build(nodeId, Name.Trim(), metrics.FirstOrDefault()?.Id);
        var segments = SessionBrick.HasSession ? SessionBrick.BuildSegments(nodeId) : null;
        // Only the staged definitions a metric actually points at: dropping the field that used one
        // leaves it in the draft (so re-adding the field can pick it again) but it must not be
        // materialised for nobody.
        var usedUnits = metrics.Where(m => m.UnitOfMeasureId is not null).Select(m => m.UnitOfMeasureId!.Value).ToHashSet();
        var usedScales = metrics.Where(m => m.ScaleTemplateId is not null).Select(m => m.ScaleTemplateId!.Value).ToHashSet();
        var newUnits = _stagedUnits.Where(u => usedUnits.Contains(u.Id)).ToList();
        var newScales = _stagedScales.Where(s => usedScales.Contains(s.Id)).ToList();
        return new ComposedHabitRequest(node, config, metrics, Segments: segments, Goal: goal,
            NewUnits: newUnits.Count > 0 ? newUnits : null,
            NewScales: newScales.Count > 0 ? newScales : null);
    }

    // Default config so streak/scheduling engines work out of the box; the editor refines it later.
    // Instance method (not static) since U8 needs the builder's Repeatable knob and Onda 2.5 T3 needs
    // Schedule (+ T5 needs SessionBrick).
    private HabitConfig BuildConfig(Guid nodeId)
    {
        var cfg = new HabitConfig { Id = Guid.NewGuid(), HabitNodeId = nodeId, Repeatable = Repeatable };
        Schedule.ApplyTo(cfg);
        SessionBrick.ApplyTo(cfg);
        var labels = NamedSeriesLabels();
        cfg.SeriesLabelsJson = Repeatable && labels.Length > 0 ? JsonSerializer.Serialize(labels) : null;
        return cfg;
    }

    private string? LabelsOfScale(Guid? scaleId) =>
        scaleId is { } id ? AvailableScales.FirstOrDefault(s => s.Id == id)?.LabelsJson : null;

    /// <summary>The series labels the user actually named: blank rows are an in-progress edit, not an
    /// unnamed entry. Shared by <see cref="BuildConfig"/> and the template payload so a launched habit
    /// and its saved template carry the same list.</summary>
    private string[] NamedSeriesLabels() =>
        SeriesLabels.Select(l => l.Name.Trim()).Where(n => n.Length > 0).ToArray();

    private static readonly JsonSerializerOptions s_templateJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes the current composition into the same payload shape that
    /// <c>HabitTemplateService.InstantiateAsync</c> reads (snake_case keys), so a saved custom
    /// template re-instantiates the same node + metrics.</summary>
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
            Config = Repeatable ? new ConfigPayload { Repeatable = true } : null,
            UiPrefs = DerivedType == HabitType.Journal
                ? new UiPrefsPayload { JournalGesture = JournalGesture.ToString() }
                : null,
            Metrics = Fields
                .Where(f => f.Nature != FieldNature.Text)
                .Select(f => new MetricPayload
                {
                    Name = f.Name,
                    Type = f.MetricType.ToString(),
                    // Codici (non GUID) perche' InstantiateAsync risolve unita' e scala per Code.
                    Unit = f.UnitOfMeasureId is { } uid && unitCodeById.TryGetValue(uid, out var uc) ? uc : null,
                    ScaleCode = f.ScaleTemplateId is { } sid && scaleCodeById.TryGetValue(sid, out var sc) ? sc : null,
                })
                .ToList(),
        };
        return JsonSerializer.Serialize(payload, s_templateJsonOptions);
    }

    // Mirror della forma consumata da HabitTemplateService.InstantiateAsync (snake_case via options).
    // I nomi e l'annidamento devono restare identici a quelli del servizio: una chiave scritta qui e
    // non letta la' e' silenziosamente persa al re-instanziamento.
    private sealed class TemplatePayload
    {
        public NodePayload? Node { get; set; }
        public ConfigPayload? Config { get; set; }
        public UiPrefsPayload? UiPrefs { get; set; }
        public List<MetricPayload>? Metrics { get; set; }
    }

    private sealed class ConfigPayload
    {
        // null-when-false (WhenWritingNull) cosi' i template salvati prima di U8 restano byte-identici.
        public bool? Repeatable { get; set; }
    }

    private sealed class UiPrefsPayload
    {
        public string? JournalGesture { get; set; }
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
        public string? Unit { get; set; }       // unit code (Quantity) → reader resolves to UnitOfMeasureId
        public string? ScaleCode { get; set; }  // scale code (Scale)  → reader resolves to ScaleTemplateId
    }

    private static string DefaultName(FieldNature n) => n switch
    {
        FieldNature.Quantity => Strings.HabitBuilder_DefaultField_Quantity,
        FieldNature.Time => Strings.HabitBuilder_DefaultField_Time,
        FieldNature.Scale => Strings.HabitBuilder_DefaultField_Scale,
        FieldNature.Text => Strings.HabitBuilder_DefaultField_Text,
        _ => Strings.HabitBuilder_Blocks,
    };
}
