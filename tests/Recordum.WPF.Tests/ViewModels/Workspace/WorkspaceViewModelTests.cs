using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Workspace;

public class WorkspaceViewModelTests
{
    [Fact]
    public void Starts_with_panoramica_active_and_no_tabs()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        sut.Tabs.Should().BeEmpty();
        sut.ActiveSubjectTab.Should().BeNull();
        sut.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public void Sidebar_defaults_expanded_with_a_sensible_width()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        sut.SidebarCollapsed.Should().BeFalse();
        sut.SidebarWidth.Should().BeGreaterThan(0);
    }
}
