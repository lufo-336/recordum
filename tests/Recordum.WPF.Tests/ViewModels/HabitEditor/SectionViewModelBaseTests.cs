using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels.HabitEditor;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class SectionViewModelBaseTests
{
    private sealed class TestSection : SectionViewModelBase
    {
        public override string Title => "Stub";
        public Task RunAsync(Func<Task> persist) => ApplyAsync(persist);
    }

    [Fact]
    public void Defaults_visible_false_collapsed()
    {
        var s = new TestSection();
        s.Title.Should().Be("Stub");
        s.IsVisible.Should().BeFalse();
        s.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_transitions_Saving_then_Saved_on_success()
    {
        var sut = new TestSection();
        var transitions = new List<EditorStatus>();
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SectionViewModelBase.Status))
                transitions.Add(sut.Status);
        };

        await sut.RunAsync(() => Task.CompletedTask);

        transitions.Should().Equal(EditorStatus.Saving, EditorStatus.Saved);
        sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_on_throw_records_message_and_sets_Error()
    {
        var sut = new TestSection();

        await sut.RunAsync(() => throw new InvalidOperationException("boom"));

        sut.Status.Should().Be(EditorStatus.Error);
        sut.ErrorMessage.Should().Be("boom");
    }

    [Fact]
    public async Task ApplyAsync_clears_prior_error_message_on_retry_success()
    {
        var sut = new TestSection();
        await sut.RunAsync(() => throw new InvalidOperationException("first"));
        sut.ErrorMessage.Should().Be("first");

        await sut.RunAsync(() => Task.CompletedTask);

        sut.Status.Should().Be(EditorStatus.Saved);
        sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void IsApplicable_default_returns_true_for_all_combinations()
    {
        var sut = new TestSection();
        sut.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeTrue();
        sut.IsApplicable(NodeType.Group, null).Should().BeTrue();
    }
}
