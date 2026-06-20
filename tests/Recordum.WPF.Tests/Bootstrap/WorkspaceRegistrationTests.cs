using System;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.ViewModels.HabitScreen;
using Recordum.WPF.ViewModels.Workspace;
using Xunit;

namespace Recordum.WPF.Tests.Bootstrap;

public sealed class WorkspaceRegistrationTests
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddRecordumServices(new RecordumOptions { DatabasePath = ":memory:", KeyFilePath = "k", SaltFilePath = "s", AttachmentsDirectory = "a", BackupsDirectory = "b", AutoBackupKeyPath = "ak" })
            .BuildServiceProvider();

    [Fact]
    public void Screen_factory_resolves_distinct_instances()
    {
        using var sp = BuildProvider();
        using var scope = sp.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<Func<HabitScreenViewModel>>();
        var a = factory();
        var b = factory();
        a.Should().NotBeNull();
        b.Should().NotBeSameAs(a);
    }

    [Fact]
    public void WorkspaceViewModel_resolves()
    {
        using var sp = BuildProvider();
        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceViewModel>().Should().NotBeNull();
    }
}
