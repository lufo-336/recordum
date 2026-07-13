using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.HabitScreen.Calendar;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;
using Recordum.WPF.ViewModels.HabitScreen.Today;
using Recordum.WPF.Services;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>
/// The habit screen: the workspace shell shown for one selected <see cref="Node"/>. It owns the set of
/// toggleable side-by-side columns (Panoramica, Diario, Cronologia, Dettaglio, Note, Configura),
/// tracks which are open (persisted per user), and gates each by applicability to the current subject
/// type (e.g. Diario is journal-only; Panoramica is groups-only) and by focus mode.
///
/// <para>It also composes the child column ViewModels (calendar, quick-add, records list, detail,
/// notes, editor) and keeps them coherent: when a mutation happens — a completion (via the
/// <see cref="Recordum.Plugins.Contracts.Hosting.IEventBus"/>) or an abstinence Start/Lapse/Delete
/// (which writes no completion, so it signals through a <c>Changed</c> event) — the relevant columns
/// are reloaded. This class is large by design (it is the coordinator); the real work lives in the
/// child ViewModels it owns.</para>
/// </summary>
public sealed partial class HabitScreenViewModel : ObservableObject, IDisposable
{
    private readonly IHabitNodeService _nodes;
    private readonly ICompletionService _completions;
    private readonly IAbstinenceService _abstinence;
    private readonly IDiaryService _diary;
    private readonly IActiveTimerService _timers;
    private readonly INoteService _noteService;
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IHabitConfigService _configs;
    private readonly IClock _clock;
    private readonly IAppSettingService _appSettings;
    private readonly IHabitUiPrefsService _uiPrefs;
    private readonly AbstinencePeriodListViewModel _periodList;
    private readonly List<IDisposable> _subscriptions = [];

    // ---- column workspace state (nav-model) ----

    internal const string OpenColumnsSettingKey = "ui.open_columns";
    internal const string LastClosedColumnSettingKey = "ui.last_closed_column";
    private readonly List<HabitColumn> _openOrder = [];
    private bool _restoringColumns;
    // Null until the user actually closes a column (or a persisted one is restored): a fresh subject
    // then falls back to its type default (journal → Diario) instead of a stale Cronologia.
    private HabitColumn? _lastClosedColumn;

    /// <summary>Raised when the user closes the last open view column (host deselects, spec §B.4).</summary>
    public event EventHandler? AllColumnsClosed;

    /// <summary>Test seam: raise <see cref="AllColumnsClosed"/> from tests (the screen is concrete and
    /// driving its columns to all-closed is awkward to set up).</summary>
    internal void RaiseAllColumnsClosedForTest() => AllColumnsClosed?.Invoke(this, EventArgs.Empty);

    /// <summary>Test seam: raise <see cref="ExportRequested"/> from tests.</summary>
    internal void RaiseExportRequestedForTest() => ExportRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The Configura column's editor; owned here so the column workspace and the host
    /// window share one instance (MainWindowViewModel.Editor is a passthrough).</summary>
    public HabitEditorViewModel Editor { get; }

    /// <summary>The Panoramica column's group rollup (spec §A.2).</summary>
    public PanoramicaViewModel Panoramica { get; }

    [ObservableProperty] private bool _isCronologiaOpen;
    [ObservableProperty] private bool _isDettaglioOpen;
    [ObservableProperty] private bool _isNoteOpen;
    [ObservableProperty] private bool _isConfiguraOpen;
    [ObservableProperty] private bool _isPanoramicaOpen;
    [ObservableProperty] private bool _isDiarioOpen;

    partial void OnIsDiarioOpenChanged(bool value) { OnColumnFlag(HabitColumn.Diario, value); NotifyShowFlags(); }
    partial void OnIsCronologiaOpenChanged(bool value) { OnColumnFlag(HabitColumn.Cronologia, value); NotifyShowFlags(); }
    partial void OnIsDettaglioOpenChanged(bool value) { OnColumnFlag(HabitColumn.Dettaglio, value); NotifyShowFlags(); }
    partial void OnIsNoteOpenChanged(bool value) { OnColumnFlag(HabitColumn.Note, value); NotifyShowFlags(); }
    partial void OnIsPanoramicaOpenChanged(bool value) { OnColumnFlag(HabitColumn.Panoramica, value); NotifyShowFlags(); }
    partial void OnIsConfiguraOpenChanged(bool value)
    {
        OnColumnFlag(HabitColumn.Configura, value);
        if (value && !_restoringColumns)
        {
            ConfigureRequested?.Invoke(this, EventArgs.Empty);
            _ = LoadEditorAsync();
        }
        NotifyShowFlags();
    }

    /// <summary>Loads the Configura column's editor for the current subject. This wiring lived in
    /// MainWindowViewModel until the workspace shell moved the column here — without it the column
    /// renders a never-loaded editor (empty name, no sections).</summary>
    private async Task LoadEditorAsync()
    {
        if (Node is null) return;
        try { await Editor.LoadAsync(Node.Id).ConfigureAwait(true); }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"Editor load failed: {ex}"); }
    }

    /// <summary>Focus mode (spec §3.6): when set, only this column fills the whole tab and the chip bar
    /// is hidden; <c>null</c> = normal split layout. Session-only (not persisted).</summary>
    [ObservableProperty] private HabitColumn? _focusedColumn;

    /// <summary>True while a single column is expanded to fill the tab.</summary>
    public bool IsFocusActive => FocusedColumn is not null;

    /// <summary>The chip toggle bar is hidden while a column is focused (a return affordance shows instead).</summary>
    public bool ShowChipBar => FocusedColumn is null;

    partial void OnFocusedColumnChanged(HabitColumn? value)
    {
        OnPropertyChanged(nameof(IsFocusActive));
        OnPropertyChanged(nameof(ShowChipBar));
        NotifyShowFlags();
    }

    // A column is visible only when focus allows it: no focus → all open columns; focus → just that one.
    private bool FocusAllows(HabitColumn column) => FocusedColumn is null || FocusedColumn == column;

    /// <summary>⤢ header toggle: expand this column to fill the tab, or restore the split layout.</summary>
    [RelayCommand]
    private void ToggleFocus(HabitColumn column) => FocusedColumn = FocusedColumn == column ? null : column;

    /// <summary>Esc / ↩: leave focus mode (no-op when not focused).</summary>
    [RelayCommand]
    private void ExitFocus() => FocusedColumn = null;

    /// <summary>Column applicability per subject type (spec §A.1 + §3.4): groups get
    /// Panoramica/Note/Configura; the ✎ Diario pane is journal-habits only; Panoramica is groups-only.</summary>
    internal static bool IsColumnApplicable(HabitColumn column, NodeType nodeType, HabitType? habitType)
    {
        if (nodeType == NodeType.Group)
            return column is HabitColumn.Panoramica or HabitColumn.Note or HabitColumn.Configura;
        if (column == HabitColumn.Panoramica) return false;
        if (column == HabitColumn.Diario) return habitType == HabitType.Journal;
        return true; // Cronologia, Dettaglio, Note, Configura
    }

    private bool IsColumnApplicable(HabitColumn column) => Node is not null
        && IsColumnApplicable(column, Node.NodeType, Node.HabitType);

    // Column visibility = open flag AND applicability to the current subject, so the open
    // set survives habit↔group↔journal switches intact (spec §A.1 + §3.4).
    public bool IsGroupNode => Node?.NodeType == NodeType.Group;

    /// <summary>True only for journal habits: gates the dedicated ✎ Diario chip and editor routing.</summary>
    public bool IsJournal => Node?.HabitType == HabitType.Journal;
    public bool ShowPanoramica => IsPanoramicaOpen && IsColumnApplicable(HabitColumn.Panoramica) && FocusAllows(HabitColumn.Panoramica);
    public bool ShowDiario => IsDiarioOpen && IsColumnApplicable(HabitColumn.Diario) && FocusAllows(HabitColumn.Diario);
    public bool ShowCronologia => IsCronologiaOpen && IsColumnApplicable(HabitColumn.Cronologia) && FocusAllows(HabitColumn.Cronologia);
    public bool ShowDettaglio => IsDettaglioOpen && IsColumnApplicable(HabitColumn.Dettaglio) && FocusAllows(HabitColumn.Dettaglio);
    public bool ShowNote => IsNoteOpen && Node is not null && FocusAllows(HabitColumn.Note);
    public bool ShowConfigura => IsConfiguraOpen && Node is not null && FocusAllows(HabitColumn.Configura);

    /// <summary>Total minimum width the open columns demand (each at <see cref="MinColumnWidth"/>).
    /// Bound to the columns Grid's MinWidth: the Grid fills the tab viewport when there is room
    /// (star-split, no leftover gap), but never shrinks below this — past it the host ScrollViewer
    /// scrolls horizontally instead of squeezing the columns (hybrid "fill-then-scroll").</summary>
    public double OpenColumnsMinWidth
    {
        get
        {
            var open = (ShowPanoramica ? 1 : 0) + (ShowDiario ? 1 : 0) + (ShowCronologia ? 1 : 0)
                     + (ShowDettaglio ? 1 : 0) + (ShowNote ? 1 : 0) + (ShowConfigura ? 1 : 0);
            return open * MinColumnWidth;
        }
    }

    private void NotifyShowFlags()
    {
        OnPropertyChanged(nameof(IsGroupNode));
        OnPropertyChanged(nameof(IsJournal));
        OnPropertyChanged(nameof(ShowTodayStrip));
        OnPropertyChanged(nameof(ShowPanoramica));
        OnPropertyChanged(nameof(ShowDiario));
        OnPropertyChanged(nameof(ShowCronologia));
        OnPropertyChanged(nameof(ShowDettaglio));
        OnPropertyChanged(nameof(ShowNote));
        OnPropertyChanged(nameof(ShowConfigura));
        OnPropertyChanged(nameof(OpenColumnsMinWidth));
    }

    partial void OnNodeChanged(HabitNode? value)
    {
        NotifyShowFlags();
        NotifyQuickAddRouting();
    }

    private void OnColumnFlag(HabitColumn column, bool open)
    {
        if (open) { if (!_openOrder.Contains(column)) _openOrder.Add(column); }
        else { _openOrder.Remove(column); if (FocusedColumn == column) FocusedColumn = null; }
        if (_restoringColumns) return;
        _ = PersistOpenColumnsAsync();      // legacy global (kept warm for brand-new habits)
        _ = PersistSubjectColumnsAsync();   // per-habit prefs (the authoritative set from now on)
        if (!open)
        {
            _lastClosedColumn = column;
            _ = _appSettings.SetAsync(LastClosedColumnSettingKey, column.ToString()).AsTask();
            if (Node is not null && !CanonicalOpen().Any(IsColumnApplicable))
                AllColumnsClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    private Task PersistOpenColumnsAsync()
        => _appSettings.SetAsync(OpenColumnsSettingKey,
            string.Join(",", CanonicalOpen().Select(c => c.ToString()))).AsTask();

    /// <summary>Persists the current subject's open-column set into its own <see cref="HabitUiPrefs.OpenColumns"/>.
    /// The whole list is replaced (never mutated) because <c>OpenColumns</c> uses reference equality.</summary>
    private async Task PersistSubjectColumnsAsync()
    {
        var node = Node;
        if (node is null) return;
        var prefs = await _uiPrefs.GetAsync(node.Id).ConfigureAwait(true);
        var cols = CanonicalOpen().Select(c => c.ToString()).ToList();
        await _uiPrefs.SetAsync(node.Id, prefs with { OpenColumns = cols }).ConfigureAwait(true);
    }

    /// <summary>Restores the open-column set for a single subject on load. Precedence:
    /// (1) the subject's own persisted <see cref="HabitUiPrefs.OpenColumns"/> — a hard replace of the set;
    /// (2) the live carry-over set from the previously shown subject when a column still applies
    ///     (preserves the old un-customized shared behaviour);
    /// (3) the legacy global <c>ui.open_columns</c> for brand-new habits with nothing carried over;
    /// (4) the per-type default (group → Panoramica §A.1, journal → Diario §3.4, else Cronologia §B.5),
    ///     preferring the last closed column when it applies.
    /// Every application runs under <see cref="_restoringColumns"/> so it never persists or fires
    /// AllColumnsClosed — only explicit user toggles do.</summary>
    internal async Task RestoreColumnsForSubjectAsync(HabitNode node, CancellationToken ct)
    {
        // (1) Per-habit prefs win: replace the open set with the subject's own applicable columns.
        var prefs = await _uiPrefs.GetAsync(node.Id, ct).ConfigureAwait(true);
        if (prefs.OpenColumns is { Count: > 0 } saved)
        {
            var cols = ParseApplicableColumns(saved, node);
            if (cols.Count > 0) { ApplyColumnSet(cols); return; }
            // saved set has nothing applicable here → fall through to the additive defaults below.
        }

        // (2) A live-carried column already applies → keep the current set as-is (additive shared behaviour).
        if (CanonicalOpen().Any(c => IsColumnApplicable(c, node.NodeType, node.HabitType)))
            return;

        // (3) Legacy global fallback (applicable subset), opened additively.
        var raw = await _appSettings.GetAsync(OpenColumnsSettingKey, ct).ConfigureAwait(true);
        var legacy = ParseApplicableColumns(
            (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            node);
        if (legacy.Count > 0) { OpenColumnsAdditively(legacy); return; }

        // (4) Per-type default (prefer the last closed column when applicable).
        var fallback = node.NodeType == NodeType.Group ? HabitColumn.Panoramica
            : node.HabitType == HabitType.Journal ? HabitColumn.Diario
            : HabitColumn.Cronologia;
        var target = _lastClosedColumn is { } lc && IsColumnApplicable(lc, node.NodeType, node.HabitType) ? lc : fallback;
        OpenColumnsAdditively([target]);
    }

    private static List<HabitColumn> ParseApplicableColumns(IEnumerable<string> tokens, HabitNode node)
        => tokens
            .Select(t => Enum.TryParse<HabitColumn>(t, out var c) ? c : (HabitColumn?)null)
            .Where(c => c is not null && IsColumnApplicable(c.Value, node.NodeType, node.HabitType))
            .Select(c => c!.Value)
            .ToList();

    /// <summary>Hard-replaces the open flags with <paramref name="columns"/> (closes everything else),
    /// preserving the given order in <see cref="_openOrder"/>. Runs under <see cref="_restoringColumns"/>.</summary>
    private void ApplyColumnSet(IReadOnlyList<HabitColumn> columns)
    {
        var target = columns.ToHashSet();
        _restoringColumns = true;
        try
        {
            foreach (var col in Enum.GetValues<HabitColumn>())
                if (!target.Contains(col)) SetColumnFlag(col, false);
            foreach (var col in columns) SetColumnFlag(col, true);
        }
        finally { _restoringColumns = false; }
    }

    /// <summary>Opens <paramref name="columns"/> on top of the current flags without closing anything and
    /// without persisting (guarded by <see cref="_restoringColumns"/>).</summary>
    private void OpenColumnsAdditively(IReadOnlyList<HabitColumn> columns)
    {
        _restoringColumns = true;
        try { foreach (var col in columns) SetColumnFlag(col, true); }
        finally { _restoringColumns = false; }
    }

    private IEnumerable<HabitColumn> CanonicalOpen()
    {
        if (IsDiarioOpen) yield return HabitColumn.Diario;
        if (IsCronologiaOpen) yield return HabitColumn.Cronologia;
        if (IsDettaglioOpen) yield return HabitColumn.Dettaglio;
        if (IsNoteOpen) yield return HabitColumn.Note;
        if (IsConfiguraOpen) yield return HabitColumn.Configura;
        if (IsPanoramicaOpen) yield return HabitColumn.Panoramica;
    }

    /// <summary>Restores the persisted open-column set (call once at startup, before the first load).</summary>
    public async Task RestoreOpenColumnsAsync(CancellationToken ct = default)
    {
        // Widths restore must run even when no open-column set was ever persisted.
        await RestoreColumnWidthsAsync(ct).ConfigureAwait(true);

        var lastClosed = await _appSettings.GetAsync(LastClosedColumnSettingKey, ct).ConfigureAwait(true);
        if (Enum.TryParse<HabitColumn>(lastClosed, out var lastClosedColumn))
            _lastClosedColumn = lastClosedColumn;

        var raw = await _appSettings.GetAsync(OpenColumnsSettingKey, ct).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(raw)) return;
        _restoringColumns = true;
        try
        {
            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (Enum.TryParse<HabitColumn>(token, out var c))
                    SetColumnFlag(c, true);
        }
        finally { _restoringColumns = false; }
    }

    private void SetColumnFlag(HabitColumn column, bool open)
    {
        switch (column)
        {
            case HabitColumn.Diario: IsDiarioOpen = open; break;
            case HabitColumn.Cronologia: IsCronologiaOpen = open; break;
            case HabitColumn.Dettaglio: IsDettaglioOpen = open; break;
            case HabitColumn.Note: IsNoteOpen = open; break;
            case HabitColumn.Configura: IsConfiguraOpen = open; break;
            case HabitColumn.Panoramica: IsPanoramicaOpen = open; break;
        }
    }

    [RelayCommand]
    private void ToggleColumn(HabitColumn column) => SetColumnFlag(column, !CanonicalOpen().Contains(column));

    /// <summary>Esc: closes the most recently opened column.</summary>
    public void CloseLastColumn()
    {
        if (_openOrder.Count == 0) return;
        SetColumnFlag(_openOrder[^1], false);
    }

    // ---- column widths (resizable via drag-thumb, spec 2026-06-11-column-resize) ----

    internal const string ColumnWidthsSettingKey = "ui.column_widths";
    internal const double MinColumnWidth = 320;
    internal const double MaxColumnWidth = 1000;

    private static double DefaultWidth(HabitColumn column) => column switch
    {
        HabitColumn.Diario => 560,
        HabitColumn.Cronologia => 460,
        HabitColumn.Dettaglio => 520,
        HabitColumn.Note => 460,
        HabitColumn.Panoramica => 460,
        _ => 480,
    };

    private static double ClampWidth(double value) => Math.Clamp(value, MinColumnWidth, MaxColumnWidth);

    // Manual properties (not [ObservableProperty]) so the setter can clamp.
    private double _cronologiaWidth = DefaultWidth(HabitColumn.Cronologia);
    public double CronologiaWidth { get => _cronologiaWidth; set => SetProperty(ref _cronologiaWidth, ClampWidth(value)); }

    private double _dettaglioWidth = DefaultWidth(HabitColumn.Dettaglio);
    public double DettaglioWidth { get => _dettaglioWidth; set => SetProperty(ref _dettaglioWidth, ClampWidth(value)); }

    private double _noteWidth = DefaultWidth(HabitColumn.Note);
    public double NoteWidth { get => _noteWidth; set => SetProperty(ref _noteWidth, ClampWidth(value)); }

    private double _configuraWidth = DefaultWidth(HabitColumn.Configura);
    public double ConfiguraWidth { get => _configuraWidth; set => SetProperty(ref _configuraWidth, ClampWidth(value)); }

    private double _panoramicaWidth = DefaultWidth(HabitColumn.Panoramica);
    public double PanoramicaWidth { get => _panoramicaWidth; set => SetProperty(ref _panoramicaWidth, ClampWidth(value)); }

    private double _diarioWidth = DefaultWidth(HabitColumn.Diario);
    public double DiarioWidth { get => _diarioWidth; set => SetProperty(ref _diarioWidth, ClampWidth(value)); }

    public double GetColumnWidth(HabitColumn column) => column switch
    {
        HabitColumn.Diario => DiarioWidth,
        HabitColumn.Cronologia => CronologiaWidth,
        HabitColumn.Dettaglio => DettaglioWidth,
        HabitColumn.Note => NoteWidth,
        HabitColumn.Panoramica => PanoramicaWidth,
        _ => ConfiguraWidth,
    };

    public void SetColumnWidth(HabitColumn column, double width)
    {
        switch (column)
        {
            case HabitColumn.Diario: DiarioWidth = width; break;
            case HabitColumn.Cronologia: CronologiaWidth = width; break;
            case HabitColumn.Dettaglio: DettaglioWidth = width; break;
            case HabitColumn.Note: NoteWidth = width; break;
            case HabitColumn.Configura: ConfiguraWidth = width; break;
            case HabitColumn.Panoramica: PanoramicaWidth = width; break;
        }
    }

    public void ResetColumnWidth(HabitColumn column) => SetColumnWidth(column, DefaultWidth(column));

    /// <summary>Called by the view at the end of a resize drag (not per-pixel).</summary>
    public Task PersistColumnWidthsAsync()
        => _appSettings.SetAsync(ColumnWidthsSettingKey,
            string.Join(",", Enum.GetValues<HabitColumn>()
                .Select(c => $"{c}={(int)Math.Round(GetColumnWidth(c))}"))).AsTask();

    private async Task RestoreColumnWidthsAsync(CancellationToken ct)
    {
        var raw = await _appSettings.GetAsync(ColumnWidthsSettingKey, ct).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(raw)) return;
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split('=', 2);
            if (parts.Length != 2) continue;
            if (!Enum.TryParse<HabitColumn>(parts[0], out var column)) continue;
            if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var width)) continue;
            if (width is < MinColumnWidth or > MaxColumnWidth) continue;   // out of range → keep default
            SetColumnWidth(column, width);
        }
    }

    [ObservableProperty] private HabitNode? _node;

    public bool HasNode => Node is not null;
    public string Name => Node?.Name ?? string.Empty;

    public CompletionListViewModel CompletionList { get; }

    /// <summary>Completions recorded on the calendar-selected day, with readable field values (B12).
    /// Shown in the Abitudine tab; reloaded on day change and on completion events.</summary>
    public HabitRecordsViewModel Records { get; }
    public TimerControlViewModel TimerControl { get; }
    public NotesViewModel Notes { get; }
    public DetailViewModel Detail { get; }

    /// <summary>The always-visible per-type "Oggi" strip above the columns (Onda 2.2). Loaded per
    /// subject; only shown for a habit (not a group).</summary>
    public TodayStripViewModel TodayStrip { get; }

    /// <summary>The strip is present for any habit subject (even with every column closed), hidden for
    /// groups and when nothing is loaded — so a habit is never a blank landing.</summary>
    public bool ShowTodayStrip => HasNode && !IsGroupNode;

    public CalendarNavigatorViewModel Calendar { get; }
    public DayNoteViewModel DayNote { get; }
    public IWebView2Availability Availability { get; }

    [ObservableProperty] private QuickAddViewModelBase? _quickAdd;

    partial void OnQuickAddChanged(QuickAddViewModelBase? value) => NotifyQuickAddRouting();

    // The journal markdown editor (WebView2) must be hosted by exactly one ContentControl, else two
    // editors share one VM and double-instantiate WebView2 (spec §3.5 caution): route the quick-add to
    // Diario for journals and to Cronologia otherwise, so only one host ever has non-null content.
    public QuickAddViewModelBase? QuickAddForDiario => IsJournal ? QuickAdd : null;
    public QuickAddViewModelBase? QuickAddForCronologia => IsJournal ? null : QuickAdd;

    private void NotifyQuickAddRouting()
    {
        OnPropertyChanged(nameof(QuickAddForDiario));
        OnPropertyChanged(nameof(QuickAddForCronologia));
    }

    public event EventHandler? ConfigureRequested;
    public event EventHandler? ExportRequested;

    public HabitScreenViewModel(
        IHabitNodeService nodes,
        ICompletionService completions,
        IActiveTimerService timers,
        IMetricDefinitionService metrics,
        IReferenceDataQueryService refData,
        IHabitConfigService configs,
        INoteService noteService,
        IGoalService goals,
        IIntensityRampResolver ramp,
        IStreakAggregator streaks,
        ITrendQueryService trend,
        IHourlyDistributionQueryService hourly,
        IAbstinenceService abstinence,
        IClock clock,
        IEventBus events,
        IWebView2Availability availability,
        IAppSettingService appSettings,
        HabitEditorViewModel editor,
        IHabitTreeQueryService tree,
        IDiaryService diary,
        IConfirmationDialog confirmationDialog,
        Recordum.Core.IAttachmentService attachments,
        IOpenFileDialog openFileDialog,
        IHabitUiPrefsService uiPrefs)
    {
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(uiPrefs);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(diary);
        ArgumentNullException.ThrowIfNull(confirmationDialog);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(openFileDialog);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(timers);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(refData);
        ArgumentNullException.ThrowIfNull(configs);
        ArgumentNullException.ThrowIfNull(noteService);
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(ramp);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(availability);
        _nodes = nodes;
        _completions = completions;
        _abstinence = abstinence;
        _diary = diary;
        _timers = timers;
        _noteService = noteService;
        _metrics = metrics;
        _refData = refData;
        _configs = configs;
        _clock = clock;
        _appSettings = appSettings;
        _uiPrefs = uiPrefs;
        Editor = editor;
        Availability = availability;
        CompletionList = new CompletionListViewModel(completions, metrics, refData, diary);
        _periodList = new AbstinencePeriodListViewModel(abstinence, confirmationDialog, clock);
        _periodList.Changed += OnAbstinenceChanged;
        Records = new HabitRecordsViewModel(new CompletionListViewModel(completions, metrics, refData, diary), _periodList, clock);
        TimerControl = new TimerControlViewModel(timers, configs, clock);
        Notes = new NotesViewModel(noteService, events, clock, availability, attachments, openFileDialog, confirmationDialog);
        Detail = new DetailViewModel(goals, streaks, trend, hourly, completions, metrics, ramp, configs, abstinence, clock, events);
        Panoramica = new PanoramicaViewModel(tree, goals, completions, clock);
        TodayStrip = new TodayStripViewModel(abstinence, clock);
        // The strip's own Inizio/Ricaduta mutate abstinence state without a bus event, same as the
        // quick-add: reuse OnAbstinenceChanged to reload Detail/calendar/records/quick-add.
        TodayStrip.Changed += OnAbstinenceChanged;
        Calendar = new CalendarNavigatorViewModel(clock) { MarkerProvider = LoadMarkersAsync };
        Calendar.SelectedDateChanged += OnCalendarDateSelected;
        DayNote = new DayNoteViewModel(noteService, clock);
        _subscriptions.Add(events.SubscribeOnUi(WellKnownEvents.CompletionRecorded, OnCompletionEvent));
        _subscriptions.Add(events.SubscribeOnUi(WellKnownEvents.CompletionUpdated, OnCompletionEvent));
        _subscriptions.Add(events.SubscribeOnUi(WellKnownEvents.CompletionDeleted, OnCompletionEvent));
        _subscriptions.Add(events.SubscribeOnUi(WellKnownEvents.NoteCreated, OnNoteEvent));
        _subscriptions.Add(events.SubscribeOnUi(WellKnownEvents.NoteUpdated, OnNoteEvent));
        _subscriptions.Add(events.SubscribeOnUi(WellKnownEvents.NoteDeleted, OnNoteEvent));
        _subscriptions.Add(events.SubscribeOnUi(WellKnownEvents.HabitUpdated, OnHabitUpdatedEvent));
    }

    public void Dispose()
    {
        Calendar.SelectedDateChanged -= OnCalendarDateSelected;
        _periodList.Changed -= OnAbstinenceChanged;
        TodayStrip.Changed -= OnAbstinenceChanged;
        if (QuickAdd is AbstinenceQuickAddViewModel ab) ab.Changed -= OnAbstinenceChanged;
        Notes.Dispose();
        Detail.Dispose();
        Records.Dispose();
        TodayStrip.Dispose();
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
    }

    // Bumped on every LoadAsync. Selecting/deselecting habits fires overlapping async loads
    // (OnHomePropertyChanged is async void with no cancellation); a stale load that resumes after
    // a newer one must not clobber state or dereference a Node the newer load already nulled.
    private int _loadGeneration;

    public async Task LoadAsync(Guid? habitNodeId, CancellationToken ct = default)
    {
        var generation = ++_loadGeneration;

        if (habitNodeId is null)
        {
            Node = null;
            QuickAdd = null;
            OnPropertyChanged(nameof(HasNode));
            OnPropertyChanged(nameof(Name));
            return;
        }

        var node = await _nodes.GetAsync(habitNodeId.Value, ct).ConfigureAwait(true);
        if (generation != _loadGeneration) return; // superseded by a newer load/deselect
        Node = node;
        OnPropertyChanged(nameof(HasNode));
        OnPropertyChanged(nameof(Name));
        if (node is null) { QuickAdd = null; return; }

        // Restore this subject's own open-column set (per-habit prefs → live carry-over → legacy global →
        // per-type default). Must run BEFORE the editorTask line below so a restored-open Configura loads.
        await RestoreColumnsForSubjectAsync(node, ct).ConfigureAwait(true);
        if (generation != _loadGeneration) return;

        if (node.NodeType == NodeType.Group)
            await Panoramica.LoadAsync(node, ct).ConfigureAwait(true);

        // Journal Cronologia shows the paired diary note body + deletes the pair via IDiaryService.
        CompletionList.IsJournal = node.HabitType == HabitType.Journal;

        // Config is needed for both QuickAdd and TimerControl; fetch once, share.
        var configTask = _configs.GetAsync(node.Id, ct).AsTask();
        var listTask = CompletionList.LoadAsync(node.Id, ct);
        var quickAddTask = BuildQuickAddAsync(node, ct);
        var notesTask = Notes.LoadAsync(node.Id, ct);
        var detailTask = Detail.LoadAsync(node, ct);
        var todayTask = TodayStrip.LoadAsync(node, ct);
        // Configura already open (restored or carried over from the previous subject): load its editor too.
        var editorTask = IsConfiguraOpen ? Editor.LoadAsync(node.Id, ct) : Task.CompletedTask;
        await Task.WhenAll(configTask, listTask, quickAddTask, notesTask, detailTask, todayTask, editorTask).ConfigureAwait(true);
        if (generation != _loadGeneration) return; // a deselect/newer selection won the race

        Calendar.CompletedColorHex = node.Color;
        Calendar.NoteColorHex = configTask.Result?.NoteMarkerColor ?? "#e0b341";
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow.UtcDateTime, _clock.LocalTimeZone);
        await Calendar.LoadAsync(todayLocal.Year, todayLocal.Month, DateOnly.FromDateTime(todayLocal), ct).ConfigureAwait(true);
        if (generation != _loadGeneration) return;

        // Mirror the calendar's pre-selected day into QuickAdd so both controls agree before the first click.
        if (QuickAdd is not null && Calendar.SelectedDate is { } preSelected)
            QuickAdd.CompletionDate = preSelected.ToDateTime(TimeOnly.MinValue);

        DayNote.SetContext(node.Id, node.HabitType == HabitType.Journal);
        Records.SetContext(node.Id, node.HabitType == HabitType.Journal, isResistance: node.HabitType == HabitType.Resistance);
        if (Calendar.SelectedDate is { } sel)
        {
            Notes.SetCalendarDay(sel);
            await DayNote.LoadDayAsync(sel, ct).ConfigureAwait(true);
            await PushDaySummaryAsync(sel, ct).ConfigureAwait(true);
            await Records.ShowDayAsync(sel, ct).ConfigureAwait(true);
        }

        await TimerControl.SetContextAsync(node, configTask.Result, ct).ConfigureAwait(true);
    }

    private async Task BuildQuickAddAsync(HabitNode node, CancellationToken ct)
    {
        QuickAddViewModelBase? vm = node.HabitType switch
        {
            HabitType.Action => new ActionQuickAddViewModel(_completions, _clock),
            HabitType.Counter => await BuildCounterQuickAddAsync(ct).ConfigureAwait(true),
            HabitType.Resistance => new AbstinenceQuickAddViewModel(_completions, _abstinence, _clock),
            HabitType.Journal => new JournalQuickAddViewModel(_completions, _clock, _diary),
            HabitType.State => new MeasuredQuickAddViewModel(_completions, _metrics, _refData, _clock, _timers),
            _ => null,
        };
        if (vm is null) { QuickAdd = null; return; }
        vm.Node = node;
        switch (vm)
        {
            case AbstinenceQuickAddViewModel ab: await ab.LoadAsync(ct).ConfigureAwait(true); break;
            case MeasuredQuickAddViewModel mq: await mq.LoadMetricsAsync(ct).ConfigureAwait(true); break;
            case CounterSeriesQuickAddViewModel cs: await cs.LoadMetricsAsync(ct).ConfigureAwait(true); break;
            case CounterTapQuickAddViewModel tap: await tap.LoadDayAsync(ct).ConfigureAwait(true); break;
        }
        QuickAdd = vm;
        if (vm is AbstinenceQuickAddViewModel abst) abst.Changed += OnAbstinenceChanged;

        // Counter splits by shape: bare (no metrics) records single-tap occurrences; with
        // metrics it records valid series rows (spec multivalue §4.1).
        async Task<QuickAddViewModelBase> BuildCounterQuickAddAsync(CancellationToken c)
        {
            var defs = await _metrics.ListByHabitAsync(node.Id, c).ConfigureAwait(true);
            return defs.Count == 0
                ? new CounterTapQuickAddViewModel(_completions, _clock)
                : new CounterSeriesQuickAddViewModel(_completions, _metrics, _refData, _clock, _timers);
        }
    }

    private async void OnCompletionEvent(EventEnvelope envelope)
    {
        // async void bus handler: swallow + trace transient failures so an event reaction can never
        // surface as an unobserved exception → DispatcherUnhandledException → crash (CR15).
        try
        {
            if (Node is null) return;
            var habitId = envelope.Payload switch
            {
                CompletionRecordedPayload r => r.HabitNodeId,
                CompletionUpdatedPayload u => u.HabitNodeId,
                CompletionDeletedPayload d => d.HabitNodeId,
                _ => Guid.Empty,
            };
            if (habitId != Node.Id) return;
            await Task.WhenAll(
                CompletionList.LoadAsync(Node.Id),
                Records.ReloadAsync(),
                TodayStrip.LoadAsync(Node),
                Calendar.JumpAsync())
                .ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnCompletionEvent failed: {ex}"); }
    }

    /// <summary>Abstinence Start/Lapse/Delete write no completions so the EventBus never fires; refresh
    /// everything that reads abstinence state here instead (CR15 async-void pattern): the Detail streak
    /// summary, the calendar markers, the Cronologia records list (so a just-lapsed period stops showing
    /// "In corso"), and the quick-add itself (so deleting the open period from the list flips it back to
    /// "Not started" instead of a stale running clock).</summary>
    private async void OnAbstinenceChanged(object? sender, EventArgs e)
    {
        try
        {
            if (Node is null) return;
            var reloads = new List<Task>
            {
                Detail.LoadAsync(Node, CancellationToken.None),
                Calendar.JumpAsync(),
                Records.ReloadAsync(),
                TodayStrip.LoadAsync(Node),
            };
            if (QuickAdd is AbstinenceQuickAddViewModel ab) reloads.Add(ab.LoadAsync());
            await Task.WhenAll(reloads).ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnAbstinenceChanged failed: {ex}"); }
    }

    private async void OnNoteEvent(EventEnvelope envelope)
    {
        try
        {
            if (Node is null) return;
            var habitId = envelope.Payload switch
            {
                NoteCreatedPayload c => c.HabitNodeId,
                NoteUpdatedPayload u => u.HabitNodeId,
                NoteDeletedPayload d => d.HabitNodeId,
                _ => Guid.Empty,
            };
            if (habitId != Node.Id) return;
            await Calendar.JumpAsync().ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnNoteEvent failed: {ex}"); }
    }

    // A habit edit in Configura (name/color/icon) must reflect live in the open workspace:
    // refresh the cached node and recolor the calendar markers.
    private async void OnHabitUpdatedEvent(EventEnvelope envelope)
    {
        try
        {
            if (Node is null || envelope.Payload is not HabitUpdatedPayload p || p.HabitNodeId != Node.Id) return;
            var refreshed = await _nodes.GetAsync(Node.Id).ConfigureAwait(true);
            if (refreshed is null || Node is null || refreshed.Id != Node.Id) return;
            Node = refreshed;
            OnPropertyChanged(nameof(Name));
            Calendar.CompletedColorHex = refreshed.Color;
            await Calendar.JumpAsync().ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnHabitUpdatedEvent failed: {ex}"); }
    }

    private async Task<CalendarMarkers> LoadMarkersAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var node = Node;
        if (node is null) return new CalendarMarkers([]);

        if (node.HabitType == HabitType.Resistance)
            return await BuildAbstinenceMarkersAsync(node, from, to, ct).ConfigureAwait(true);

        var magByDate = new Dictionary<DateOnly, decimal>();
        var noteByDate = new Dictionary<DateOnly, int>();

        var last = new DateOnly(to.Year, to.Month, 1);

        // Fire every month's two queries concurrently (each opens its own SQLite connection; readers
        // don't block readers) instead of 2×N sequential awaits, so re-running this on each
        // completion/note stays inside the home perf budget (CR24).
        var monthTasks = new List<(Task<IReadOnlyList<Recordum.Core.Abstractions.Services.Dtos.DailyMagnitude>> Mags,
                                   Task<IReadOnlyList<Recordum.Core.Abstractions.Services.Dtos.DayNoteCount>> Notes)>();
        for (var cursor = new DateOnly(from.Year, from.Month, 1); cursor <= last; cursor = cursor.AddMonths(1))
        {
            monthTasks.Add((
                _completions.GetDailyMagnitudeAsync(node.Id, cursor.Year, cursor.Month, ct).AsTask(),
                _noteService.ListNoteCountsInMonthAsync(node.Id, cursor.Year, cursor.Month, ct).AsTask()));
        }
        await Task.WhenAll(monthTasks.SelectMany(t => new Task[] { t.Mags, t.Notes })).ConfigureAwait(true);
        foreach (var (magsTask, notesTask) in monthTasks)
        {
            foreach (var m in magsTask.Result) magByDate[m.Date] = m.Magnitude;
            foreach (var n in notesTask.Result) noteByDate[n.Date] = n.Count;
        }

        var rangeMax = magByDate.Count > 0 ? magByDate.Values.Max() : 0m;
        var type = node.HabitType ?? HabitType.Action;

        var days = new List<CalendarDayMarker>();
        foreach (var date in magByDate.Keys.Union(noteByDate.Keys).Distinct())
        {
            // Presence in magByDate = a real completion that day. Kept distinct from the magnitude
            // value so a State day on a 0-based scale (mag == 0) still renders as recorded (CR5),
            // while a date that only carries a note (no completion) stays untinted.
            var hasCompletion = magByDate.TryGetValue(date, out var mv);
            var mag = hasCompletion ? mv : 0m;
            var noteCount = noteByDate.TryGetValue(date, out var nc) ? nc : 0;
            days.Add(new CalendarDayMarker(date, TintStepFor(type, mag, rangeMax, hasCompletion), NumberFor(type, mag, hasCompletion), noteCount));
        }
        return new CalendarMarkers(days);
    }

    /// <summary>Calendar marker builder for <see cref="HabitType.Resistance"/> habits. Derives day colors
    /// from <see cref="IAbstinenceService.ListPeriodsAsync"/> instead of the completion heatmap (which is
    /// always empty for Resistance in the new explicit-state model).</summary>
    /// <remarks>
    /// Classification per day in [from, to]:
    /// <list type="bullet">
    ///   <item>clean day (inside a period, not the closed end-day) → habit color + TintStep 3</item>
    ///   <item>lapse day (== a closed period's end-day) → danger hex + TintStep 4</item>
    ///   <item>gap day (after first period's start, not in any period, not future) → danger hex + TintStep 1</item>
    ///   <item>before first period / future → no marker</item>
    /// </list>
    /// Pauses are NOT reflected in calendar tinting (the streak engine still freezes streaks during pauses;
    /// only this coloring ignores them). Tracked as a known minor for a future improvement.
    /// </remarks>
    private async Task<CalendarMarkers> BuildAbstinenceMarkersAsync(
        HabitNode node, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var periods = await _abstinence.ListPeriodsAsync(node.Id, from, to, ct).ConfigureAwait(true);
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        // Notes still belong on Resistance days (diary/quick notes) even though the coloring is now
        // period-based, so gather note counts for the visible range and merge them into the markers.
        var noteByDate = new Dictionary<DateOnly, int>();
        var noteMonthTasks = new List<Task<IReadOnlyList<Recordum.Core.Abstractions.Services.Dtos.DayNoteCount>>>();
        var lastMonth = new DateOnly(to.Year, to.Month, 1);
        for (var cursor = new DateOnly(from.Year, from.Month, 1); cursor <= lastMonth; cursor = cursor.AddMonths(1))
            noteMonthTasks.Add(_noteService.ListNoteCountsInMonthAsync(node.Id, cursor.Year, cursor.Month, ct).AsTask());
        await Task.WhenAll(noteMonthTasks).ConfigureAwait(true);
        foreach (var t in noteMonthTasks)
            foreach (var n in t.Result) noteByDate[n.Date] = n.Count;

        // Resolve danger color from the WPF resource dictionary (null in headless/unit-test context).
        const string FallbackDangerHex = "#FFF87171";
        var dangerHex = FallbackDangerHex;
        var dangerBrush = System.Windows.Application.Current?.TryFindResource("Brush.Danger")
            as System.Windows.Media.SolidColorBrush;
        if (dangerBrush is not null)
            dangerHex = dangerBrush.Color.ToString(System.Globalization.CultureInfo.InvariantCulture); // #AARRGGBB e.g. "#FFF87171"

        if (periods.Count == 0)
        {
            // No abstinence periods yet, but note-only days must still render their badges.
            var noteOnly = noteByDate
                .Where(kv => kv.Value > 0)
                .Select(kv => new CalendarDayMarker(kv.Key, TintStep: 0, Number: null, NoteCount: kv.Value))
                .ToList();
            return new CalendarMarkers(noteOnly);
        }

        // Convert raw timestamps to date-range tuples.
        var dayPeriods = periods
            .Select(p => (
                StartDay: DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(p.StartedAtMs).UtcDateTime),
                EndDay: p.EndedAtMs.HasValue
                    ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(p.EndedAtMs.Value).UtcDateTime)
                    : today,
                IsClosed: p.EndedAtMs.HasValue))
            .ToList();

        var firstStart = dayPeriods.Min(p => p.StartDay);

        var days = new List<CalendarDayMarker>();
        foreach (var noteDate in noteByDate.Keys)
            if ((noteDate < from || noteDate > to) && noteByDate[noteDate] > 0)
                days.Add(new CalendarDayMarker(noteDate, TintStep: 0, Number: null, NoteCount: noteByDate[noteDate]));

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var noteCount = noteByDate.TryGetValue(day, out var nc) ? nc : 0;

            if (day > today)
            {
                // Future days carry no period coloring, but a note badge may still exist.
                if (noteCount > 0)
                    days.Add(new CalendarDayMarker(day, TintStep: 0, Number: null, NoteCount: noteCount));
                continue;
            }

            // Find the first period that contains this day.
            bool found = false;
            (DateOnly StartDay, DateOnly EndDay, bool IsClosed) hit = default;
            foreach (var p in dayPeriods)
            {
                if (p.StartDay <= day && day <= p.EndDay) { hit = p; found = true; break; }
            }

            if (found)
            {
                if (hit.IsClosed && day == hit.EndDay)
                    // Lapse day: end of a closed period → danger color, full intensity.
                    days.Add(new CalendarDayMarker(day, TintStep: 4, Number: null, NoteCount: noteCount, TintColorHex: dangerHex));
                else
                    // Clean day: inside a period (not the lapse end) → habit color (null = CompletedColorHex).
                    days.Add(new CalendarDayMarker(day, TintStep: 3, Number: null, NoteCount: noteCount, TintColorHex: null));
            }
            else if (day > firstStart)
            {
                // Gap day: after the first period started, not covered by any period → faint danger tint.
                days.Add(new CalendarDayMarker(day, TintStep: 1, Number: null, NoteCount: noteCount, TintColorHex: dangerHex));
            }
            else if (noteCount > 0)
            {
                // Before the first period's start: no coloring, but a note badge still shows.
                days.Add(new CalendarDayMarker(day, TintStep: 0, Number: null, NoteCount: noteCount));
            }
            // else: before the first period's start with no note → no marker (cell stays blank).
        }

        return new CalendarMarkers(days);
    }

    private static int TintStepFor(HabitType type, decimal mag, decimal monthMax, bool hasCompletion)
    {
        if (!hasCompletion) return 0; // only a note that day, no completion → no tint
        if (type is HabitType.Action or HabitType.Journal) return 4; // binario: fatto = pieno
        // A recorded day always gets at least the lowest non-empty step. State on a 0-based (or
        // negative) scale can legitimately record mag <= 0 yet must still show as filled (CR5);
        // Counter/Resistance always floor at 1.
        if (monthMax <= 0 || mag <= 0) return 1;
        var step = (int)Math.Ceiling((double)(mag / monthMax) * 4);
        return Math.Clamp(step, 1, 4);
    }

    private static string? NumberFor(HabitType type, decimal mag, bool hasCompletion)
    {
        if (!hasCompletion) return null;
        return type switch
        {
            // State shows its recorded value, including 0 (the cell IS the scale level).
            HabitType.State => FormatMag(mag),
            HabitType.Counter => mag > 0 ? FormatMag(mag) : null,
            HabitType.Resistance => mag > 1 ? FormatMag(mag) : null,
            _ => mag > 1 ? $"{FormatMag(mag)}×" : null, // Action/Journal: hidden unless multiple
        };
    }

    /// <summary>Compact magnitude for a calendar cell: exact below 1000, then abbreviated (1.2k / 13k /
    /// 125k / 1.2M / 12M) so it fits the count ring. The exact value stays in the cell tooltip.</summary>
    private static string FormatMag(decimal mag)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        if (mag >= 1000m)
            return AbbreviateLarge(mag, culture);
        return mag == Math.Truncate(mag)
            ? ((long)mag).ToString(culture)
            : mag.ToString("0.#", culture);
    }

    private static string AbbreviateLarge(decimal value, System.Globalization.CultureInfo culture)
    {
        var (scaled, suffix) = value < 1_000_000m ? (value / 1000m, "k") : (value / 1_000_000m, "M");
        // One decimal below 10 of the scaled unit (1.2k), whole numbers above (13k, 125k); trim a trailing .0.
        var rounded = scaled < 10m ? Math.Round(scaled, 1) : Math.Round(scaled, 0);
        var text = rounded == Math.Truncate(rounded)
            ? ((long)rounded).ToString(culture)
            : rounded.ToString("0.0", culture);
        return text + suffix;
    }

    private async void OnCalendarDateSelected(object? sender, DateOnly date)
    {
        try
        {
            if (QuickAdd is not null)
                QuickAdd.CompletionDate = date.ToDateTime(TimeOnly.MinValue);
            Notes.SetCalendarDay(date);
            await DayNote.LoadDayAsync(date).ConfigureAwait(true);
            await PushDaySummaryAsync(date, CancellationToken.None).ConfigureAwait(true);
            if (Node is not null) await Records.ShowDayAsync(date).ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnCalendarDateSelected failed: {ex}"); }
    }

    private async Task PushDaySummaryAsync(DateOnly date, CancellationToken ct)
    {
        var node = Node;
        if (node is null) return;
        var mags = await _completions.GetDailyMagnitudeAsync(node.Id, date.Year, date.Month, ct).ConfigureAwait(true);
        var dayMag = mags.FirstOrDefault(m => m.Date == date);
        var mag = dayMag?.Magnitude ?? 0m;
        // Enrich the aggregate with the primary metric's unit (Counter) or scale label (State) so the
        // strip reads "4 km" / "Good" instead of a bare number (R-daySummaryUnits).
        var (unit, scaleLabels) = await ResolvePrimaryDisplayAsync(node, ct).ConfigureAwait(true);
        var label = DaySummaryFormatter.Label(node.HabitType ?? HabitType.Action, mag, unit, scaleLabels);
        var notes = await _noteService.ListByHabitAndDateAsync(node.Id, date, ct).ConfigureAwait(true);
        // Presence (a completion exists) drives the strip, not the value: a State 0 still counts (CR5).
        DayNote.SetDaySummary(dayMag is not null, label, notes.Count);
    }

    /// <summary>Resolves the primary (lowest SortOrder) metric's unit code (Counter) or scale labels
    /// (State) for the day-summary strip. Returns nulls for other types or when none apply.</summary>
    private async Task<(string? unit, IReadOnlyList<string>? scaleLabels)> ResolvePrimaryDisplayAsync(HabitNode node, CancellationToken ct)
    {
        var type = node.HabitType ?? HabitType.Action;
        if (type is not (HabitType.Counter or HabitType.State)) return (null, null);

        var metrics = await _metrics.ListByHabitAsync(node.Id, ct).ConfigureAwait(true);
        var primary = metrics.OrderBy(m => m.SortOrder).FirstOrDefault();
        if (primary is null) return (null, null);

        if (type == HabitType.Counter)
        {
            if (primary.UnitOfMeasureId is not { } uid) return (null, null);
            var units = await _refData.ListUnitsAsync(ct).ConfigureAwait(true);
            return (units.FirstOrDefault(u => u.Id == uid)?.Code, null);
        }

        // State
        if (primary.ScaleTemplateId is not { } sid) return (null, null);
        var scales = await _refData.ListScalesAsync(ct).ConfigureAwait(true);
        var tmpl = scales.FirstOrDefault(s => s.Id == sid);
        var labels = ParseScaleLabels(primary.LabelsJson) ?? ParseScaleLabels(tmpl?.LabelsJson);
        return (null, labels);
    }

    private static string[]? ParseScaleLabels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(json); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    [RelayCommand]
    private void OpenConfigure()
    {
        // Idempotent: opening when already open still re-raises so the host reloads the editor.
        if (IsConfiguraOpen) ConfigureRequested?.Invoke(this, EventArgs.Empty);
        else IsConfiguraOpen = true; // the flag's change handler raises ConfigureRequested
    }

    [RelayCommand]
    private void Export() => ExportRequested?.Invoke(this, EventArgs.Empty);
}
