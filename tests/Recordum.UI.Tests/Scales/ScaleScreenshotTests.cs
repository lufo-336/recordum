using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels.Scales;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Home;
using Recordum.UI.Views.Rows;
using Xunit;
using static Recordum.UI.Tests.Scales.ScaleFixtures;

namespace Recordum.UI.Tests.Scales;

/// <summary>GIO-23 Task 7: i PNG dell'editor della scala e del cursore. Un PNG da solo non prova niente: ognuno ha la sua
/// asserzione strutturale (cosa e' disegnato, dove, di che misura).</summary>
public class ScaleScreenshotTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-scale-shots");

    private static void Inside(Control c, Window w)
    {
        c.IsEffectivelyVisible.Should().BeTrue();
        var r = new Rect(c.TranslatePoint(new Point(0, 0), w)!.Value, c.Bounds.Size);
        r.Width.Should().BeGreaterThan(0);
        r.Right.Should().BeLessThanOrEqualTo(w.Width + 0.5, $"{c.Name} non esce a destra");
    }

    [AvaloniaTheory]
    [InlineData("light", 900)]
    [InlineData("light", 360)]
    [InlineData("dark", 900)]
    [InlineData("dark", 360)]
    public void Scale_editor_umore(string theme, int width)
    {
        using var _ = TestCulture.Pin();
        var vm = new ScaleEditorViewModel();
        vm.Load(Umore(), null);
        var editor = new ScaleEditor { DataContext = vm };
        var w = Show(new ScrollViewer { Content = new Border { Padding = new Thickness(16), Child = editor } }, theme, width, 640);
        try
        {
            ScreenshotProbeTests.Capture(w, $"scale-editor-umore-{theme}-{width}");
            AllNamed<TextBox>(editor, "LabelBox").Should().HaveCount(5).And.OnlyContain(b => b.Bounds.Height > 20 && b.Bounds.Width > 60);
            var shown = VisibleNames(Named<ScaleMarksBar>(editor, "PreviewBar"));
            if (width < 480)
            {
                // A 360 i nomi fitti non si sfiorano: gli estremi restano, quelli che collidono si sfumano (come nella vista dello Stato).
                shown.Should().Contain("Malissimo").And.Contain("Benissimo").And.HaveCountLessThan(5);
                shown.Should().BeSubsetOf(["Malissimo", "Male", "Così così", "Bene", "Benissimo"]);
            }
            else shown.Should().Equal("Malissimo", "Male", "Così così", "Bene", "Benissimo");
            AllNamed<TextBlock>(editor, "TagText").Select(t => t.Text).Should().Equal("#malissimo", "#male", "#così-così", "#bene", "#benissimo");
            Inside(Named<TextBox>(editor, "NameBox"), w);
            Inside(Named<ScaleMarksBar>(editor, "PreviewBar"), w);
            editor.Classes.Contains("narrow").Should().Be(width < 480);
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("light", 900)]
    [InlineData("light", 360)]
    [InlineData("dark", 900)]
    [InlineData("dark", 360)]
    public void Scale_editor_medica_12(string theme, int width)
    {
        using var _ = TestCulture.Pin();
        var vm = new ScaleEditorViewModel();
        vm.Load(Medica12(), null);
        var editor = new ScaleEditor { DataContext = vm };
        var w = Show(new ScrollViewer { Content = new Border { Padding = new Thickness(16), Child = editor } }, theme, width, 1100);
        try
        {
            Click(w, Named<Button>(editor, "FillButton"));
            // Qualche nome, per vedere come la barra gestisce nomi fitti su una scala stretta.
            foreach (var (i, n) in new[] { (0, "Assente"), (3, "Lieve"), (4, "Moderato"), (5, "Intenso"), (11, "Estremo") }) vm.Rows[i].Label = n;
            Pump();
            ScreenshotProbeTests.Capture(w, $"scale-editor-medica-12-{theme}-{width}");
            AllNamed<TextBox>(editor, "LabelBox").Should().HaveCount(12);
            var bar = Named<ScaleMarksBar>(editor, "PreviewBar");
            Ticks(bar).Should().HaveCount(12);
            var shown = VisibleNames(bar);
            shown.Should().Contain("Assente").And.Contain("Estremo", "i due estremi hanno sempre il loro nome");
            if (width < 480) shown.Should().NotContain("Moderato", "su una scala stretta un nome che tocca il precedente si nasconde");
            else shown.Should().Equal("Assente", "Lieve", "Moderato", "Intenso", "Estremo");
            Inside(bar, w);
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("light", 900)]
    [InlineData("light", 360)]
    [InlineData("dark", 900)]
    [InlineData("dark", 360)]
    public async Task State_umore_37(string theme, int width)
    {
        using var _ = TestCulture.Pin();
        var (scope, content, w, today) = await OpenStateAsync(Host, Umore(), "Umore", theme, width, recorded: 37);
        try
        {
            ScreenshotProbeTests.Capture(w, $"state-umore-37-{theme}-{width}");
            Named<TextBlock>(today, "CurrentLabel").Text.Should().Be(content.CurrentLabel);
            content.CurrentLabel.Should().Be("37, fra Male e Così così");
            Named<TextBox>(today, "ValueBox").Text.Should().Be("37");
            Named<Slider>(today, "ValueSlider").Value.Should().Be(37);
            Ticks(Named<ScaleMarksBar>(today, "Marks")).Should().HaveCount(5);
            VisibleNames(Named<ScaleMarksBar>(today, "Marks")).Should().Contain("Malissimo").And.Contain("Benissimo");
            Inside(Named<Slider>(today, "ValueSlider"), w);
            Inside(Named<TextBox>(today, "ValueBox"), w);
            Inside(Named<Button>(today, "SaveButton"), w);
        }
        finally { w.Close(); scope.Dispose(); }
    }

    [AvaloniaTheory]
    [InlineData("light", 900)]
    [InlineData("light", 360)]
    [InlineData("dark", 900)]
    [InlineData("dark", 360)]
    public async Task State_nrs_0(string theme, int width)
    {
        using var _ = TestCulture.Pin();
        var (scope, content, w, today) = await OpenStateAsync(Host, Nrs(), "Dolore", theme, width, recorded: 0);
        try
        {
            ScreenshotProbeTests.Capture(w, $"state-nrs-0-{theme}-{width}");
            content.CurrentLabel.Should().Be("Nessun dolore");
            Named<TextBox>(today, "ValueBox").Text.Should().Be("0");
            Ticks(Named<ScaleMarksBar>(today, "Marks")).Should().HaveCount(11, "0-10: una tacca per intero");
            Named<Slider>(today, "ValueSlider").Value.Should().Be(0);
            Inside(Named<TextBox>(today, "ValueBox"), w);
        }
        finally { w.Close(); scope.Dispose(); }
    }

    [AvaloniaTheory]
    [InlineData("light", 900)]
    [InlineData("light", 360)]
    [InlineData("dark", 900)]
    [InlineData("dark", 360)]
    public void Home_state_umore(string theme, int width)
    {
        using var _ = TestCulture.Pin();
        var rows = ManifestHome.Rows(ManifestHome.Today).Select(r => r.Name == "Umore" ? r with { TodayStateValue = 37, StateScale = Umore() } : r).ToList();
        var vm = ManifestHome.Home(rows);
        var view = new HomeView { DataContext = vm };
        var w = Show(view, theme, width, 700);
        vm.LoadCommand.Execute(null);
        Pump();
        try
        {
            ScreenshotProbeTests.Capture(w, $"home-state-umore-{theme}-{width}");
            var row = view.GetVisualDescendants().OfType<HabitRow>().Single(r => r.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Umore"));
            row.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text)
                .Should().Contain("Male · 37", "la riga legge lo scaglione piu' vicino e il valore vero");
        }
        finally { w.Close(); }
    }
}
