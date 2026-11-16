using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;
using Recordum.Presentation.ViewModels.Home;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Xunit;

namespace Recordum.UI.Tests.Controls;

/// <summary>GIO-20: la riga d'anteprima di creatore, selettore dei modelli e Configura è la riga della Home, nella
/// versione scelta in Impostazioni › Aspetto.</summary>
public class RowPreviewTests
{
    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Nella colonna stretta del creatore sette giorni mangiavano il nome ("Monitor …"): i giorni seguono la
    /// larghezza con la stessa regola della Home.</summary>
    [AvaloniaFact]
    public void A_narrow_preview_shows_fewer_days_and_keeps_the_name()
    {
        using var culture = TestCulture.Pin();
        var preview = new RowPreview
        {
            Model = SamplePreviewFactory.HomeRowFor(HabitType.Action, KeyFigureKind.None, HomeStrip.Days14, "Monitor del sonno", "#2F7FC1", null),
        };
        var w = new Window { Width = 320, Height = 120, Content = preview };
        w.Show();
        Pump();
        preview.GetVisualDescendants().OfType<HeatmapStrip>().Single()
            .GetVisualDescendants().OfType<TextBlock>().Should().HaveCount(HomeRowsMetrics.VisibleDays(HomeRows.Comfortable, 320));
        var name = preview.FindControl<TextBlock>("NameText")!;
        name.TextLayout.TextLines.Should().OnlyContain(l => !l.HasCollapsed, "il nome sta intero");
        w.Close();
    }

    /// <summary>In Impostazioni › Aspetto l'intestazione è solo da guardare: senza comando i giorni non devono prendere
    /// il fondo dei pulsanti spenti.</summary>
    [AvaloniaFact]
    public void A_header_without_a_command_has_no_disabled_background()
    {
        using var culture = TestCulture.Pin();
        var header = new Recordum.UI.Views.Home.DayHeaderView
        {
            Cells = [new DayHeaderCell("lun", "l", "lunedì", 1, false, false), new DayHeaderCell("oggi", "m", "martedì", 0, true, true)],
        };
        var w = new Window { Width = 200, Height = 80, Content = header };
        w.Show();
        Pump();
        var day = header.GetVisualDescendants().OfType<Button>().First();
        var painted = day.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First();
        (painted.Background as Avalonia.Media.ISolidColorBrush)?.Color.A.Should().Be(0, "nessun fondo sotto un giorno non scelto");
        w.Close();
    }

    [AvaloniaFact]
    public void A_sample_row_is_drawn_like_the_home_in_the_chosen_version()
    {
        using var culture = TestCulture.Pin();
        var preview = new RowPreview
        {
            Model = SamplePreviewFactory.HomeRowFor(HabitType.Action, KeyFigureKind.None, HomeStrip.Days14, "Tè", "#2F7FC1", null),
        };
        var w = new Window { Width = 420, Height = 120, Content = preview };
        try
        {
            w.Show();
            Pump();
            var strip = preview.GetVisualDescendants().OfType<HeatmapStrip>().Single();
            strip.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)
                .Should().HaveCount(7).And.OnlyContain(t => t == "✓" || t == "·", "le spunte della Home, non i quadretti di prima");
            preview.FindControl<Grid>("HomeRowGrid")!.Bounds.Height.Should().Be(36, "alta come una riga Larga");

            HomeRowsAppStyle.Apply(Application.Current!, HomeRows.Marks);
            Pump();
            strip.GetVisualDescendants().OfType<TextBlock>().Should().BeEmpty("in Segni la striscia non scrive");
            strip.GetVisualDescendants().OfType<Border>().Count(b => b.Bounds.Width == 14 && b.Bounds.Height == 14).Should().Be(7);
            preview.FindControl<Grid>("HomeRowGrid")!.Bounds.Height.Should().Be(30);
        }
        finally
        {
            HomeRowsAppStyle.Apply(Application.Current!, HomeRows.Comfortable);
            w.Close();
        }
    }
}
