using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.HomeRow;
using Recordum.Presentation.ViewModels.HabitBuilder;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitBuilder;
using Xunit;

namespace Recordum.UI.Tests.HabitBuilder;

/// <summary>L'anteprima viva del builder (F2b Task 3): segue la composizione e usa la striscia per tipo della Home (W2V-19).</summary>
public class BuilderPreviewPaneTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-hbp");

    private static async Task<(IServiceScope Scope, HabitBuilderViewModel Vm, Window Window, BuilderPreviewPane Pane)> OpenAsync()
    {
        var scope = Host.Provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<HabitBuilderViewModel>();
        await vm.InitializeAsync();
        var view = new HabitBuilderView { DataContext = vm };
        var w = new Window { Width = 1100, Height = 900, Content = view };
        w.Show();
        PrivateDbHost.Pump();
        return (scope, vm, w, view.GetVisualDescendants().OfType<BuilderPreviewPane>().Single());
    }

    private static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public async Task Composing_another_type_changes_the_preview_strip()
    {
        var o = await OpenAsync();
        try
        {
            var strip = Named<HeatmapStrip>(o.Pane, "HomeStrip");
            strip.Kind.Should().Be(StripStyle.Squares);   // Action

            o.Vm.Abstinence = true;
            PrivateDbHost.Pump();
            strip.Kind.Should().Be(StripStyle.Band);      // Resistance, la stessa forma della riga Home vera
            strip.Fractions.Should().Equal(o.Vm.Preview!.HomeRow.Fractions);

            // Un pixel: la banda è un unico segmento pieno nel colore d'accento, i quadrati hanno spazi fra loro.
            var rects = strip.GetVisualDescendants().OfType<Rectangle>().Count();
            rects.Should().BeLessThan(7, "Band non disegna una cella per giorno");
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    [AvaloniaFact]
    public async Task Name_color_and_icon_follow_the_header()
    {
        var o = await OpenAsync();
        try
        {
            o.Vm.Name = "Corsa";
            o.Vm.Color = "#FF0000";
            o.Vm.Icon = "Icon.Settings";
            PrivateDbHost.Pump();
            Named<TextBlock>(o.Pane, "NameText").Text.Should().Be("Corsa");
            var accent = (ISolidColorBrush)o.Pane.GetVisualDescendants().OfType<Rectangle>().First().Fill!;
            accent.Color.Should().Be(Color.Parse("#FF0000"));
            Named<Avalonia.Controls.Shapes.Path>(o.Pane, "IconPath").IsVisible.Should().BeTrue();
            Named<HeatmapStrip>(o.Pane, "HomeStrip").AccentColor.Should().Be("#FF0000");
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }

    [AvaloniaFact]
    public async Task A_counter_preview_shows_the_key_figure_like_Home()
    {
        var o = await OpenAsync();
        try
        {
            Named<Button>(o.Window, "AddQuantityButton").Command!.Execute(Recordum.Core.Enums.FieldNature.Quantity);
            PrivateDbHost.Pump();
            var key = Named<TextBlock>(o.Pane, "KeyFigureText");
            o.Vm.Preview!.HomeRow.KeyFigure.Should().NotBeNullOrEmpty();
            key.IsVisible.Should().BeTrue();
            key.Text.Should().Be(o.Vm.Preview.HomeRow.KeyFigure);
            Named<HeatmapStrip>(o.Pane, "HomeStrip").Kind.Should().Be(StripStyle.Bars);
        }
        finally { o.Window.Close(); o.Scope.Dispose(); }
    }
}
