using System.Reflection;
using System.Runtime.Versioning;
using FluentAssertions;
using Xunit;

namespace Recordum.UI.Tests.Bootstrap;

/// <summary>Le librerie condivise fra computer e telefono restano net10.0: un -windows o un -android in uno di questi
/// assembly lo rende inutilizzabile dall'altra testa. Le teste (Recordum.Desktop, Recordum.Android) non sono qui.</summary>
public class PlatformNeutralityTests
{
    public static TheoryData<string> SharedAssemblies() => new()
    {
        typeof(Recordum.Core.Abstractions.IClock).Assembly.GetName().Name!,
        typeof(Recordum.Data.RecordumDbContext).Assembly.GetName().Name!,
        typeof(Recordum.Presentation.Bootstrap.RecordumOptions).Assembly.GetName().Name!,
        typeof(Recordum.Plugins.Contracts.Hosting.IPluginManager).Assembly.GetName().Name!,
        typeof(Recordum.Plugins.ChartEngine.ChartEngineActivator).Assembly.GetName().Name!,
        typeof(Recordum.Plugins.ExportEngine.ExportEngineManifest).Assembly.GetName().Name!,
        typeof(Recordum.UI.App).Assembly.GetName().Name!,
        "Recordum.Plugins",
    };

    [Theory]
    [MemberData(nameof(SharedAssemblies))]
    public void Shared_assemblies_have_no_target_platform(string name)
    {
        var assembly = Assembly.Load(name);
        assembly.GetCustomAttribute<TargetPlatformAttribute>().Should().BeNull($"{name} e' condiviso dalle due teste");
    }

    [Fact]
    public void The_ui_library_is_not_an_executable()
    {
        // La testa e' Recordum.Desktop: una libreria non ha punto d'ingresso.
        typeof(Recordum.UI.App).Assembly.EntryPoint.Should().BeNull();
    }
}
