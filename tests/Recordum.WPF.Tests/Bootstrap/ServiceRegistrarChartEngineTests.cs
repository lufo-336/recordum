using Recordum.WPF.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.ChartEngine;
using Recordum.Presentation.Bootstrap;
using Xunit;

namespace Recordum.WPF.Tests.Bootstrap;

public sealed class ServiceRegistrarChartEngineTests
{
    [Fact]
    public void Resolves_chart_engine_activator_and_query_service()
    {
        var services = new ServiceCollection()
            .AddRecordumServices(new RecordumOptions { DatabasePath = ":memory:", KeyFilePath = "k", SaltFilePath = "s", AttachmentsDirectory = "a", BackupsDirectory = "b", AutoBackupKeyPath = "ak", DataDirectory = "d", TempDirectory = "t" });
        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<IChartEngineActivator>());
        using var scope = sp.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IChartQueryService>());
    }
}
