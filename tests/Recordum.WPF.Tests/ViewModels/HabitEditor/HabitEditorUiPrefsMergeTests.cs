using System.Linq;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitEditor;

/// <summary>Regression coverage for finding C2 (Onda 2.4 whole-branch review): before the fix, both
/// <see cref="RegistrationSectionViewModel"/> and <see cref="HomeRowSectionViewModel"/> cached the
/// whole <see cref="HabitUiPrefs"/> record at <c>LoadAsync</c> and later wrote back
/// <c>_current with { OneField = ... }</c> — so on a Journal habit (where
/// <c>HabitEditorViewModel.LoadAsync</c> loads both sections), whichever section wrote second silently
/// reverted the other's field. The fix routes both sections' writes through
/// <c>IHabitUiPrefsService.UpdateAsync</c>, which re-reads immediately before writing instead
/// of trusting a stale snapshot.</summary>
public class HabitEditorUiPrefsMergeTests
{
    private static HabitNode JournalNode(Guid id) =>
        new() { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j" };

    [Fact]
    public async Task Key_figure_survives_a_later_gesture_write_from_a_sibling_section()
    {
        var prefs = new FakeHabitUiPrefsService();
        var node = JournalNode(Guid.NewGuid());
        var registration = new RegistrationSectionViewModel(new FakeHabitConfigService(), prefs);
        var homeRow = new HomeRowSectionViewModel(prefs);

        // Both sections load from the same prefs snapshot, mirroring HabitEditorViewModel.LoadAsync
        // loading Registration (:239) then HomeRow (:244) sequentially for the same node.
        await registration.LoadAsync(node);
        await homeRow.LoadAsync(node);

        // JournalToday is AllowedKeyFigures(Journal)[0] — the type default HomeRow.LoadAsync already
        // selects for an empty-prefs habit — so it must NOT be the value picked here: CommunityToolkit's
        // generated setter no-ops (and never calls OnSelectedKeyFigureChanged) when the assigned value
        // is equal, by KeyFigureOption's record equality, to the current one. JournalEntriesWeek is a
        // genuine change and actually exercises the write this test is about.
        homeRow.SelectedKeyFigure = homeRow.KeyFigureOptions.First(o => o.Kind == KeyFigureKind.JournalEntriesWeek);
        await Task.Delay(20); // ApplyAsync is fire-and-forget in sections

        registration.SelectedGesture = registration.GestureOptions.Single(o => o.Gesture == JournalPrimaryGesture.QuickNote);
        await Task.Delay(20);

        var saved = await prefs.GetAsync(node.Id);
        saved.KeyFigure.Should().Be(HomeRowPresentation.IdFor(KeyFigureKind.JournalEntriesWeek),
            "the key-figure write from HomeRow must survive Registration's later gesture write");
        saved.JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
    }

    [Fact]
    public async Task Gesture_survives_a_later_key_figure_write_from_a_sibling_section()
    {
        // Mirror of the test above with the write order reversed — the finding calls out both orders.
        var prefs = new FakeHabitUiPrefsService();
        var node = JournalNode(Guid.NewGuid());
        var registration = new RegistrationSectionViewModel(new FakeHabitConfigService(), prefs);
        var homeRow = new HomeRowSectionViewModel(prefs);

        await registration.LoadAsync(node);
        await homeRow.LoadAsync(node);

        registration.SelectedGesture = registration.GestureOptions.Single(o => o.Gesture == JournalPrimaryGesture.QuickNote);
        await Task.Delay(20);

        homeRow.SelectedKeyFigure = homeRow.KeyFigureOptions.First(o => o.Kind == KeyFigureKind.JournalEntriesWeek);
        await Task.Delay(20);

        var saved = await prefs.GetAsync(node.Id);
        saved.JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote,
            "the gesture write from Registration must survive HomeRow's later key-figure write");
        saved.KeyFigure.Should().Be(HomeRowPresentation.IdFor(KeyFigureKind.JournalEntriesWeek));
    }

    [Fact]
    public async Task Open_columns_write_from_HabitScreenViewModel_survives_alongside_a_section_write()
    {
        // The third read-modify-write caller of ui_prefs_json: HabitScreenViewModel.PersistSubjectColumnsAsync
        // now also routes through UpdateAsync instead of its own hand-rolled GetAsync+SetAsync re-read.
        var prefs = new FakeHabitUiPrefsService();
        var id = Guid.NewGuid();
        await prefs.SetAsync(id, new HabitUiPrefs { OpenColumns = new[] { "Diario" } });

        var registration = new RegistrationSectionViewModel(new FakeHabitConfigService(), prefs);
        await registration.LoadAsync(JournalNode(id));
        registration.SelectedGesture = registration.GestureOptions.Single(o => o.Gesture == JournalPrimaryGesture.QuickNote);
        await Task.Delay(20);

        // Simulate a concurrent column-open write via the same merge API HabitScreenViewModel uses.
        await prefs.UpdateAsync(id, p => p with { OpenColumns = new[] { "Diario", "Cronologia" } });

        var saved = await prefs.GetAsync(id);
        saved.JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
        saved.OpenColumns.Should().Equal("Diario", "Cronologia");
    }
}
