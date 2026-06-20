using System;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Recordum.WPF.Bootstrap;
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
    public void Subject_screen_factory_creates_distinct_screens_in_their_own_scopes()
    {
        using var sp = BuildProvider();
        var factory = sp.GetRequiredService<ISubjectScreenFactory>();

        var a = factory.Create();
        var b = factory.Create();

        a.Screen.Should().NotBeNull();
        b.Screen.Should().NotBeSameAs(a.Screen);
        a.Scope.Should().NotBeNull();
        b.Scope.Should().NotBeSameAs(a.Scope);
        a.Scope.Dispose();
        b.Scope.Dispose();
    }

    [Fact]
    public void WorkspaceViewModel_resolves()
    {
        using var sp = BuildProvider();
        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceViewModel>().Should().NotBeNull();
    }
}
