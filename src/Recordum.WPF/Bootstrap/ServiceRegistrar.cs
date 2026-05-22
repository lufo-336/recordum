using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Data;
using Recordum.Data.Interceptors;
using Recordum.Data.Security;
using Recordum.Data.Services;
using Recordum.Core.Services;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Events;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Plugins.Hosting;
using Recordum.Plugins.Management;
using Recordum.Plugins.Permissions;
using Recordum.Plugins.Routing;
using Recordum.WPF.Theming;
using Recordum.WPF.Services;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Quick;
using Recordum.WPF.ViewModels.Timer;
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
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPluginContext, AmbientPluginContext>();
        services.AddLogging();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IWebView2Availability, WebView2Availability>();

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
        services.AddScoped<IHabitConfigService, HabitConfigService>();
        services.AddScoped<IHabitTemplateService, HabitTemplateService>();
        services.AddScoped<ICompletionService, CompletionService>();
        services.AddScoped<IActiveTimerService, ActiveTimerService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IMetricDefinitionService, MetricDefinitionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IChartService, ChartService>();
        services.AddScoped<IExportTemplateService, ExportTemplateService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IStreakPauseService, StreakPauseService>();
        services.AddScoped<IPurgeService, PurgeService>();
        services.AddScoped<IHabitTreeQueryService, HabitTreeQueryService>();
        services.AddScoped<IKeyTransitionService, KeyTransitionService>();
        services.AddScoped<IAppSettingService, AppSettingService>();
        services.AddSingleton<IIntensityRampResolver, IntensityRampResolver>();
        services.AddScoped<IStreakAggregator, Data.Services.Aggregators.NaiveDailyStreakAggregator>();
        services.AddScoped<ITrendQueryService, TrendQueryService>();
        services.AddScoped<IHourlyDistributionQueryService, HourlyDistributionQueryService>();

        // Plugin host stack.
        services.AddSingleton<IPermissionBroker, AlwaysAllowPermissionBroker>();
        services.AddSingleton<IExtensionPointRegistry, ExtensionPointRegistry>();
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IApiRouter, ApiRouter>();
        services.AddSingleton<IPluginHost, InProcessPluginHost>();
        services.AddSingleton<IPluginManager, PluginManager>();

        // ViewModels.
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HabitEditorViewModel>();
        services.AddTransient<HabitTemplatePickerViewModel>();
        services.AddSingleton<IHabitTemplatePickerLauncher, WpfHabitTemplatePickerLauncher>();
        services.AddTransient<TimerStatusBarViewModel>();
        services.AddTransient<TimerRecoveryService>();
        services.AddTransient<HabitScreenViewModel>();

        services.AddScoped<IQuickActionHandler, ActionQuickActionHandler>();
        services.AddScoped<IQuickActionHandler, CounterQuickActionHandler>();
        services.AddScoped<IQuickActionHandler>(_ => new OpenScreenQuickActionHandler(Core.Enums.HabitType.Resistance));
        services.AddScoped<IQuickActionHandler>(_ => new OpenScreenQuickActionHandler(Core.Enums.HabitType.Journal));
        services.AddScoped<IQuickActionHandler>(_ => new OpenScreenQuickActionHandler(Core.Enums.HabitType.State));
        services.AddScoped<QuickActionHandlerSelector>();

        services.AddTransient<MainWindowViewModel>();

        // Main window.
        services.AddTransient<MainWindow>();

        // Bootstrap.
        services.AddSingleton<ApplicationInitializer>();

        return services;
    }
}
