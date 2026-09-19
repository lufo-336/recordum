using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

public class RegistrationSectionTests
{
    [Fact]
    public async Task Toggle_persists_repeatable_on_the_config()
    {
        var configs = new FakeHabitConfigService();
        var section = new RegistrationSectionViewModel(configs, new FakeHabitUiPrefsService());
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c" };
        await section.LoadAsync(node);

        section.Repeatable = true;
        await Task.Delay(50); // ApplyAsync is fire-and-forget in sections

        (await configs.GetAsync(node.Id))!.Repeatable.Should().BeTrue();
    }

    [Fact]
    public void Applicable_to_counter_and_journal_habits()
    {
        var section = new RegistrationSectionViewModel(new FakeHabitConfigService(), new FakeHabitUiPrefsService());
        section.IsApplicable(NodeType.Habit, HabitType.Counter).Should().BeTrue();
        section.IsApplicable(NodeType.Habit, HabitType.Journal).Should().BeTrue();
        section.IsApplicable(NodeType.Habit, HabitType.Action).Should().BeFalse();
        section.IsApplicable(NodeType.Group, null).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_does_not_persist_the_loaded_value_back(/* guards against the 4d01f9c data-wipe class of bug */)
    {
        var configs = new FakeHabitConfigService();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "c" };
        await configs.UpsertAsync(node.Id, c => c.Repeatable = true);
        var section = new RegistrationSectionViewModel(configs, new FakeHabitUiPrefsService());

        await section.LoadAsync(node);

        section.Repeatable.Should().BeTrue();
        await Task.Delay(50);
        // LoadAsync must not have fired a spurious persist while _loading was true — reloading
        // with a fresh section instance should still see the same value, not a clobbered default.
        (await configs.GetAsync(node.Id))!.Repeatable.Should().BeTrue();
    }

    [Fact]
    public async Task Journal_shows_the_gesture_and_hides_the_repeatable_check()
    {
        var section = new RegistrationSectionViewModel(new FakeHabitConfigService(), new FakeHabitUiPrefsService());
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };

        await section.LoadAsync(node);

        section.ShowJournalGesture.Should().BeTrue();
        section.ShowRepeatable.Should().BeFalse();
    }

    [Fact]
    public async Task Selecting_the_quick_note_gesture_persists_it_in_ui_prefs()
    {
        var prefs = new FakeHabitUiPrefsService();
        var section = new RegistrationSectionViewModel(new FakeHabitConfigService(), prefs);
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
        await section.LoadAsync(node);

        section.SelectedGesture = section.GestureOptions.Single(o => o.Gesture == JournalPrimaryGesture.QuickNote);
        await Task.Delay(50); // ApplyAsync e' fire-and-forget nelle sezioni

        (await prefs.GetAsync(node.Id)).JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
    }

    [Fact]
    public async Task LoadAsync_does_not_persist_the_loaded_gesture_back()
    {
        // Stessa guardia del test gemello sul repeatable: la selezione impostata durante il load
        // non deve rimbalzare in scrittura (classe di bug 4d01f9c / 1d96967).
        var prefs = new FakeHabitUiPrefsService();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };
        await prefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });
        var section = new RegistrationSectionViewModel(new FakeHabitConfigService(), prefs);

        await section.LoadAsync(node);

        section.SelectedGesture!.Gesture.Should().Be(JournalPrimaryGesture.QuickNote);
        await Task.Delay(50);
        (await prefs.GetAsync(node.Id)).JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
    }
}
