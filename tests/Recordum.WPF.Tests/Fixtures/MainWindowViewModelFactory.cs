using Microsoft.Web.WebView2.Core;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Services;
using Recordum.Plugins.ExtensionPoints;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Services;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.Backup;
using Recordum.WPF.ViewModels.Charts;
using Recordum.WPF.ViewModels.Export;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Timer;

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
        var configs = new FakeHabitConfigService();
        var goals = new FakeGoalService();
        var metrics = new FakeMetricDefinitionService();
        var notif = new FakeNotificationService();
        var tags = new FakeTagService();
        var settings = new FakeAppSettingService();
        var home = new HomeViewModel(query, nodes, purge);
        var editor = new HabitEditorViewModel(nodes, configs, goals, metrics, notif, tags, settings, new FakeReferenceData(), new RecentColorStore(settings));
        var clock = new FakeClock();
        var ats = new FakeActiveTimerService();
        var completions = new FakeCompletionService();
        var timerStatusBar = new TimerStatusBarViewModel(ats, clock, nodes);
        var habitScreen = new HabitScreenViewModel(
            nodes, completions, ats, metrics, new FakeReferenceData(), configs, new FakeNoteService(),
            goals, new IntensityRampResolver(),
            new FakeStreakAggregator(), new FakeTrendQueryService(), new FakeHourlyDistributionQueryService(),
            clock, new FakeEventBus(), new FakeWebView2Availability(), settings, editor);
        var charts = new ChartsWorkspaceViewModel(
            new FakeChartService(), new FakeChartQueryService(),
            new ExtensionPointRegistry(),
            new ChartBuilderViewModel(new FakeChartService()));
        var exportLauncher = new FakeExportDialogLauncher();
        var backupLauncher = launcher ?? new FakeBackupDialogLauncher();
        var backupLog = log ?? new FakeBackupLogService();
        var backupHistory = new BackupHistoryViewModel(backupLog);

        return new MainWindowViewModel(home, editor, settings, timerStatusBar, habitScreen, charts, exportLauncher, backupLauncher, backupHistory, new FakeSettingsWindowLauncher());
    }
}
