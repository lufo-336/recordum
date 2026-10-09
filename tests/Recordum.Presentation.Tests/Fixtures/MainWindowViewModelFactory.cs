using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Services;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Charts;
using Recordum.Presentation.ViewModels.Timer;
using Recordum.Presentation.ViewModels.Workspace;

namespace Recordum.Presentation.Tests.Fixtures;

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
        var timerStatusBar = new TimerStatusBarViewModel(ats, clock, nodes, new FakeEventBus(), NullTickSourceFactory.Instance);
        var workspace = new WorkspaceViewModel(new FakeSubjectScreenFactory(), settings);
        var charts = new ChartsWorkspaceViewModel(
            new FakeChartService(), new FakeChartQueryService(),
            new ExtensionPointRegistry(),
            new ChartBuilderViewModel(new FakeChartService(), new FakeHabitTreeQueryService(), new FakeMetricDefinitionService(),
                new FakeChartQueryService(), new ExtensionPointRegistry(), null));
        var exportLauncher = new FakeExportDialogLauncher();
        var backupLauncher = launcher ?? new FakeBackupDialogLauncher();
        var backupLog = log ?? new FakeBackupLogService();
        var backupHistory = new BackupHistoryViewModel(backupLog);

        return new MainWindowViewModel(home, settings, timerStatusBar, workspace, charts, exportLauncher, backupLauncher, backupHistory, new FakeSettingsWindowLauncher(), new FakeApplicationLifetime());
    }
}
