using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Workspace;
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

    [Fact]
    public async Task Single_open_creates_one_preview_tab_and_activates_it()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        var id = Guid.NewGuid();

        await sut.OpenSubjectAsync(id, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");

        sut.Tabs.Should().ContainSingle();
        sut.Tabs[0].IsPreview.Should().BeTrue();
        sut.Tabs[0].SubjectId.Should().Be(id);
        sut.ActiveSubjectTab.Should().Be(sut.Tabs[0]);
        sut.IsPanoramicaActive.Should().BeFalse();
    }

    [Fact]
    public async Task Second_single_open_replaces_the_preview_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");
        var second = Guid.NewGuid();

        await sut.OpenSubjectAsync(second, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Peso");

        sut.Tabs.Should().ContainSingle();
        sut.Tabs[0].SubjectId.Should().Be(second);
        sut.Tabs[0].IsPreview.Should().BeTrue();
    }

    [Fact]
    public async Task Opening_an_already_open_subject_activates_it_without_duplicating()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        var id = Guid.NewGuid();
        await sut.OpenSubjectAsync(id, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Peso", pin: true);

        await sut.OpenSubjectAsync(id, Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa");

        sut.Tabs.Where(t => t.SubjectId == id).Should().ContainSingle();
        sut.ActiveSubjectTab!.SubjectId.Should().Be(id);
    }

    [Fact]
    public async Task Open_with_pin_true_creates_a_pinned_tab()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();

        await sut.OpenSubjectAsync(Guid.NewGuid(), Recordum.WPF.ViewModels.Workspace.WorkspaceTabKind.Habit, "Corsa", pin: true);

        sut.Tabs[0].IsPreview.Should().BeFalse();
    }

    [Fact]
    public async Task PinActiveTab_promotes_the_preview_and_clears_preview_slot()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa");

        sut.PinActiveTab();

        sut.ActiveSubjectTab!.IsPreview.Should().BeFalse();
        // A later single open now ADDS rather than replaces (preview slot is empty).
        var n = sut.Tabs.Count;
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Peso");
        sut.Tabs.Should().HaveCount(n + 1);
    }

    [Fact]
    public async Task CloseTab_removes_it_and_closing_last_returns_to_panoramica()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", pin: true);
        var tab = sut.Tabs[0];

        sut.CloseTab(tab);

        sut.Tabs.Should().BeEmpty();
        sut.IsPanoramicaActive.Should().BeTrue();
    }

    [Fact]
    public async Task CloseTab_activates_a_neighbour_when_others_remain()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);
        var first = sut.Tabs[0];
        sut.ActivateTab(first);

        sut.CloseTab(first);

        sut.Tabs.Should().ContainSingle();
        sut.ActiveSubjectTab.Should().Be(sut.Tabs[0]);
    }

    [Fact]
    public async Task MoveTab_reorders()
    {
        var (sut, _) = WorkspaceViewModelFactory.Create();
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "A", pin: true);
        await sut.OpenSubjectAsync(Guid.NewGuid(), WorkspaceTabKind.Habit, "B", pin: true);
        var a = sut.Tabs[0];

        sut.MoveTab(a, 1);

        sut.Tabs[1].Should().Be(a);
    }
}
