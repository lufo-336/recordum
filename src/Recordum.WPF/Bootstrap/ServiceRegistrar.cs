using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Data;
using Recordum.Data.Interceptors;
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

        // Core abstractions.
        services.AddSingleton(options);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPluginContext, AmbientPluginContext>();

        // Data layer.
        services.AddSingleton<TimestampInterceptor>();
        services.AddDbContextFactory<RecordumDbContext>((sp, builder) =>
        {
            var interceptor = sp.GetRequiredService<TimestampInterceptor>();
            builder
                .UseSqlite(options.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(interceptor);
        });

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
