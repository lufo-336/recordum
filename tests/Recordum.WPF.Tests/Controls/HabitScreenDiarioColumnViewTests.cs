using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Controls;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.Tests.ViewModels.HabitScreen;
using Recordum.WPF.Views.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.Controls;

/// <summary>Regression coverage for finding C1 (Onda 2.4 whole-branch review): a
/// <c>ContentControl</c> with an explicit <c>ContentTemplate</c> applies that template even when its
/// <c>Content</c> is null — <c>ContentPresenter.ChooseTemplate()</c> has no null-content check. The
/// VM-level tests in <see cref="HabitScreenViewModelTests"/> (e.g.
/// <c>Quick_note_journal_hosts_the_composer_and_never_the_markdown_editor</c>) only assert that
/// <c>QuickAddForDiario</c> is null; they cannot see whether WPF still walked
/// <see cref="MarkdownEditorControl"/> into the visual tree despite that, which is exactly the gap C1
/// found. This loads the REAL compiled <c>HabitScreen.xaml</c> against a real (fake-backed)
/// <c>HabitScreenViewModel</c> and inspects the actual visual tree.
///
/// <para>What this proves: for a quick-note journal habit, no <see cref="MarkdownEditorControl"/>
/// instance exists anywhere under the Diario column (<c>HabitScreen.DiarioColumn</c>, the
/// <c>x:Name</c>'d Border the fix's Visibility guards live in) — i.e. that column's Markdown/WebView2
/// host is never materialized, which is the mechanism the fix (a <c>Visibility</c> guard alongside the
/// null <c>Content</c>) targets. The search is scoped to that one column rather than the whole
/// <see cref="HabitScreen"/> deliberately: the screen also hosts an unrelated
/// <c>MarkdownEditorControl</c> in its Note tab (<c>NotesTabView</c>, gated by a different column
/// flag, <c>ShowNote</c>) whose default open/closed state is not this test's concern — asserting over
/// the whole tree would make the test depend on that unrelated column's default visibility. What this
/// does NOT prove: that a real <c>WebView2</c> control never navigates in production —
/// <c>FakeWebView2Availability</c>'s <c>Environment</c> is always null, so even
/// <see cref="MarkdownEditorControl"/>'s own <c>TryInitAsync</c> would fall back to its plain-TextBox
/// path rather than touch a real WebView2 runtime; the claim under test is purely "the control is/isn't
/// in the Diario column's tree at all".</para></summary>
public sealed class HabitScreenDiarioColumnViewTests
{
    [WpfFact]
    public async Task Quick_note_journal_never_materializes_the_markdown_editor_in_the_visual_tree()
    {
        TestApp.EnsureLoaded();
        var sut = HabitScreenViewModelTests.CreateSut(
            out var nodes, out _, out _, out _, out _, out _, out _, out _, out var uiPrefs);
        var node = HabitScreenViewModelTests.AddJournal(nodes, "Food diary");
        await uiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });
        await sut.LoadAsync(node.Id);
        sut.QuickAddForDiario.Should().BeNull();       // VM invariant, already covered elsewhere
        sut.QuickEntryForDiario.Should().NotBeNull();

        var view = new HabitScreen { DataContext = sut };
        var window = new Window { Content = view, Width = 1400, Height = 900 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var diario = view.DiarioColumn;
            diario.Visibility.Should().Be(Visibility.Visible, "a fresh journal defaults to Diario open (existing VM coverage)");

            FindDescendants<MarkdownEditorControl>(diario).Should().BeEmpty(
                "a quick-note journal's Diario column must host only the composer — the Markdown/WebView2 " +
                "host must never be walked into the visual tree, not merely receive a null Content (C1)");
            FindDescendants<JournalQuickEntryView>(diario).Should().ContainSingle(
                "the composer is the one editor surface that should be present for a quick-note habit");
        }
        finally { window.Close(); }
    }

    [WpfFact]
    public async Task Diary_gesture_journal_still_materializes_the_markdown_editor()
    {
        // Symmetric check: the fix must not accidentally suppress the Markdown editor for the
        // "Diary" gesture (the pre-existing, still-supported path).
        TestApp.EnsureLoaded();
        var sut = HabitScreenViewModelTests.CreateSut(
            out var nodes, out _, out _, out _, out _, out _, out _, out _, out var uiPrefs);
        var node = HabitScreenViewModelTests.AddJournal(nodes, "Daily journal");
        await uiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.Diary });
        await sut.LoadAsync(node.Id);
        sut.QuickAddForDiario.Should().NotBeNull();
        sut.QuickEntryForDiario.Should().BeNull();

        var view = new HabitScreen { DataContext = sut };
        var window = new Window { Content = view, Width = 1400, Height = 900 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var diario = view.DiarioColumn;

            FindDescendants<MarkdownEditorControl>(diario).Should().NotBeEmpty(
                "a Diary-gesture journal must still show the Markdown editor in Diario");
            FindDescendants<JournalQuickEntryView>(diario).Should().BeEmpty(
                "the composer host must stay collapsed and unmaterialized for a Diary-gesture journal");
        }
        finally { window.Close(); }
    }

    [WpfFact]
    public async Task Quick_note_journal_materializes_no_markdown_editor_anywhere_in_the_screen()
    {
        // W24-Q1 (open question left by the Onda 2.4 review): the sibling test above scopes its search
        // to the Diario column on purpose, so it cannot see the SECOND MarkdownEditorControl the screen
        // hosts in its Note tab. If that one is built even while the Note column is closed, then "no
        // WebView2 for a quick-note journal" was never true — the editor just moved out of view. This
        // widens the same search to the whole screen and settles it either way.
        TestApp.EnsureLoaded();
        var sut = HabitScreenViewModelTests.CreateSut(
            out var nodes, out _, out _, out _, out _, out _, out _, out _, out var uiPrefs);
        var node = HabitScreenViewModelTests.AddJournal(nodes, "Food diary");
        await uiPrefs.SetAsync(node.Id, new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote });
        await sut.LoadAsync(node.Id);
        sut.ShowNote.Should().BeFalse("this test is about the Note column being closed");

        var view = new HabitScreen { DataContext = sut };
        var window = new Window { Content = view, Width = 1400, Height = 900 };
        window.Show();
        try
        {
            window.UpdateLayout();

            FindDescendants<MarkdownEditorControl>(view).Should().BeEmpty(
                "with the Note column closed and a quick-note journal open, no Markdown/WebView2 host " +
                "should exist anywhere in the screen (W24-Q1)");
        }
        finally { window.Close(); }
    }

    private static List<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var found = new List<T>();
        void Walk(DependencyObject node)
        {
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is T match) found.Add(match);
                Walk(child);
            }
        }
        Walk(root);
        return found;
    }
}
