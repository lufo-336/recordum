using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

public class HabitScreenViewTests
{
    // Due colori lontani fra loro e dal default dell'app (#3B82F6 chiaro, #60A5FA scuro).
    private const string Red = "#E11D48";
    private const string Amber = "#F59E0B";

    private static async Task<Guid> CreateHabitAsync(string name, string color, NodeType type = NodeType.Habit)
    {
        using var scope = ShellFixture.Shared.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = type, HabitType = type == NodeType.Habit ? HabitType.Action : null, Name = name, Color = color,
        });
    }

    private static async Task<(HabitScreenViewModel Vm, HabitScreenView View, Window Window)> OpenAsync(Guid id)
    {
        var vm = ShellFixture.Shared.Services.CreateScope().ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 800, Height = 600, Content = view };
        w.Show();
        Pump();
        return (vm, view, w);
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Un quadrato che usa Brush.Accent, in basso a destra, e il suo centro letto dal frame.</summary>
    private static Border AddAccentProbe(HabitScreenView view)
    {
        var probe = new Border { Width = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom };
        probe.Bind(Border.BackgroundProperty, probe.GetResourceObservable("Brush.Accent"));
        ((Grid)view.Content!).Children.Add(probe);
        return probe;
    }

    private static Color Pixel(Window w, Control c, double dx = 0, double dy = 0)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2 + dx, c.Bounds.Height / 2 + dy), w)!.Value;
        using var frame = w.CaptureRenderedFrame()!;
        var buffer = new byte[4];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try { frame.CopyPixels(new PixelRect((int)p.X, (int)p.Y, 1, 1), handle.AddrOfPinnedObject(), 4, 4); }
        finally { handle.Free(); }
        return frame.Format == Avalonia.Platform.PixelFormats.Bgra8888
            ? Color.FromRgb(buffer[2], buffer[1], buffer[0]) : Color.FromRgb(buffer[0], buffer[1], buffer[2]);
    }

    private static bool Near(Color a, string hex)
    {
        var b = Color.Parse(hex);
        return Math.Abs(a.R - b.R) <= 3 && Math.Abs(a.G - b.G) <= 3 && Math.Abs(a.B - b.B) <= 3;
    }

    [AvaloniaFact]
    public async Task The_screen_accent_is_the_habit_colour()
    {
        var red = await CreateHabitAsync("Rossa", Red);
        var amber = await CreateHabitAsync("Ambra", Amber);
        var (vm, view, w) = await OpenAsync(red);
        var probe = AddAccentProbe(view);
        var box = new CheckBox { IsChecked = true, Content = "x", HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom };
        ((Grid)view.Content!).Children.Add(box);
        Pump();
        // Il quadratino pieno del check (parte "NormalRectangle" del template Fluent): letto in un angolo, fuori dalla spunta.
        Color Glyph() => Pixel(w, box.GetVisualDescendants().OfType<Border>().First(b => b.Name == "NormalRectangle"), -8, -8);

        Near(Pixel(w, probe), Red).Should().BeTrue("Brush.Accent dentro la schermata e' il colore dell'abitudine, non il default dell'app");
        Near(Glyph(), Red).Should().BeTrue("la famiglia SystemAccentColor tinge anche i controlli Fluent");

        await vm.LoadAsync(amber);   // stesso tab, altra abitudine: la view non si ricostruisce
        Pump();
        Near(Pixel(w, probe), Amber).Should().BeTrue("cambiando Node l'accento segue");
        Near(Glyph(), Amber).Should().BeTrue();
        w.Close();
    }

    [AvaloniaFact]
    public async Task The_accent_survives_a_theme_switch()
    {
        var red = await CreateHabitAsync("Rossa2", Red);
        var (_, view, w) = await OpenAsync(red);
        var probe = AddAccentProbe(view);
        w.RequestedThemeVariant = ThemeVariant.Light;
        Pump();
        var header = view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "HabitName");
        var lightBg = Pixel(w, header, 340, 0);
        Near(lightBg, "#F7F7F8").Should().BeTrue("sfondo chiaro");

        w.RequestedThemeVariant = ThemeVariant.Dark;
        Pump();
        Near(Pixel(w, probe), Red).Should().BeTrue("l'accento resta il colore dell'abitudine");
        Near(Pixel(w, header, 340, 0), "#18181B").Should().BeTrue("lo sfondo segue il tema");
        w.Close();
    }

    [AvaloniaFact]
    public async Task The_header_shows_the_habit_name()
    {
        var id = await CreateHabitAsync("Nome visibile", Red);
        var (_, view, w) = await OpenAsync(id);
        view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "HabitName").Text.Should().Be("Nome visibile");
        w.Close();
    }

    [AvaloniaFact]
    public async Task Only_the_cronologia_chip_is_visible_in_f2a()
    {
        var id = await CreateHabitAsync("Chip", Red);
        var (_, view, w) = await OpenAsync(id);
        var chips = view.GetVisualDescendants().OfType<CheckBox>().Where(c => c.Classes.Contains("segment")).ToList();
        chips.Should().HaveCount(4);
        chips.Where(c => c.IsVisible).Select(c => c.Name).Should().Equal("ChipCronologia");
        view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ExportButton").IsVisible.Should().BeFalse();
        w.Close();
    }

    [AvaloniaFact]
    public async Task The_cronologia_chip_toggles_its_column()
    {
        var id = await CreateHabitAsync("Colonna", Red);
        var (vm, view, w) = await OpenAsync(id);
        var chip = view.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "ChipCronologia");
        var column = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "CronologiaColumn");

        var wasOpen = vm.IsCronologiaOpen;
        chip.IsChecked.Should().Be(wasOpen);
        chip.IsChecked = !wasOpen;   // cio' che fa il clic
        Pump();
        vm.IsCronologiaOpen.Should().Be(!wasOpen);
        column.IsVisible.Should().Be(!wasOpen);
        chip.IsChecked = wasOpen;
        Pump();
        column.IsVisible.Should().Be(wasOpen);
        foreach (var n in new[] { "TodayHost", "CalendarHost", "QuickAddHost", "TimerHost", "RecordsHost" })
            view.GetVisualDescendants().OfType<Border>().Should().Contain(b => b.Name == n);
        w.Close();
    }

    [AvaloniaFact]
    public async Task A_group_shows_the_header_and_no_chips()
    {
        var id = await CreateHabitAsync("Gruppo", Amber, NodeType.Group);
        var (_, view, w) = await OpenAsync(id);
        view.GetVisualDescendants().OfType<CheckBox>().Where(c => c.IsVisible).Should().BeEmpty();
        view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "HabitName").Text.Should().Be("Gruppo");
        w.Close();
    }
}
