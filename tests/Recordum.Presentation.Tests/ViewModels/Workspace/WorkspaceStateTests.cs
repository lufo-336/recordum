using FluentAssertions;
using Recordum.Presentation.ViewModels.Workspace;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Workspace;

public class WorkspaceStateTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var id = Guid.NewGuid();
        var state = new WorkspaceState
        {
            SidebarCollapsed = true,
            SidebarWidth = 240,
            ActiveTabId = id.ToString(),
            Tabs = [new WorkspaceTabState(id, WorkspaceTabKind.Habit)],
        };

        var restored = WorkspaceState.FromJson(state.ToJson());

        restored.Should().NotBeNull();
        restored!.SidebarCollapsed.Should().BeTrue();
        restored.SidebarWidth.Should().Be(240);
        restored.ActiveTabId.Should().Be(id.ToString());
        restored.Tabs.Should().ContainSingle();
        restored.Tabs[0].SubjectId.Should().Be(id);
        restored.Tabs[0].Kind.Should().Be(WorkspaceTabKind.Habit);
    }

    [Fact]
    public void FromJson_returns_null_for_null_empty_or_garbage()
    {
        WorkspaceState.FromJson(null).Should().BeNull();
        WorkspaceState.FromJson("").Should().BeNull();
        WorkspaceState.FromJson("{not json").Should().BeNull();
    }

    [Fact]
    public void Default_active_is_panoramica_sentinel()
    {
        WorkspaceState.PanoramicaId.Should().Be("panoramica");
    }
}
