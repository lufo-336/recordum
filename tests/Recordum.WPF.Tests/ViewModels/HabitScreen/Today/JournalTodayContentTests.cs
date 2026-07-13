using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Today;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Today;

public class JournalTodayContentTests
{
    private static (JournalTodayContent Sut, FakeHabitUiPrefsService Prefs, HabitNode Node) NewSut()
    {
        var prefs = new FakeHabitUiPrefsService();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
        return (new JournalTodayContent(prefs), prefs, node);
    }

    [Fact]
    public async Task Gesture_defaults_to_Diary_with_null_prefs_and_raises_the_diary_action()
    {
        var (sut, _, node) = NewSut();
        await sut.LoadAsync(node);
        sut.Gesture.Should().Be(JournalPrimaryGesture.Diary);
        sut.IsQuickNote.Should().BeFalse();
        TodayContentAction? raised = null;
        sut.ActionRequested += (_, a) => raised = a;

        sut.PrimaryCommand.Execute(null);

        raised.Should().Be(TodayContentAction.OpenDiario);
    }

    [Fact]
    public async Task QuickNote_pref_switches_the_gesture_and_action()
    {
        var (sut, prefs, node) = NewSut();
        prefs.Store[node.Id] = new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote };
        await sut.LoadAsync(node);
        sut.IsQuickNote.Should().BeTrue();
        TodayContentAction? raised = null;
        sut.ActionRequested += (_, a) => raised = a;

        sut.PrimaryCommand.Execute(null);

        raised.Should().Be(TodayContentAction.OpenQuickNote);
    }
}
