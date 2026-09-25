using Recordum.WPF.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Data.Services.Queries;
using Recordum.Plugins.ExportEngine;
using Recordum.Presentation.Bootstrap;
using Xunit;

namespace Recordum.WPF.Tests.Bootstrap;

public sealed class ServiceRegistrarExportEngineTests
{
    [Fact]
    public void Resolves_export_engine_activator_and_query_and_job_services()
    {
        var services = new ServiceCollection()
            .AddRecordumServices(new RecordumOptions { DatabasePath = ":memory:", KeyFilePath = "k", SaltFilePath = "s", AttachmentsDirectory = "a", BackupsDirectory = "b", AutoBackupKeyPath = "ak", DataDirectory = "d", TempDirectory = "t" });
        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<IExportEngineActivator>());
        using var scope = sp.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IExportQueryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IExportJobService>());
    }
}
