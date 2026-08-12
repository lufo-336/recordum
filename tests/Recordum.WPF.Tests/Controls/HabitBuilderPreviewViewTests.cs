using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.Controls;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitBuilder;
using Recordum.WPF.Views.HabitBuilder;
using Xunit;

namespace Recordum.WPF.Tests.Controls;

/// <summary>Onda 2.5 T9 — the builder's live preview against the REAL compiled
/// <c>HabitBuilderView.xaml</c>. The claim under test is the spec's hard constraint: no WebView2 in
/// the preview, ever. A VM-level assertion could not see it (C1's lesson: the visual tree is built
/// from templates, not from whether a binding happens to be null), so this walks the actual tree.</summary>
public sealed class HabitBuilderPreviewViewTests
{
    private static HabitBuilderViewModel NewVm()
    {
        var settings = new FakeAppSettingService();
        return new(new FakeComposedHabitService(), new FakeHabitTemplateService(), new FakeReferenceData(),
            new FakeClock(), new RecentColorStore(settings), new FavoriteColorStore(settings),
            new FakeScreenColorPicker());
    }

    [WpfTheory]
    [InlineData(JournalPrimaryGesture.Diary)]
    [InlineData(JournalPrimaryGesture.QuickNote)]
    public void The_preview_never_materializes_the_markdown_editor(JournalPrimaryGesture gesture)
    {
        TestApp.EnsureLoaded();
        var vm = NewVm();
        vm.Name = "Diario";
        vm.AddFieldCommand.Execute(FieldNature.Text); // -> Journal
        vm.JournalGesture = gesture;
        vm.DerivedType.Should().Be(HabitType.Journal);

        var view = new HabitBuilderView { DataContext = vm };
        var window = new Window { Content = view, Width = 1100, Height = 700 };
        window.Show();
        try
        {
            window.UpdateLayout();

            FindDescendants<MarkdownEditorControl>(view).Should().BeEmpty(
                "the builder preview draws a static stand-in of the diary editor; hosting the real one " +
                "would start a WebView2 process just to show a sample (spec §3, constraint 1)");
            FindDescendants<BuilderPreviewPane>(view).Should().ContainSingle();
        }
        finally { window.Close(); }
    }

    [WpfFact]
    public void The_preview_pane_is_inert()
    {
        // A preview you can click is a preview that can record something. The whole pane is
        // hit-test-invisible, which is also what keeps the real "Oggi" view-models' commands — wired
        // to inert sample services — permanently out of reach.
        TestApp.EnsureLoaded();
        var vm = NewVm();
        vm.AddFieldCommand.Execute(FieldNature.Quantity);

        var view = new HabitBuilderView { DataContext = vm };
        var window = new Window { Content = view, Width = 1100, Height = 700 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var pane = FindDescendants<BuilderPreviewPane>(view).Single();
            FindDescendants<System.Windows.Controls.ScrollViewer>(pane).First()
                .IsHitTestVisible.Should().BeFalse();
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
