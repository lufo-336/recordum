using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Data;
using Recordum.Data.Interceptors;
using Recordum.Data.Security;
using Recordum.Data.Services;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Events;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Plugins.Hosting;
using Recordum.Plugins.Management;
using Recordum.Plugins.Permissions;
using Recordum.Plugins.Routing;

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

        // Plugin host stack.
        services.AddSingleton<IPermissionBroker, AlwaysAllowPermissionBroker>();
        services.AddSingleton<IExtensionPointRegistry, ExtensionPointRegistry>();
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IApiRouter, ApiRouter>();
        services.AddSingleton<IPluginHost, InProcessPluginHost>();
        services.AddSingleton<IPluginManager, PluginManager>();

        // Bootstrap.
        services.AddSingleton<ApplicationInitializer>();

        return services;
    }
}
