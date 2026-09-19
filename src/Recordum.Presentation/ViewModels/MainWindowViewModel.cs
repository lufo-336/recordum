using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Export;
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
    [RelayCommand] private Task BackupRestoreAsync() => _backupLauncher.ShowRestoreAsync();
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
            if (Home.SelectedRow is not { } row) { Workspace.ActivatePanoramica(); return; }
            await Workspace.OpenSubjectAsync(row.HabitNodeId, KindOf(row), row.Name).ConfigureAwait(true);
        }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"OnHomePropertyChanged failed: {ex}"); }
    }

    private static WorkspaceTabKind KindOf(HomeRowViewModel row)
        => row.IsGroup ? WorkspaceTabKind.Group : WorkspaceTabKind.Habit;

    // "+" on a habit whose row is already selected: SelectedRow won't change, so pin
    // the subject in the workspace to keep it from being replaced by the next preview.
    private async void OnHabitActivationRequested(object? sender, Guid habitNodeId)
    {
        try
        {
            var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == habitNodeId);
            if (row is null) return;
            await Workspace.OpenSubjectAsync(habitNodeId, KindOf(row), row.Name, pin: true).ConfigureAwait(true);
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
