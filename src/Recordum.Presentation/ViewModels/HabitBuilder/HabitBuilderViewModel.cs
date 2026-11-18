using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Composition;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Formatting;
using Recordum.Core.Models;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Templates;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.Presentation.ViewModels.Scales;

namespace Recordum.Presentation.ViewModels.HabitBuilder;

/// <summary>One named entry of a repeatable composition (Onda 2.5 T6) — "Night", "Nap"… The names
/// travel in <c>HabitConfig.SeriesLabelsJson</c> and only alongside the Repeatable flag.</summary>
public sealed partial class SeriesLabelRowViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
}

/// <summary>The creator's number boxes whose rejected text must block "Create habit" (the view reports them through
/// <see cref="HabitBuilderViewModel.SetInputRejected"/>): their empty value is a valid "none", so clearing it alone would
/// let a red box create the habit without what it shows.</summary>
public enum BuilderNumberInput { Tolerance, RollingDays, Rest }

/// <summary>One of the creator's four field tiles: on when the grammar allows the field, with the line that says what it is
/// for or why it is off (<see cref="HabitBuilderViewModel.FieldTileHint"/>).</summary>
public sealed partial class FieldTileViewModel(FieldNature nature) : ObservableObject
{
    public FieldNature Nature { get; } = nature;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _hint = string.Empty;
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
    private Guid? _parentId;

    public HabitBuilderViewModel(IComposedHabitService composed, IHabitTemplateService templates,
        IReferenceDataQueryService refData, IClock clock, IRecentColorStore recentColors,
        IFavoriteColorStore favoriteColors, IScreenColorPicker screenPicker)
    {
        _composed = composed;
        _templates = templates;
        _refData = refData;
        _clock = clock;
        _recentColors = recentColors;
        _favoriteColors = favoriteColors;
        _screenPicker = screenPicker;
        _previewFactory = new SamplePreviewFactory(clock);
        // Every brick feeds the preview, so they all go through Recompute — which also clears the
        // "Saved" badge and re-queries Launch (an incomplete goal blocks it, R7-style).
        Schedule.Changed += (_, _) => Recompute();
        GoalBrick.Changed += (_, _) => Recompute();
        SessionBrick.Changed += (_, _) => Recompute();
        SeriesLabels.CollectionChanged += OnSeriesLabelsChanged;
        Recompute();
    }

    private readonly SamplePreviewFactory _previewFactory;

    public ObservableCollection<FieldBlockViewModel> Fields { get; } = new();

    /// <summary>The three-tier live preview (E7), rebuilt by <see cref="Recompute"/> on every change
    /// of the draft. A record, so the pane re-renders wholesale instead of watching a dozen
    /// properties.</summary>
    [ObservableProperty] private BuilderPreviewModel? _preview;

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
    [ObservableProperty] private ScaleEditorViewModel? _scaleForm;

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
        ScaleForm = new ScaleEditorViewModel();
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
        // Gli scaglioni viaggiano con il nome; i tag li risolve il servizio al salvataggio (come per ogni scala in staging).
        var scale = form.ToTemplate(Guid.NewGuid(), ReferenceCodeSlug.From(form.Name, AvailableScales.Select(x => x.Code).ToList()));
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

    /// <summary>Descrizione che il modello porta all'abitudine (tradotta se il modello e' ufficiale). Il creatore non la
    /// mostra: e' di Configura (spec §6.4); qui viaggia solo dal modello alla richiesta di creazione.</summary>
    [ObservableProperty] private string? _description;
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
        Recompute();
    }

    private void OnSeriesLabelRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        Recompute();

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


    /// <summary>Riempie il creatore dal modello <paramref name="templateId"/>: l'inverso di
    /// <see cref="BuildTemplatePayloadJson"/>, e per la parte che il creatore sa comporre lo stesso risultato di
    /// <c>HabitTemplateService.InstantiateAsync</c>. Da chiamare su un creatore nuovo, dopo <see cref="InitializeAsync"/>.
    /// I testi di un modello ufficiale passano da <see cref="OfficialTexts"/> (per Id), quelli di un modello dell'utente
    /// restano com'e' li ha scritti. Un'unita' o una scala che non si trova lascia il campo incompleto (lo blocca
    /// <see cref="CanLaunch"/>), mai un'eccezione; un modello che non esiste invece e' un errore di chi chiama.</summary>
    public async Task LoadTemplateAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await _templates.GetAsync(templateId, ct).ConfigureAwait(true)
            ?? throw new HabitTemplateNotFoundException(templateId);
        var payload = ParseTemplatePayload(template.PayloadJson);
        string Text(string seeded) => OfficialTexts.TemplateText(templateId, seeded);
        var config = payload.Config;

        Name = Text(payload.Node?.Name ?? template.Name);
        Description = OfficialTexts.TemplateDescription(template);
        if (!string.IsNullOrWhiteSpace(payload.Node?.Color)) Color = payload.Node.Color;
        Icon = payload.Node?.Icon;
        if (payload.UiPrefs?.JournalGesture is { } gesture
            && Enum.TryParse<JournalPrimaryGesture>(gesture, ignoreCase: true, out var parsedGesture))
            JournalGesture = parsedGesture;

        // Il tipo non si carica: lo deriva la grammatica dai mattoni. Il payload non dice "contatore nudo", "astinenza" o
        // "testo": lo dice il tipo, e un contatore senza metriche e' un contatore nudo.
        var habitType = Enum.TryParse<HabitType>(payload.Node?.HabitType, ignoreCase: true, out var ht) ? ht : HabitType.Action;
        var metrics = payload.Metrics?.Where(m => !string.IsNullOrWhiteSpace(m.Name)).ToList() ?? [];
        if (habitType == HabitType.Resistance) Abstinence = true;
        else if (habitType == HabitType.Counter && metrics.Count == 0) CountsOccurrences = true;
        else if (habitType == HabitType.Journal) AddField(FieldNature.Text);
        foreach (var m in metrics) LoadMetric(m, payload, Text);

        // Il lettore tratta un payload senza "config" cosi': il resto del programma resta quello di default di HabitConfig,
        // che per il recupero della serie e' None (il creatore parte da GraceDay).
        if (config is null)
        {
            Repeatable = payload.RepeatableLegacy ?? false;
            Schedule.StreakRecovery = StreakRecovery.None;
        }
        else
        {
            Schedule.ScheduleType = ParseEnum(config.ScheduleType, ScheduleType.Daily);
            Schedule.WeeklyTarget = config.ScheduleWeeklyTarget;
            Schedule.EveryXDays = config.ScheduleEveryXDays;
            Schedule.StreakMode = ParseEnum(config.StreakMode, StreakMode.Daily);
            Schedule.StreakRecovery = ParseEnum(config.StreakRecovery, StreakRecovery.None);
            Schedule.StreakToleranceMinutes = config.StreakToleranceMinutes;
            foreach (var day in DaysOf(config.ScheduleDaysJson))
                if (Schedule.Days.FirstOrDefault(d => d.DayKey == day) is { } toggle) toggle.IsSelected = true;
            Repeatable = config.Repeatable ?? payload.RepeatableLegacy ?? false;
            foreach (var label in config.SeriesLabels ?? [])
                SeriesLabels.Add(new SeriesLabelRowViewModel { Name = Text(label) });
            LoadSession(config, payload.Segments, Text);
        }
        LoadGoal(payload.Goal, Text);
    }

    private void LoadMetric(MetricPayload m, TemplatePayload payload, Func<string, string> text)
    {
        var type = ParseEnum(m.Type, MetricType.Integer);
        var nature = type == MetricType.Seconds ? FieldNature.Time
            : m.ScaleCode is not null ? FieldNature.Scale
            : FieldNature.Quantity;
        if (!CanAddField(nature)) return;   // una composizione che la grammatica rifiuta (modello a mano) non si carica a meta'
        AddField(nature);
        var field = Fields[^1];
        field.Name = text(m.Name!);
        if (nature == FieldNature.Scale) field.ScaleTemplateId = FindOrStageScale(m.ScaleCode, payload.Scales);
        else field.UnitOfMeasureId = FindOrStageUnit(m.Unit, payload.Units);
        // Un decimale senza unita' si ricorda a parte (il peso del Registro allenamento): con l'unita' lo e' gia'.
        if (nature == FieldNature.Quantity && field.UnitOfMeasureId is null) field.AllowsDecimals = type == MetricType.Decimal;
    }

    // Il lettore risolve per codice senza badare alle maiuscole; qui uguale. Un codice che il catalogo non ha ma che il
    // modello definisce (un'unita' inventata nel creatore) si prepara di nuovo, come ConfirmCustomUnit.
    private Guid? FindOrStageUnit(string? code, List<UnitDefPayload>? definitions)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (AvailableUnits.FirstOrDefault(u => string.Equals(u.Code, code, StringComparison.OrdinalIgnoreCase)) is { } known)
            return known.Id;
        if (definitions?.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)) is not { } def)
            return null;
        var unit = new UnitOfMeasure
        {
            Id = Guid.NewGuid(),
            Code = def.Code!,
            Name = string.IsNullOrWhiteSpace(def.Name) ? def.Code! : def.Name,
            Symbol = def.Symbol,
            IsOfficial = false,
        };
        _stagedUnits.Add(unit);
        AvailableUnits.Add(unit);
        return unit.Id;
    }

    private Guid? FindOrStageScale(string? code, List<ScaleDefPayload>? definitions)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (AvailableScales.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase)) is { } known)
            return known.Id;
        if (definitions?.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)) is not { } def)
            return null;
        var scale = new ScaleTemplate
        {
            Id = Guid.NewGuid(),
            Code = def.Code!,
            Name = string.IsNullOrWhiteSpace(def.Name) ? def.Code! : def.Name,
            MinValue = def.MinValue,
            MaxValue = def.Anchors is { Count: > 0 } ? def.MaxValue : Math.Max(def.MaxValue, def.MinValue + (def.Labels?.Count ?? 0) - 1),
            Precision = Enum.TryParse<ScalePrecision>(def.Precision, true, out var precision) ? precision : ScalePrecision.Integer,
            Anchors = (def.Anchors is { Count: > 0 } named
                    ? named.Select(a => new ScaleAnchor { Id = Guid.NewGuid(), Value = a.Value, Label = a.Label })
                    // A template saved before GIO-23 carries names in a row: position i is value MinValue + i.
                    : (def.Labels ?? []).Select((l, i) => new ScaleAnchor { Id = Guid.NewGuid(), Value = def.MinValue + i, Label = l }))
                .ToList(),
            IsOfficial = false,
        };
        _stagedScales.Add(scale);
        AvailableScales.Add(scale);
        return scale.Id;
    }

    private void LoadSession(ConfigPayload config, List<SegmentPayload>? segments, Func<string, string> text)
    {
        // Il creatore sa comporre solo la sessione; un cronometro semplice o un tempo obiettivo restano a Configura.
        // Disponibile solo con i campi gia' al loro posto (Recompute l'ha acceso), come in una composizione a mano.
        if (!SessionBrick.IsAvailable || ParseEnum(config.TimerMode, TimerMode.Stopwatch) != TimerMode.Session) return;
        SessionBrick.HasSession = true;
        // Via i due segmenti di partenza, uno alla volta (mai Clear su una collezione bindata): il modello dice i suoi.
        while (SessionBrick.Segments.Count > 0) SessionBrick.Segments.RemoveAt(SessionBrick.Segments.Count - 1);
        foreach (var s in (segments ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Name)).OrderBy(s => s.SortOrder))
            SessionBrick.Segments.Add(new SegmentRowViewModel { Name = text(s.Name!), IsRest = s.IsRest });
        SessionBrick.RestTimerSeconds = config.RestTimerSeconds;
        SessionBrick.TimerAutoPrompt = config.TimerAutoPrompt ?? false;
    }

    private void LoadGoal(GoalPayload? goal, Func<string, string> text)
    {
        if (goal is null || !GoalBrick.IsAvailable) return;
        GoalBrick.HasGoal = true;
        GoalBrick.Target = goal.Target;
        GoalBrick.Name = string.IsNullOrWhiteSpace(goal.Name) ? null : text(goal.Name);
        // Fixed vuole inizio e fine, che il creatore non ha: un modello a mano o di WPF con un periodo fisso si carica
        // aperto, tenendo traguardo e nome (Configura lo rende di nuovo fisso).
        var period = ParseEnum(goal.PeriodType, GoalPeriodType.Open);
        GoalBrick.PeriodType = period == GoalPeriodType.Fixed ? GoalPeriodType.Open : period;
        GoalBrick.RollingDays = goal.RollingDays;
        // La finestra mobile senza il suo numero: la riga sta in Avanzate, lo si apre (e la riga del perche' la nomina).
        if (GoalBrick is { PeriodType: GoalPeriodType.Rolling, RollingDays: not > 0 }) AdvancedOpen = true;
    }

    /// <summary>Quanto serve alla scheda di un modello nel selettore, letto dal payload senza caricare un creatore: il colore,
    /// il tipo che il modello dichiara, se si registra piu' volte al giorno e il gesto del diario. Le stesse regole di
    /// <see cref="LoadTemplateAsync"/> (repeatable alla radice per i modelli di prima della 2.4).</summary>
    internal static (string? Color, HabitType Type, bool Repeatable, JournalPrimaryGesture Gesture) DescribeTemplate(string? payloadJson)
    {
        var payload = ParseTemplatePayload(payloadJson);
        return (payload.Node?.Color,
            ParseEnum(payload.Node?.HabitType, HabitType.Action),
            payload.Config?.Repeatable ?? payload.RepeatableLegacy ?? false,
            ParseEnum(payload.UiPrefs?.JournalGesture, JournalPrimaryGesture.Diary));
    }

    private static string[] DaysOf(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }   // un elenco di giorni illeggibile e' un programma incompleto, non un errore
    }

    private static TemplatePayload ParseTemplatePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new TemplatePayload();
        try { return JsonSerializer.Deserialize<TemplatePayload>(json, s_templateJsonOptions) ?? new TemplatePayload(); }
        catch (JsonException) { return new TemplatePayload(); }   // come il lettore: un payload rotto e' un modello vuoto
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private HabitComposition Snapshot() =>
        new(Fields.Select(f => f.Nature).ToList(), CountsOccurrences, Abstinence);

    public bool CanAddField(FieldNature nature) => HabitCompositionRules.CanAddField(Snapshot(), nature);

    /// <summary>The four field tiles of the creator ("Cosa registri"): on or off as <see cref="CanAddField"/> says, with the
    /// line under each one (<see cref="FieldTileHint"/>). Kept in step by <see cref="Recompute"/>.</summary>
    public FieldTileViewModel QuantityTile { get; } = new(FieldNature.Quantity);
    public FieldTileViewModel TimeTile { get; } = new(FieldNature.Time);
    public FieldTileViewModel ScaleTile { get; } = new(FieldNature.Scale);
    public FieldTileViewModel TextTile { get; } = new(FieldNature.Text);

    /// <summary>The line under a field tile: what the field is for, or — when the grammar forbids it — why not. Mirrors the
    /// order of <see cref="HabitCompositionRules.CanAddField"/>, one string per rule.</summary>
    public string FieldTileHint(FieldNature nature)
    {
        var c = Snapshot();
        if (HabitCompositionRules.CanAddField(c, nature))
            return nature switch
            {
                FieldNature.Quantity => Strings.HabitBuilder_Tile_Quantity_Hint,
                FieldNature.Time => Strings.HabitBuilder_Tile_Time_Hint,
                FieldNature.Scale => Strings.HabitBuilder_Tile_Scale_Hint,
                _ => Strings.HabitBuilder_Tile_Text_Hint,
            };
        if (c.CountsOccurrences) return Strings.HabitBuilder_Tile_Disabled_Counter;
        var hasText = c.Fields.Contains(FieldNature.Text);
        return nature switch
        {
            FieldNature.Text when hasText => Strings.HabitBuilder_Tile_Disabled_TextOnce,
            FieldNature.Text when c.Abstinence => Strings.HabitBuilder_Tile_Disabled_Abstinence,
            FieldNature.Text => Strings.HabitBuilder_Tile_Disabled_TextWithMeasures,
            FieldNature.Time => Strings.HabitBuilder_Tile_Disabled_JournalTime,   // il diario ha gia' la sua durata
            _ => Strings.HabitBuilder_Tile_Disabled_Journal,                        // quantita' o scala accanto al testo
        };
    }

    /// <summary>The derived type in the word Home uses ("counter", "journal"…), for the line under the name.</summary>
    public string DerivedTypeWord => TypeWord(DerivedType);

    internal static string TypeWord(HabitType type) => type switch
    {
        HabitType.Counter => Strings.Home_HabitType_Counter,
        HabitType.Resistance => Strings.Home_HabitType_Resistance,
        HabitType.Journal => Strings.Home_HabitType_Journal,
        HabitType.State => Strings.Home_HabitType_State,
        _ => Strings.Home_HabitType_Action,
    };

    /// <summary>"Behaves like: counter · changes by itself with what you record", as plain text (the view bolds the word
    /// from the <c>**{0}**</c> marker of the same string).</summary>
    public string DerivedTypeLine =>
        string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_DerivedType_Line.Replace("**", string.Empty, StringComparison.Ordinal), DerivedTypeWord);

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
        // A micro-form opened for THIS field would otherwise stay on screen pointing at a detached
        // VM: confirming it would write the unit onto nothing and leave a staged definition in every
        // picker for the rest of the session, with the typed name silently lost (review 2.5, F6).
        if (_formTarget == field) CancelCustomForm();
        field.PropertyChanged -= OnFieldChanged;
        Fields.Remove(field);
        Recompute();
    }

    // Picking a unit/scale on a field flips CanLaunch — re-query the Launch button (it watches CanExecuteChanged).
    private void OnFieldChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FieldBlockViewModel.UnitOfMeasureId) or nameof(FieldBlockViewModel.ScaleTemplateId)
            or nameof(FieldBlockViewModel.Name) or nameof(FieldBlockViewModel.AllowsDecimals))
        {
            Recompute();
        }
    }

    // Name/colour/icon/repeatable/gesture all show up in the preview, so they rebuild it too.
    partial void OnNameChanged(string value) => Recompute();
    partial void OnColorChanged(string value) => Recompute();
    partial void OnIconChanged(string? value) => Recompute();
    partial void OnRepeatableChanged(bool value) => Recompute();
    partial void OnJournalGestureChanged(JournalPrimaryGesture value) => Recompute();

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
        Preview = _previewFactory.Build(BuildPreviewInput());
        OnPropertyChanged(nameof(CanEnableCounter));
        OnPropertyChanged(nameof(CanEnableAbstinence));
        OnPropertyChanged(nameof(FieldsAllowed));
        OnPropertyChanged(nameof(ShowJournalGesture));
        OnPropertyChanged(nameof(DerivedTypeWord));
        OnPropertyChanged(nameof(DerivedTypeLine));
        foreach (var tile in new[] { QuantityTile, TimeTile, ScaleTile, TextTile })
        {
            tile.IsEnabled = CanAddField(tile.Nature);
            tile.Hint = FieldTileHint(tile.Nature);
        }
        RaiseLaunchState();
    }

    private void RaiseLaunchState()
    {
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(LaunchBlocked));
        OnPropertyChanged(nameof(LaunchBlockedReason));
        LaunchCommand.NotifyCanExecuteChanged();
        SaveAsTemplateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Gates both Launch and Save-as-template: a valid name, a scale on every Scale field, a
    /// time unit on every Time field and a schedule that carries the number (or the days) it needs. A Quantity field's unit stays optional — a bare number reads
    /// fine — but a duration without a unit has no unit to render its totals in. Saving is gated by the
    /// same rule because a template is a saved composition and <c>InstantiateAsync</c> materialises it
    /// with no gate of its own: a template you cannot launch would create the malformed habit later.</summary>
    public bool CanLaunch =>
        !string.IsNullOrWhiteSpace(Name)
        && FirstRejectedInput() is null
        && Fields.All(f => f.Nature != FieldNature.Scale || f.ScaleTemplateId is not null)
        && Fields.All(f => f.Nature != FieldNature.Time || f.UnitOfMeasureId is not null)
        && GoalBrick.IsComplete
        && SessionBrick.IsComplete
        && Schedule.IsComplete;

    /// <summary>Inverse of <see cref="CanLaunch"/>, for the "why is Launch disabled" hint.</summary>
    public bool LaunchBlocked => !CanLaunch;

    /// <summary>Why "Create habit" is off, the first failing rule of <see cref="CanLaunch"/> in its order; null when it is on.
    /// A rule with no line of its own (a fixed goal period, which the creator never offers) falls back to the general hint:
    /// a disabled button never stays silent.</summary>
#pragma warning disable CA1863 // CompositeFormat caching not worth it for a line that changes with the culture
    public string? LaunchBlockedReason
    {
        get
        {
            if (CanLaunch) return null;
            if (string.IsNullOrWhiteSpace(Name)) return Strings.HabitBuilder_Blocked_Name;
            if (FirstRejectedInput() is { } rejected)
                return string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Blocked_NotANumber, rejected switch
                {
                    BuilderNumberInput.Tolerance => Strings.Editor_Field_ToleranceMinutes,
                    BuilderNumberInput.Rest => Strings.Editor_Field_RestTimerSeconds,
                    _ => RollingRowWithGap,
                });
            if (Fields.FirstOrDefault(f => f.Nature == FieldNature.Scale && f.ScaleTemplateId is null) is { } scale)
                return string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Blocked_Scale, FieldLabel(scale));
            if (Fields.FirstOrDefault(f => f.Nature == FieldNature.Time && f.UnitOfMeasureId is null) is { } time)
                return string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Blocked_TimeUnit, FieldLabel(time));
            if (GoalBrick.HasGoal && GoalBrick.Target is not > 0) return Strings.HabitBuilder_Blocked_GoalTarget;
            if (GoalBrick is { HasGoal: true, PeriodType: GoalPeriodType.Rolling, RollingDays: not > 0 })
                return string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Blocked_RollingDays, RollingRowWithGap);
            if (Schedule.StreakToleranceMinutes is <= 0 or > ScheduleBrickViewModel.MaxToleranceMinutes)
                return Strings.HabitBuilder_Blocked_Tolerance;
            if (SessionBrick is { HasSession: true, RestTimerSeconds: <= 0 }) return Strings.HabitBuilder_Blocked_Rest;
            if (!Schedule.IsComplete) return Strings.HabitBuilder_Blocked_Schedule;
            return Strings.HabitBuilder_LaunchHint;
        }
    }
#pragma warning restore CA1863

    // "Count over the last … days": the Avanzate row, named in the reason with a gap where its number goes.
#pragma warning disable CA1863 // as above
    private static string RollingRowWithGap => string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_Goal_RollingRow, "…");
#pragma warning restore CA1863

    private readonly HashSet<BuilderNumberInput> _rejected = [];

    /// <summary>The view reports a number box whose text the binding rejected (and when it is accepted again).</summary>
    public void SetInputRejected(BuilderNumberInput input, bool rejected)
    {
        if (rejected ? _rejected.Add(input) : _rejected.Remove(input)) RaiseLaunchState();
    }

    // A rejected box counts only while it is part of the composition: the rest without a session, or the window without a
    // goal, is not on screen and cannot be corrected there.
    private BuilderNumberInput? FirstRejectedInput()
    {
        foreach (var input in new[] { BuilderNumberInput.Tolerance, BuilderNumberInput.RollingDays, BuilderNumberInput.Rest })
        {
            if (!_rejected.Contains(input)) continue;
            if (input == BuilderNumberInput.Rest && !SessionBrick.HasSession) continue;
            if (input == BuilderNumberInput.RollingDays && !GoalBrick.HasGoal) continue;
            return input;
        }
        return null;
    }

    /// <summary>Whether the Avanzate block is open. The view binds its toggle here so a template load can open it when the
    /// row it needs is in there (a rolling goal without its days).</summary>
    [ObservableProperty] private bool _advancedOpen;

    private static string FieldLabel(FieldBlockViewModel f) => string.IsNullOrWhiteSpace(f.Name) ? DefaultName(f.Nature) : f.Name.Trim();

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync(CancellationToken ct)
    {
        // Belt and braces (R7 precedent): AsyncRelayCommand.ExecuteAsync does not consult CanExecute,
        // so an incomplete goal (HasGoal with no positive target) has to be refused here too, not
        // only on the greyed-out button.
        if (!CanLaunch) return;
        // One habit per builder (Review Focus 3): a second Launch while the first is in flight, or after it
        // succeeded, must not create a second one. ExecuteAsync does not consult CanExecute either.
        if (_launching || CreatedHabitNodeId is not null) return;
        _launching = true;
        try
        {
            // The gesture travels inside the request (W24-R1): it used to be written after
            // CreatedHabitNodeId had already closed the dialog, so a transient failure would surface out
            // of a command that had visibly succeeded. ComposedHabitService serializes it onto the node
            // before the insert, in the same transaction as everything else.
            var habitId = await _composed.CreateComposedHabitAsync(BuildRequest(), ct).ConfigureAwait(true);
            CreatedHabitNodeId = habitId;
        }
        finally { _launching = false; }

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

    private bool _launching;

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
            Description = Description,
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
            Description = Description,
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
        var goal = GoalBrick.Build(nodeId, Name.Trim(), metrics.FirstOrDefault()?.Id);
        var segments = SessionBrick.HasSession ? SessionBrick.BuildSegments(nodeId) : null;
        // Only the staged definitions a metric actually points at: dropping the field that used one
        // leaves it in the draft (so re-adding the field can pick it again) but it must not be
        // materialised for nobody.
        var usedUnits = metrics.Where(m => m.UnitOfMeasureId is not null).Select(m => m.UnitOfMeasureId!.Value).ToHashSet();
        var usedScales = metrics.Where(m => m.ScaleTemplateId is not null).Select(m => m.ScaleTemplateId!.Value).ToHashSet();
        var newUnits = _stagedUnits.Where(u => usedUnits.Contains(u.Id)).ToList();
        var newScales = _stagedScales.Where(s => usedScales.Contains(s.Id)).ToList();
        // I3: the gesture knob used to reach only a saved template's payload — Lancia never applied
        // it, so a "quick note with photos" composition still launched with the Markdown editor.
        var uiPrefs = DerivedType == HabitType.Journal
            ? new HabitUiPrefs { JournalGesture = JournalGesture }
            : null;
        return new ComposedHabitRequest(node, config, metrics, Segments: segments, Goal: goal,
            NewUnits: newUnits.Count > 0 ? newUnits : null,
            NewScales: newScales.Count > 0 ? newScales : null,
            UiPrefs: uiPrefs);
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

    /// <summary>Projects the draft into the preview's input. Reads the same collections the launch
    /// path reads, so what the pane shows and what Lancia creates cannot say different things.</summary>
    private BuilderPreviewInput BuildPreviewInput() => new(
        Name: Name,
        Color: Color,
        Icon: Icon,
        Type: DerivedType,
        CountsOccurrences: CountsOccurrences,
        Repeatable: Repeatable,
        JournalGesture: JournalGesture,
        Fields: Fields.Select(f =>
        {
            var unit = f.UnitOfMeasureId is { } uid ? AvailableUnits.FirstOrDefault(u => u.Id == uid) : null;
            return new PreviewField(
                f.Name, f.Nature,
                unit?.Code,
                ScaleLabelsOf(f.ScaleTemplateId),
                unit is not null ? UnitLabel.For(unit) : null);
        }).ToList(),
        SeriesLabels: NamedSeriesLabels(),
        SegmentNames: SessionBrick.HasSession
            ? SessionBrick.Segments.Select(s => s.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
            : [],
        GoalTarget: GoalBrick.HasGoal ? GoalBrick.Target : null);

    // One reading per value of the scale (GIO-23: the names live in the scale's anchors, read through ScaleText).
    private List<string> ScaleLabelsOf(Guid? scaleId) =>
        scaleId is { } id && AvailableScales.FirstOrDefault(x => x.Id == id) is { } scale
            ? Enumerable.Range(scale.MinValue, scale.MaxValue - scale.MinValue + 1).Select(v => ScaleText.Short(scale, v)).ToList()
            : [];

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
        var fields = Fields.Where(f => f.Nature != FieldNature.Text).ToList();

        // Solo le definizioni staged che una metrica referenzia ancora — stessa regola di BuildRequest.
        var usedUnits = fields.Where(f => f.UnitOfMeasureId is not null).Select(f => f.UnitOfMeasureId!.Value).ToHashSet();
        var usedScales = fields.Where(f => f.ScaleTemplateId is not null).Select(f => f.ScaleTemplateId!.Value).ToHashSet();
        var units = _stagedUnits.Where(u => usedUnits.Contains(u.Id))
            .Select(u => new UnitDefPayload { Code = u.Code, Name = u.Name, Symbol = u.Symbol }).ToList();
        var scales = _stagedScales.Where(s => usedScales.Contains(s.Id))
            .Select(s => new ScaleDefPayload
            {
                Code = s.Code,
                Name = s.Name,
                MinValue = s.MinValue,
                MaxValue = s.MaxValue,
                Precision = s.Precision.ToString(),
                Anchors = s.Anchors.OrderBy(a => a.Value).Select(a => new ScaleAnchorPayload { Value = a.Value, Label = a.Label }).ToList(),
            }).ToList();

        // Il payload config si legge dalla stessa HabitConfig che Lancia costruirebbe: così le due
        // strade non possono divergere (è esattamente ciò che il guard di parità T10 verifica).
        var launched = BuildConfig(Guid.Empty);
        var seriesLabels = NamedSeriesLabels();
        var segments = SessionBrick.HasSession
            ? SessionBrick.BuildSegments(Guid.Empty)
                .Select(s => new SegmentPayload { Name = s.Name, IsRest = s.IsRest, SortOrder = s.SortOrder })
                .ToList()
            : null;
        var firstUnitCode = fields.FirstOrDefault() is { UnitOfMeasureId: { } gid }
            && unitCodeById.TryGetValue(gid, out var guc) ? guc : null;

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
            // Sempre presente e con i default scritti espliciti: il lettore ha fallback propri
            // (StreakRecovery.None) che non coincidono con quelli del builder, e i template seed li
            // dichiarano tutti — il guard di parità (T10) confronta le due forme.
            Config = new ConfigPayload
            {
                ScheduleType = launched.ScheduleType.ToString(),
                ScheduleDaysJson = launched.ScheduleDaysJson,
                ScheduleWeeklyTarget = launched.ScheduleWeeklyTarget,
                ScheduleEveryXDays = launched.ScheduleEveryXDays,
                StreakMode = launched.StreakMode.ToString(),
                StreakRecovery = launched.StreakRecovery.ToString(),
                StreakToleranceMinutes = launched.StreakToleranceMinutes,
                RestTimerSeconds = launched.RestTimerSeconds,
                TimerAutoPrompt = launched.TimerAutoPrompt ? true : null,
                HasIntegratedTimer = launched.HasIntegratedTimer ? true : null,
                TimerMode = launched.TimerMode?.ToString(),
                Repeatable = launched.Repeatable ? true : null,
                SeriesLabels = seriesLabels.Length > 0 && launched.SeriesLabelsJson is not null
                    ? seriesLabels.ToList() : null,
            },
            UiPrefs = DerivedType == HabitType.Journal
                ? new UiPrefsPayload { JournalGesture = JournalGesture.ToString() }
                : null,
            Metrics = fields
                .Select(f => new MetricPayload
                {
                    Name = f.Name,
                    Type = f.MetricType.ToString(),
                    // Codici (non GUID) perche' InstantiateAsync risolve unita' e scala per Code.
                    Unit = f.UnitOfMeasureId is { } uid && unitCodeById.TryGetValue(uid, out var uc) ? uc : null,
                    ScaleCode = f.ScaleTemplateId is { } sid && scaleCodeById.TryGetValue(sid, out var sc) ? sc : null,
                })
                .ToList(),
            Segments = segments is { Count: > 0 } ? segments : null,
            // Il target del goal appartiene alla prima metrica; l'unità è ridondante per il lettore
            // (che la ignora) ma i seed la portano e la parità semantica la confronta.
            Goal = GoalBrick.Build(Guid.Empty, Name.Trim(), null) is { } g
                ? new GoalPayload
                {
                    // Il nome solo se e' proprio: assente = quello dell'abitudine.
                    Name = string.IsNullOrWhiteSpace(GoalBrick.Name) ? null : g.Name,
                    Target = g.TargetValue,
                    Unit = firstUnitCode,
                    PeriodType = g.PeriodType.ToString(),
                    RollingDays = g.RollingDays,
                }
                : null,
            Units = units.Count > 0 ? units : null,
            Scales = scales.Count > 0 ? scales : null,
        };
        return JsonSerializer.Serialize(payload, s_templateJsonOptions);
    }

    /// <summary>Test seam, twin of <see cref="BuildRequestForTest"/>: the parity guard (T10) compares
    /// this payload with the seed ones without going through the DB-backed template service.</summary>
    internal string BuildTemplatePayloadJsonForTest() => BuildTemplatePayloadJson();

    // Mirror della forma consumata da HabitTemplateService.InstantiateAsync (snake_case via options).
    // I nomi e l'annidamento devono restare identici a quelli del servizio: una chiave scritta qui e
    // non letta la' e' silenziosamente persa al re-instanziamento.
    private sealed class TemplatePayload
    {
        public NodePayload? Node { get; set; }
        public ConfigPayload? Config { get; set; }
        public UiPrefsPayload? UiPrefs { get; set; }
        public List<MetricPayload>? Metrics { get; set; }
        public List<SegmentPayload>? Segments { get; set; }
        public GoalPayload? Goal { get; set; }
        public List<UnitDefPayload>? Units { get; set; }
        public List<ScaleDefPayload>? Scales { get; set; }

        /// <summary>Solo lettura: i modelli del creatore prima della 2.4 scrivevano "repeatable" alla radice
        /// (stessa compatibilita' di <c>HabitTemplateService</c>, I5). Il writer non lo produce piu'.</summary>
        [JsonPropertyName("repeatable")]
        public bool? RepeatableLegacy { get; set; }
    }

    private sealed class ConfigPayload
    {
        public string? ScheduleType { get; set; }
        public string? ScheduleDaysJson { get; set; }
        public int? ScheduleWeeklyTarget { get; set; }
        public int? ScheduleEveryXDays { get; set; }
        public string? StreakMode { get; set; }
        public string? StreakRecovery { get; set; }
        public bool? HasIntegratedTimer { get; set; }
        public string? TimerMode { get; set; }
        // null-when-false (WhenWritingNull) cosi' i template salvati prima di U8 restano byte-identici.
        public bool? Repeatable { get; set; }
        public List<string>? SeriesLabels { get; set; }
        public int? StreakToleranceMinutes { get; set; }
        public int? RestTimerSeconds { get; set; }
        public bool? TimerAutoPrompt { get; set; }
    }

    private sealed class SegmentPayload
    {
        public string? Name { get; set; }
        public bool IsRest { get; set; }
        public int SortOrder { get; set; }
    }

    private sealed class GoalPayload
    {
        public string? Name { get; set; }
        public decimal Target { get; set; }
        public string? Unit { get; set; }
        public string? PeriodType { get; set; }
        public int? RollingDays { get; set; }
    }

    private sealed class UnitDefPayload
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Symbol { get; set; }
    }

    private sealed class ScaleDefPayload
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public string? Precision { get; set; }
        public List<ScaleAnchorPayload>? Anchors { get; set; }
        /// <summary>Read only: templates saved before GIO-23 carry the names in a row.</summary>
        public List<string>? Labels { get; set; }
    }

    private sealed class ScaleAnchorPayload
    {
        public int Value { get; set; }
        public string? Label { get; set; }
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
