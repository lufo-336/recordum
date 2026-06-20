using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Workspace;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Workspace;

public class SubjectTabViewModelTests
{
    private static SubjectTabViewModel NewTab(bool isPreview)
    {
        var (screen, _) = HabitScreenViewModelFactory.Create();
        return new SubjectTabViewModel(Guid.NewGuid(), WorkspaceTabKind.Habit, "Corsa", screen,
            new Recordum.WPF.Tests.Fixtures.TrackingDisposable(), isPreview);
    }

    [Fact]
    public void Carries_identity_and_screen()
    {
        var tab = NewTab(isPreview: true);
        tab.Title.Should().Be("Corsa");
        tab.Kind.Should().Be(WorkspaceTabKind.Habit);
        tab.Screen.Should().NotBeNull();
        tab.IsPreview.Should().BeTrue();
    }

    [Fact]
    public void Pin_clears_preview_and_raises_change()
    {
        var tab = NewTab(isPreview: true);
        var changed = new List<string?>();
        tab.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        tab.Pin();

        tab.IsPreview.Should().BeFalse();
        changed.Should().Contain(nameof(SubjectTabViewModel.IsPreview));
    }

    [Fact]
    public void Pin_is_idempotent_on_already_pinned()
    {
        var tab = NewTab(isPreview: false);
        tab.Pin();
        tab.IsPreview.Should().BeFalse();
    }

    [Fact]
    public void Dispose_disposes_the_owned_scope()
    {
        var (screen, _) = HabitScreenViewModelFactory.Create();
        var scope = new Recordum.WPF.Tests.Fixtures.TrackingDisposable();
        var tab = new SubjectTabViewModel(Guid.NewGuid(), WorkspaceTabKind.Habit, "x", screen, scope, isPreview: false);

        tab.Dispose();

        scope.Disposed.Should().BeTrue();
    }
}
