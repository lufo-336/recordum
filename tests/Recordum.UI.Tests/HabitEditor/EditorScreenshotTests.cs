using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitEditor;
using Xunit;
using static Recordum.UI.Tests.HabitEditor.EditorPaneTests;

namespace Recordum.UI.Tests.HabitEditor;

/// <summary>F2b-9.3: Configura con tutte le sezioni applicabili aperte, in chiaro e in scuro (DB privato di EditorPaneTests).</summary>
public class EditorScreenshotTests(ITestOutputHelper output)
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Configura_with_the_sections_open_renders_in_both_themes(string variant)
    {
        using var culture = TestCulture.Pin();
        var id = await CreateAsync($"Counter-{Guid.NewGuid():N}", NodeType.Habit, HabitType.Counter);
        var o = await OpenAsync(id);
        try
        {
            o.Window.Width = 1400;
            o.Window.Height = 1100;
            o.Window.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            var pane = await OpenConfiguraAsync(o);
            var editor = o.Vm.Workspace.ActiveSubjectTab!.Screen.Editor;
            var visible = editor.Sections.Where(s => s.IsVisible).ToList();
            Named<Avalonia.Controls.Primitives.ToggleButton>(pane, "AdvancedToggle").IsChecked = true;
            foreach (var s in visible) s.IsExpanded = true;
            PrivateDbHost.Pump();
            await Task.Delay(100);
            PrivateDbHost.Pump();
            output.WriteLine(ScreenshotProbeTests.Capture(o.Window, $"configura-{variant.ToLowerInvariant()}"));

            o.Window.ActualThemeVariant.Should().Be(variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);
            visible.Count.Should().BeGreaterThanOrEqualTo(8, "un contatore applica la maggior parte delle sezioni");
            var stack = Named<StackPanel>(pane, "SectionsStack");
            var rows = stack.GetVisualDescendants().OfType<ConfigRow>().Where(r => r.Part == SectionPart.Main && r.IsEffectivelyVisible).ToList();
            rows.Should().HaveCountGreaterThanOrEqualTo(11, "dodici sezioni, la sezione Scale non si disegna per un contatore");
            rows.Should().OnlyContain(r => r.IsOpen && r.Bounds.Height > 40, "ogni sezione e' aperta e disegnata");
            rows.Should().OnlyContain(r => r.Bounds.Width >= stack.Bounds.Width - 1, "ogni sezione occupa la larghezza della colonna, non quella del suo contenuto");
        }
        finally { o.Window.Close(); }
    }
}
