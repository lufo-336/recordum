using Recordum.Core.Abstractions.Services;
using Recordum.Core.Services;
using Recordum.Plugins.ExtensionPoints;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Services;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.Charts;
using Recordum.WPF.ViewModels.Timer;
using Recordum.WPF.ViewModels.Workspace;

namespace Recordum.WPF.Tests.Fixtures;

public static class MainWindowViewModelFactory
{
    public static MainWindowViewModel Build(
        IBackupDialogLauncher? launcher = null,
        IBackupLogService? log = null)
    {
        var query = new FakeHabitTreeQueryService();
        var nodes = new FakeHabitNodeService();
        var purge = new FakePurgeService();
        var settings = new FakeAppSettingService();
        var home = new HomeViewModel(query, nodes, purge);
        var clock = new FakeClock();
        var ats = new FakeActiveTimerService();
        var timerStatusBar = new TimerStatusBarViewModel(ats, clock, nodes);
        var workspace = new WorkspaceViewModel(new FakeSubjectScreenFactory(), settings);
        var charts = new ChartsWorkspaceViewModel(
            new FakeChartService(), new FakeChartQueryService(),
            new ExtensionPointRegistry(),
            new ChartBuilderViewModel(new FakeChartService()));
        var exportLauncher = new FakeExportDialogLauncher();
        var backupLauncher = launcher ?? new FakeBackupDialogLauncher();
        var backupLog = log ?? new FakeBackupLogService();
        var backupHistory = new BackupHistoryViewModel(backupLog);

        return new MainWindowViewModel(home, settings, timerStatusBar, workspace, charts, exportLauncher, backupLauncher, backupHistory, new FakeSettingsWindowLauncher());
    }
}
