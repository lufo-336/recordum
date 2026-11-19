using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Export;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Recordum.Presentation.ViewModels.Settings;
using Recordum.Presentation.ViewModels.Timer;
using Recordum.Presentation.ViewModels.Workspace;

namespace Recordum.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IAppSettingService _appSettings;
    private readonly IExportDialogLauncher _exportLauncher;
    private readonly IBackupDialogLauncher _backupLauncher;
    private readonly ISettingsWindowLauncher _settingsLauncher;
    private readonly IApplicationLifetime _lifetime;

    public HomeViewModel Home { get; }

    public WorkspaceViewModel Workspace { get; }
    public TimerStatusBarViewModel TimerStatusBar { get; }
    public Charts.ChartsWorkspaceViewModel Charts { get; }
    public BackupHistoryViewModel BackupHistory { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeWorkspace))]
    [NotifyPropertyChangedFor(nameof(IsChartsWorkspace))]
    [NotifyPropertyChangedFor(nameof(IsBackupHistoryWorkspace))]
    private AppWorkspace _currentWorkspace = AppWorkspace.Home;

    public bool IsHomeWorkspace => CurrentWorkspace == AppWorkspace.Home;
    public bool IsChartsWorkspace => CurrentWorkspace == AppWorkspace.Charts;
    public bool IsBackupHistoryWorkspace => CurrentWorkspace == AppWorkspace.BackupHistory;

    /// <summary>Hides the left icon ribbon (the 44px Home/Charts/Backup strip). Toggled by the
    /// title-bar ☰. Independent of the Home-tree sidebar collapse (<see cref="WorkspaceViewModel.SidebarCollapsed"/>),
    /// which the ⌂ ribbon icon still drives.</summary>
    [ObservableProperty] private bool _ribbonCollapsed;

    public MainWindowViewModel(
        HomeViewModel home,
        IAppSettingService appSettings,
        TimerStatusBarViewModel timerStatusBar,
        WorkspaceViewModel workspace,
        Charts.ChartsWorkspaceViewModel charts,
        IExportDialogLauncher exportLauncher,
        IBackupDialogLauncher backupLauncher,
        BackupHistoryViewModel backupHistory,
        ISettingsWindowLauncher settingsLauncher,
        IApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(timerStatusBar);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(charts);
        ArgumentNullException.ThrowIfNull(exportLauncher);
        ArgumentNullException.ThrowIfNull(backupLauncher);
        ArgumentNullException.ThrowIfNull(backupHistory);
        ArgumentNullException.ThrowIfNull(settingsLauncher);
        ArgumentNullException.ThrowIfNull(lifetime);
        Home = home;
        _appSettings = appSettings;
        TimerStatusBar = timerStatusBar;
        Workspace = workspace;
        Charts = charts;
        _exportLauncher = exportLauncher;
        _backupLauncher = backupLauncher;
        BackupHistory = backupHistory;
        _settingsLauncher = settingsLauncher;
        _lifetime = lifetime;
        Home.PropertyChanged += OnHomePropertyChanged;
        Home.HabitActivationRequested += OnHabitActivationRequested;
        Home.SelectedRowClicked += OnSelectedRowClicked;
        Home.GroupCreated += OnHomeGroupCreated;
        // Empty-click in the tree → full-width Home, even when SelectedRow is already null (a tab can be
        // active without a tree selection): the SelectedRow setter wouldn't notify, so deactivate here.
        Home.BackgroundClicked += (_, _) => Workspace.ActivatePanoramica();
        Workspace.ExportRequested += OnWorkspaceExportRequested;
        Workspace.SubjectActivationRequested += async (_, id) =>
        {
            var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == id);
            if (row is not null) await Workspace.OpenSubjectAsync(id, KindOf(row), row.Name).ConfigureAwait(true);
        };
        // Persist the light workspace session whenever tabs/active/sidebar change (SP1: no debounce).
        Workspace.Tabs.CollectionChanged += async (_, _) => await Workspace.SaveStateAsync().ConfigureAwait(true);
        Workspace.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName is nameof(WorkspaceViewModel.ActiveSubjectTab)
                or nameof(WorkspaceViewModel.SidebarCollapsed) or nameof(WorkspaceViewModel.SidebarWidth))
                await Workspace.SaveStateAsync().ConfigureAwait(true);
        };
    }

    [RelayCommand]
    private void ShowHome()
    {
        // Model A: from Charts/Backup, ⌂ returns to the Home workspace. When already on Home it
        // collapses/expands the tree sidebar, so an open habit's columns + focus can fill the window
        // (collapse) and the sidebar comes back (expand). The title-bar ☰ does the same toggle.
        // To make the *tree* fill the window instead, close the tab (✕ / Ctrl+W) or click empty tree space.
        if (CurrentWorkspace != AppWorkspace.Home)
        {
            CurrentWorkspace = AppWorkspace.Home;
            return;
        }
        Workspace.ToggleSidebar();
    }

    /// <summary>Title-bar ☰: show/hide the left icon ribbon.</summary>
    [RelayCommand]
    private void ToggleRibbon() => RibbonCollapsed = !RibbonCollapsed;

    [RelayCommand]
    private async Task ShowChartsAsync()
    {
        CurrentWorkspace = AppWorkspace.Charts;
        await Charts.LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ShowBackupHistoryAsync()
    {
        CurrentWorkspace = AppWorkspace.BackupHistory;
        await BackupHistory.LoadAsync();
    }

    [RelayCommand] private Task BackupCreateAsync() => _backupLauncher.ShowBackupAsync();
    [RelayCommand] private async Task BackupRestoreAsync() => await _backupLauncher.ShowRestoreAsync().ConfigureAwait(true);
    [RelayCommand] private Task BackupSettingsAsync() => _settingsLauncher.ShowAsync(SettingsCategoryId.Backup);
    [RelayCommand] private Task ShowSettingsAsync() => _settingsLauncher.ShowAsync();

    public async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        await Home.LoadCommand.ExecuteAsync(null);
        // After Home.Rows is populated so the title resolver can name persisted subjects.
        await Workspace.RestoreStateAsync(ResolveTitle, ct).ConfigureAwait(true);
        await TimerStatusBar.LoadAsync(ct).ConfigureAwait(true);
    }

    private string ResolveTitle(Guid id, WorkspaceTabKind kind)
        => Home.Rows.FirstOrDefault(r => r.HabitNodeId == id)?.Name ?? string.Empty;

    [RelayCommand]
    private void Quit() => _lifetime.Shutdown();

    private async void OnHomePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // async void event handler: a transient failure (e.g. a DB hiccup) must not escape as an
        // unobserved exception → DispatcherUnhandledException → crash (CR15).
        try
        {
            if (e.PropertyName != nameof(HomeViewModel.SelectedRow)) return;
            if (Home.SelectedRow is not { } row) { _selectedSubject = null; Workspace.ActivatePanoramica(); return; }
            // La Home rifà le righe a ogni registrazione, cambio di giorno o filtro e rimette la selezione (anche su una riga
            // sola rimasta da due scelte, GIO-11): non è l'utente che apre, e il giorno scelto nella schermata non si tocca.
            var recording = Home.RecordRequestedFor == row.HabitNodeId;
            // Una scheda che nasce adesso parte comunque dal giorno della Home.
            var restoredOpenTab = Home.IsRestoringSelection && Workspace.Tabs.Any(t => t.SubjectId == row.HabitNodeId);
            var opened = recording || (_selectedSubject != row.HabitNodeId && !restoredOpenTab);
            _selectedSubject = row.HabitNodeId;
            await Workspace.OpenSubjectAsync(row.HabitNodeId, KindOf(row), row.Name).ConfigureAwait(true);
            if (opened) await LandOnHomeDayAsync(row, recording).ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnHomePropertyChanged failed: {ex}"); }
    }

    private Guid? _selectedSubject;

    /// <summary>Dopo un'apertura fatta dall'utente dalla Home (onda "Giornata", A1; GIO-6): vince il giorno della Home,
    /// oggi compreso. Unica eccezione: una schermata con una scrittura in corso su un altro giorno (bozza del diario, nota
    /// del giorno non salvata) resta dov'è, senza domande. Se l'utente aveva chiesto di registrare e l'astinenza è in
    /// corso, la schermata propone la ricaduta, con la data su cui si trova. Un gruppo resta su oggi, come la sua
    /// Panoramica.</summary>
    private async Task LandOnHomeDayAsync(HomeRowViewModel row, bool recording)
    {
        if (row.IsGroup) return;
        if (Workspace.Tabs.FirstOrDefault(t => t.SubjectId == row.HabitNodeId)?.Screen is not { HasNode: true } screen) return;
        var day = row.RecordDay ?? screen.CurrentDay;
        if (screen.SelectedDay != day && !screen.HasWritingInProgress) await screen.SelectDayAsync(day).ConfigureAwait(true);
        if (recording && screen.QuickAdd is AbstinenceQuickAddViewModel abstinence) abstinence.RequestLapsePrompt();
    }

    private static WorkspaceTabKind KindOf(HomeRowViewModel row)
        => row.IsGroup ? WorkspaceTabKind.Group : WorkspaceTabKind.Habit;

    // Clic sulla riga già scelta: niente si apre, ma la schermata torna sul giorno della Home (GIO-6).
    private async void OnSelectedRowClicked(object? sender, Guid habitNodeId)
    {
        try
        {
            if (Home.SelectedRow is { } row && row.HabitNodeId == habitNodeId)
                await LandOnHomeDayAsync(row, recording: false).ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnSelectedRowClicked failed: {ex}"); }
    }

    // "+" on a habit whose row is already selected, or Enter on the selected row: SelectedRow won't change, so pin
    // the subject in the workspace to keep it from being replaced by the next preview.
    private async void OnHabitActivationRequested(object? sender, Guid habitNodeId)
    {
        try
        {
            var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == habitNodeId);
            if (row is null) return;
            // Letto prima di ogni await: vale solo mentre la Home alza l'evento. Invio apre, non registra.
            var recording = Home.RecordRequestedFor == habitNodeId;
            await Workspace.OpenSubjectAsync(habitNodeId, KindOf(row), row.Name, pin: true).ConfigureAwait(true);
            await LandOnHomeDayAsync(row, recording).ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnHabitActivationRequested failed: {ex}"); }
    }

    // New group from ＋Nuova▾: open it as a pinned tab (Configura auto-opens per-tab in Task 2).
    private async void OnHomeGroupCreated(object? sender, Guid id)
    {
        try
        {
            var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == id);
            if (row is null) return;
            await Workspace.OpenSubjectAsync(id, KindOf(row), row.Name, pin: true).ConfigureAwait(true);
            // Un gruppo nuovo si apre su Configura (decisione 7 di F2b): e' li' che si rinomina e si ricolora.
            if (Workspace.Tabs.FirstOrDefault(t => t.SubjectId == id) is { } tab) await tab.Screen.OpenConfiguraForSubjectAsync().ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnHomeGroupCreated failed: {ex}"); }
    }

    [RelayCommand]
    private async Task ExportLibraryAsync() => await _exportLauncher.ShowForLibraryAsync().ConfigureAwait(true);

    // A tab asked to export its subject: resolve the display name from its Home row.
    private async void OnWorkspaceExportRequested(object? sender, Guid subjectId)
    {
        try
        {
            var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == subjectId);
            await _exportLauncher.ShowForHabitAsync(subjectId, row?.Name ?? string.Empty).ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnWorkspaceExportRequested failed: {ex}"); }
    }

    [RelayCommand]
    private async Task ShowDiagnosticInfoAsync() => await _settingsLauncher.ShowAsync(SettingsCategoryId.Diagnostics).ConfigureAwait(true);
}
