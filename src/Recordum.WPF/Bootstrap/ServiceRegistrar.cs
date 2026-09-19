using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Backup;
using Recordum.Core.Enums;
using Recordum.Data;
using Recordum.Data.Backup;
using Recordum.Data.Interceptors;
using Recordum.Data.Security;
using Recordum.Data.Services;
using Recordum.Core.Services;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.ChartEngine;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.ExportEngine;
using Recordum.Plugins.Events;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Plugins.Hosting;
using Recordum.Plugins.Management;
using Recordum.Plugins.Permissions;
using Recordum.Plugins.Routing;
using Recordum.WPF.Theming;
using Recordum.WPF.Services;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Charts;
using Recordum.Presentation.ViewModels.DiagnosticInfo;
using Recordum.Presentation.ViewModels.Export;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.Presentation.ViewModels.Workspace;
using Recordum.Presentation.ViewModels.Timer;
using Recordum.WPF.Views;

namespace Recordum.WPF.Bootstrap;

public static class ServiceRegistrar
{
    public static IServiceCollection AddRecordumServices(this IServiceCollection services, RecordumOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        // Core abstractions + storage paths.
        services.AddSingleton(options);
        services.AddSingleton<IDbStoragePaths>(options);
        services.AddSingleton<IBackupStoragePaths>(options);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPluginContext, AmbientPluginContext>();
        services.AddLogging();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAppFontService, AppFontService>();
        services.AddSingleton<ICultureService, CultureService>();
        services.AddSingleton<IWebView2Availability, WebView2Availability>();
        services.AddSingleton<IEditorAvailability>(sp => sp.GetRequiredService<IWebView2Availability>());
        // Il battito dell'interfaccia: una sola fabbrica per il processo, ogni consumatore crea la
        // propria sorgente col proprio intervallo. La gemella Avalonia si registra nella sua shell.
        services.AddSingleton<ITickSourceFactory, DispatcherTickSourceFactory>();

        // Security: DPAPI by default. Passphrase opt-in via IKeyTransitionService.
#pragma warning disable CA1416 // Windows-only target for Fase 3 WPF.
        services.AddSingleton<IDbKeyProvider>(_ => new DpapiKeyProvider(options.KeyFilePath));
#pragma warning restore CA1416

        // Interceptors — registration order = invocation order in EF Core.
        services.AddSingleton<HabitConfigHistoryInterceptor>();
        services.AddSingleton<TimestampInterceptor>();
        services.AddSingleton<SqlCipherKeyConnectionInterceptor>();

        services.AddDbContextFactory<RecordumDbContext>((sp, builder) =>
        {
            var history = sp.GetRequiredService<HabitConfigHistoryInterceptor>();
            var timestamp = sp.GetRequiredService<TimestampInterceptor>();
            var cipher = sp.GetRequiredService<SqlCipherKeyConnectionInterceptor>();
            builder
                .UseSqlite(options.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(history, timestamp, cipher);
        });

        // 14 aggregate/query services + 1 key transition (Task 32). Scoped — per-operation IPluginContext capture.
        services.AddScoped<IHabitNodeService, HabitNodeService>();
        services.AddScoped<IComposedHabitService, ComposedHabitService>();
        services.AddScoped<IHabitConfigService, HabitConfigService>();
        services.AddScoped<IHabitUiPrefsService, HabitUiPrefsService>();
        services.AddScoped<IHabitTemplateService, HabitTemplateService>();
        services.AddScoped<ICompletionService, CompletionService>();
        services.AddScoped<IActiveTimerService, ActiveTimerService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<Recordum.Core.IAttachmentService>(sp => new Data.Services.AttachmentService(
            sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IPluginContext>()));
        services.AddScoped<IDiaryService, Data.Services.DiaryService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IMetricDefinitionService, MetricDefinitionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IChartService, ChartService>();
        services.AddScoped<IExportTemplateService, ExportTemplateService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IStreakPauseService, StreakPauseService>();
        services.AddScoped<IAbstinenceService, Data.Services.AbstinenceService>();
        services.AddScoped<IStreakService, StreakService>();
        services.AddScoped<IPurgeService, PurgeService>();
        services.AddScoped<IHabitTreeQueryService, HabitTreeQueryService>();
        services.AddScoped<IKeyTransitionService, KeyTransitionService>();
        services.AddScoped<IAppSettingService, AppSettingService>();
        services.AddScoped<IRecentColorStore, RecentColorStore>();
        services.AddScoped<IFavoriteColorStore, FavoriteColorStore>();
        services.AddScoped<IResetTimeResolver, ResetTimeResolver>();
        // Passo di manutenzione R9, eseguito una volta dall'ApplicationInitializer.
        services.AddScoped<Recordum.Data.Maintenance.RetroactiveNoonBackfill>();
        services.AddScoped<IScheduleEvaluator, Data.Services.Evaluators.ScheduleEvaluator>();
        services.AddSingleton<IIntensityRampResolver, IntensityRampResolver>();
        services.AddKeyedScoped<IStreakModeEvaluator, Data.Services.Evaluators.DailyStreakModeEvaluator>(StreakMode.Daily);
        services.AddKeyedScoped<IStreakModeEvaluator, Data.Services.Evaluators.WeeklyStreakModeEvaluator>(StreakMode.Weekly);
        services.AddKeyedScoped<IStreakModeEvaluator, Data.Services.Evaluators.OccurrencesStreakModeEvaluator>(StreakMode.Occurrences);
        services.AddScoped<Data.Services.Evaluators.DailyStreakModeEvaluator>();
        services.AddScoped<Data.Services.Evaluators.WeeklyStreakModeEvaluator>();
        services.AddKeyedScoped<IStreakModeEvaluator>(StreakMode.Both, (sp, _) =>
            new Data.Services.Evaluators.BothStreakModeEvaluator(
                sp.GetRequiredService<Data.Services.Evaluators.DailyStreakModeEvaluator>(),
                sp.GetRequiredService<Data.Services.Evaluators.WeeklyStreakModeEvaluator>()));
        services.AddKeyedScoped<IStreakRecoveryPolicy, Data.Services.Policies.NoneStreakRecoveryPolicy>(StreakRecovery.None);
        services.AddKeyedScoped<IStreakRecoveryPolicy, Data.Services.Policies.GraceDayStreakRecoveryPolicy>(StreakRecovery.GraceDay);
        services.AddKeyedScoped<IStreakRecoveryPolicy, Data.Services.Policies.ManualStreakRecoveryPolicy>(StreakRecovery.Manual);
        services.AddKeyedScoped<IStreakRecoveryPolicy, Data.Services.Policies.RetroactiveStreakRecoveryPolicy>(StreakRecovery.Retroactive);
        services.AddScoped<IStreakAggregator, Data.Services.Aggregators.SchedulingAwareStreakAggregator>();
        services.AddScoped<ITrendQueryService, TrendQueryService>();
        services.AddScoped<IHourlyDistributionQueryService, HourlyDistributionQueryService>();
        services.AddScoped<IChartQueryService, ChartQueryService>();
        services.AddScoped<IExportQueryService, ExportQueryService>();
        services.AddScoped<IExportJobService, ExportJobService>();
        services.AddScoped<IReferenceDataQueryService, ReferenceDataQueryService>();

        // Plugin host stack.
        services.AddSingleton<IPermissionBroker, AlwaysAllowPermissionBroker>();
        services.AddSingleton<IExtensionPointRegistry, ExtensionPointRegistry>();
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IApiRouter, ApiRouter>();
        services.AddSingleton<IPluginHost, InProcessPluginHost>();
        services.AddSingleton<IPluginManager, PluginManager>();
        services.AddSingleton<IChartEngineActivator, ChartEngineActivator>();
        services.AddSingleton<IExportEngineActivator, ExportEngineActivator>();

        // ViewModels.
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<Recordum.Presentation.ViewModels.Settings.GeneralSettingsViewModel>();
        services.AddTransient<Recordum.Presentation.ViewModels.Settings.SettingsWindowViewModel>();
        services.AddSingleton<ISettingsWindowLauncher, WpfSettingsWindowLauncher>();
        services.AddTransient<HabitEditorViewModel>();
        services.AddTransient<HabitTemplatePickerViewModel>();
        services.AddSingleton<IHabitTemplatePickerLauncher, WpfHabitTemplatePickerLauncher>();
        services.AddTransient<HabitBuilderViewModel>();
        services.AddSingleton<IHabitBuilderLauncher, WpfHabitBuilderLauncher>();
        services.AddTransient<TimerStatusBarViewModel>();
        services.AddTransient<TimerRecoveryService>();
        services.AddTransient<HabitScreenViewModel>();
        // Each workspace tab gets its own DI scope via ISubjectScreenFactory (disposed on tab close).
        services.AddSingleton<ISubjectScreenFactory, SubjectScreenFactory>();
        services.AddTransient<WorkspaceViewModel>();

        services.AddScoped<IQuickActionHandler, ActionQuickActionHandler>();
        services.AddScoped<IQuickActionHandler, CounterQuickActionHandler>();
        services.AddScoped<IQuickActionHandler, AbstinenceQuickActionHandler>();
        services.AddScoped<IQuickActionHandler>(_ => new OpenScreenQuickActionHandler(Core.Enums.HabitType.Journal));
        services.AddScoped<IQuickActionHandler>(_ => new OpenScreenQuickActionHandler(Core.Enums.HabitType.State));
        services.AddScoped<QuickActionHandlerSelector>();

        services.AddTransient<ChartBuilderViewModel>();
        services.AddTransient<ChartsWorkspaceViewModel>();

        services.AddSingleton<ISaveFileDialog, WpfSaveFileDialog>();
        services.AddSingleton<IExportDialogLauncher, WpfExportDialogLauncher>();
        services.AddTransient<ExportDialogViewModel>();
        services.AddSingleton<IClipboard, WpfClipboard>();
        services.AddSingleton<IScreenColorPicker, ScreenColorPicker>();
        services.AddSingleton<IConfirmationDialog, MessageBoxConfirmationDialog>();
        services.AddTransient<DiagnosticInfoViewModel>();

        // Backup / Restore.
        services.AddScoped<IBackupLogService, BackupLogService>();
        services.AddSingleton<IBackupCipher, AesGcmBackupCipher>();
        services.AddSingleton<BackupArchiveWriter>();
        services.AddSingleton<BackupArchiveReader>();
        services.AddSingleton<IBackupSaveFileDialog, WpfBackupSaveFileDialog>();
        services.AddSingleton<IOpenFileDialog, WpfOpenFileDialog>();
        services.AddScoped<IBackupService>(sp => new BackupService(
            sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>(),
            sp.GetRequiredService<IDbKeyProvider>(),
            sp.GetRequiredService<IBackupStoragePaths>(),
            sp.GetRequiredService<IBackupCipher>(),
            sp.GetRequiredService<BackupArchiveWriter>(),
            sp.GetRequiredService<IBackupLogService>(),
            sp.GetRequiredService<IPluginManager>(),
            sp.GetRequiredService<IClock>(),
            appVersion: "3.10.0"));
        services.AddScoped<IRestoreService>(sp => new RestoreService(
            sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>(),
            sp.GetRequiredService<IDbKeyProvider>(),
            sp.GetRequiredService<IBackupStoragePaths>(),
            sp.GetRequiredService<IBackupCipher>(),
            sp.GetRequiredService<BackupArchiveReader>(),
            sp.GetRequiredService<IBackupService>(),
            sp.GetRequiredService<IPluginManager>(),
            sp.GetRequiredService<IClock>()));
#pragma warning disable CA1416
        services.AddScoped<IBackupScheduler>(sp => new BackupScheduler(
            sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>(),
            sp.GetRequiredService<IBackupService>(),
            sp.GetRequiredService<IBackupStoragePaths>(),
            sp.GetRequiredService<IClock>()));
#pragma warning restore CA1416
        services.AddSingleton<IAutoBackupKeyStore, WpfAutoBackupKeyStore>();
        services.AddTransient<BackupDialogViewModel>();
        services.AddTransient<RestoreDialogViewModel>();
        services.AddTransient<BackupHistoryViewModel>();
        services.AddTransient<BackupSettingsViewModel>();
        services.AddSingleton<IBackupDialogLauncher, WpfBackupDialogLauncher>();
        services.AddSingleton(new BackupSchedulerOptions());
        services.AddSingleton<BackupSchedulerHost>();
        services.AddScoped<IBackupSchedulerRunner, BackupSchedulerRunnerAdapter>();

        services.AddSingleton<IApplicationLifetime, WpfApplicationLifetime>();
        services.AddTransient<MainWindowViewModel>();

        // Main window.
        services.AddTransient<MainWindow>();

        // Bootstrap.
        services.AddSingleton<ApplicationInitializer>();
        services.AddTransient<DemoDataSeeder>();   // dev tool, runs only with --seed-demo

        return services;
    }
}
