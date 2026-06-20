using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.Services;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.Export;
using Recordum.WPF.ViewModels.Settings;
using Recordum.WPF.ViewModels.Timer;
using Recordum.WPF.ViewModels.Workspace;

namespace Recordum.WPF.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{

    internal const string HabitScreenWidthSettingKey = "ui.habit_screen_width";
    internal const double DefaultHabitScreenWidth = 520d;
    internal const double MinHabitScreenWidth = 360d;
    internal const double MaxHabitScreenWidth = 1100d;

    private readonly IAppSettingService _appSettings;
    private readonly IExportDialogLauncher _exportLauncher;
    private readonly IBackupDialogLauncher _backupLauncher;
    private readonly ISettingsWindowLauncher _settingsLauncher;

    public HomeViewModel Home { get; }

    public WorkspaceViewModel Workspace { get; }
    public TimerStatusBarViewModel TimerStatusBar { get; }
    public Charts.ChartsWorkspaceViewModel Charts { get; }
    public BackupHistoryViewModel BackupHistory { get; }

    [ObservableProperty] private double _habitScreenWidth = DefaultHabitScreenWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeWorkspace))]
    [NotifyPropertyChangedFor(nameof(IsChartsWorkspace))]
    [NotifyPropertyChangedFor(nameof(IsBackupHistoryWorkspace))]
    private AppWorkspace _currentWorkspace = AppWorkspace.Home;

    public bool IsHomeWorkspace => CurrentWorkspace == AppWorkspace.Home;
    public bool IsChartsWorkspace => CurrentWorkspace == AppWorkspace.Charts;
    public bool IsBackupHistoryWorkspace => CurrentWorkspace == AppWorkspace.BackupHistory;

    public MainWindowViewModel(
        HomeViewModel home,
        IAppSettingService appSettings,
        TimerStatusBarViewModel timerStatusBar,
        WorkspaceViewModel workspace,
        Charts.ChartsWorkspaceViewModel charts,
        IExportDialogLauncher exportLauncher,
        IBackupDialogLauncher backupLauncher,
        BackupHistoryViewModel backupHistory,
        ISettingsWindowLauncher settingsLauncher)
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
        Home = home;
        _appSettings = appSettings;
        TimerStatusBar = timerStatusBar;
        Workspace = workspace;
        Charts = charts;
        _exportLauncher = exportLauncher;
        _backupLauncher = backupLauncher;
        BackupHistory = backupHistory;
        _settingsLauncher = settingsLauncher;
        Home.PropertyChanged += OnHomePropertyChanged;
        Home.HabitActivationRequested += OnHabitActivationRequested;
        Home.GroupCreated += OnHomeGroupCreated;
        Workspace.ExportRequested += OnWorkspaceExportRequested;
        Workspace.SubjectActivationRequested += async (_, id) =>
        {
            var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == id);
            if (row is not null) await Workspace.OpenSubjectAsync(id, KindOf(row), row.Name).ConfigureAwait(true);
        };
    }

    [RelayCommand]
    private void ShowHome()
    {
        CurrentWorkspace = AppWorkspace.Home;
        // Clicking "Home" returns to a clean tree: deselecting closes the open habit
        // workspace + editor columns (their visibility/width track SelectedRow). Without
        // this, the only way to collapse them was to bounce through Archived/Trash.
        Home.SelectedRow = null;
    }

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
        await LoadHabitScreenWidthAsync(ct).ConfigureAwait(true);
        await Home.LoadCommand.ExecuteAsync(null);
        await TimerStatusBar.LoadAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private static void Quit() => System.Windows.Application.Current?.Shutdown();

    public async Task CommitHabitScreenWidthAsync(CancellationToken ct = default)
    {
        var clamped = Math.Clamp(HabitScreenWidth, MinHabitScreenWidth, MaxHabitScreenWidth);
        if (Math.Abs(clamped - HabitScreenWidth) > 0.5)
            HabitScreenWidth = clamped;
        await _appSettings.SetAsync(
            HabitScreenWidthSettingKey,
            HabitScreenWidth.ToString("0.##", CultureInfo.InvariantCulture),
            ct).ConfigureAwait(true);
    }

    private async ValueTask LoadHabitScreenWidthAsync(CancellationToken ct)
        => HabitScreenWidth = await LoadWidthAsync(HabitScreenWidthSettingKey, DefaultHabitScreenWidth, MinHabitScreenWidth, MaxHabitScreenWidth, ct);

    private async Task<double> LoadWidthAsync(string key, double @default, double min, double max, CancellationToken ct)
    {
        var raw = await _appSettings.GetAsync(key, ct).ConfigureAwait(true);
        var value = @default;
        if (raw is not null
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed))
        {
            value = parsed;
        }
        return Math.Clamp(value, min, max);
    }

    private async void OnHomePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.SelectedRow)) return;
        if (Home.SelectedRow is not { } row) { Workspace.ActivatePanoramica(); return; }
        await Workspace.OpenSubjectAsync(row.HabitNodeId, KindOf(row), row.Name).ConfigureAwait(true);
    }

    private static WorkspaceTabKind KindOf(HomeRowViewModel row)
        => row.IsGroup ? WorkspaceTabKind.Group : WorkspaceTabKind.Habit;

    // "+" on a habit whose row is already selected: SelectedRow won't change, so pin
    // the subject in the workspace to keep it from being replaced by the next preview.
    private async void OnHabitActivationRequested(object? sender, Guid habitNodeId)
    {
        var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == habitNodeId);
        if (row is null) return;
        await Workspace.OpenSubjectAsync(habitNodeId, KindOf(row), row.Name, pin: true).ConfigureAwait(true);
    }

    // New group from ＋Nuova▾: open it as a pinned tab (Configura auto-opens per-tab in Task 2).
    private async void OnHomeGroupCreated(object? sender, Guid id)
    {
        var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == id);
        if (row is null) return;
        await Workspace.OpenSubjectAsync(id, KindOf(row), row.Name, pin: true).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExportLibraryAsync() => await _exportLauncher.ShowForLibraryAsync().ConfigureAwait(true);

    // A tab asked to export its subject: resolve the display name from its Home row.
    private async void OnWorkspaceExportRequested(object? sender, Guid subjectId)
    {
        var row = Home.Rows.FirstOrDefault(r => r.HabitNodeId == subjectId);
        await _exportLauncher.ShowForHabitAsync(subjectId, row?.Name ?? string.Empty).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ShowDiagnosticInfoAsync() => await _settingsLauncher.ShowAsync(SettingsCategoryId.Diagnostics).ConfigureAwait(true);
}
