using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;
using Recordum.UI.Bootstrap;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Calendar;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>Il calendario della Cronologia. DB SQLCipher PRIVATO e orologio finto (Ruling R8, R3): il giorno di
/// "oggi" non deve dipendere dall'ora in cui gira la suite.</summary>
public class CalendarNavigatorViewTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-cal");
    private static PrivateDbHost.FakeClock Clock => Host.Clock;
    private static ServiceProvider ProviderValue => Host.Provider;

    private static async Task<(HabitScreenViewModel Vm, HabitScreenView View, Window Window)> OpenAsync(
        DateTimeOffset now, string resetTime = "00:00")
    {
        Clock.Now = now;
        var scope = ProviderValue.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", resetTime);
        var id = await scope.ServiceProvider.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = $"Cal-{Guid.NewGuid():N}", Color = "#E11D48",
        });
        var vm = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 800, Height = 700, Content = view };
        w.Show();
        Pump();
        return (vm, view, w);
    }

    private static void Pump() => PrivateDbHost.Pump();

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static List<Button> Days(Visual root) =>
        root.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("day")).ToList();

    private static Button DayOf(Visual root, DateOnly d) =>
        Days(root).Single(b => ((CalendarDayCellViewModel)b.DataContext!).Date == d);

    private static Point Center(Window w, Control c) =>
        c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;

    private static Color Pixel(Window w, Control c, double dx, double dy)
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

    private static readonly DateTimeOffset Noon = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public async Task Clicking_a_day_selects_it_and_moves_the_quick_add_date()
    {
        using var culture = TestCulture.Pin();
        var (vm, view, w) = await OpenAsync(Noon);
        try
        {
            var yesterday = new DateOnly(2026, 3, 14);
            vm.Calendar.SelectedDate.Should().Be(new DateOnly(2026, 3, 15), "parte da oggi");
            var cell = DayOf(view, yesterday);
            var p = Center(w, cell);
            w.MouseDown(p, MouseButton.Left);
            w.MouseUp(p, MouseButton.Left);
            Pump();
            vm.Calendar.SelectedDate.Should().Be(yesterday);
            vm.QuickAdd.Should().NotBeNull();
            DateOnly.FromDateTime(vm.QuickAdd!.CompletionDate).Should().Be(yesterday);
            cell.Classes.Should().Contain("selected");
            DayOf(view, new DateOnly(2026, 3, 15)).Classes.Should().NotContain("selected");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Today_is_the_day_of_the_boundary()
    {
        using var culture = TestCulture.Pin();
        // Reset alle 04:00, adesso le 02:30 locali del 15: per il confine e' ancora il 14.
        var (vm, view, w) = await OpenAsync(new DateTimeOffset(2026, 3, 15, 2, 30, 0, TimeSpan.Zero), "04:00");
        try
        {
            var marked = Days(view).Where(b => b.Classes.Contains("today")).ToList();
            marked.Should().ContainSingle();
            ((CalendarDayCellViewModel)marked[0].DataContext!).Date.Should().Be(new DateOnly(2026, 3, 14));
            vm.Calendar.SelectedDate.Should().Be(new DateOnly(2026, 3, 14));
            // Il 15 di calendario e' futuro per il confine: la cella non si puo' premere.
            DayOf(view, new DateOnly(2026, 3, 15)).IsEnabled.Should().BeFalse();
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task The_calendar_retints_on_a_theme_switch()
    {
        using var culture = TestCulture.Pin();
        var (_, view, w) = await OpenAsync(Noon);
        try
        {
            // Una cella vuota (ne' oggi ne' selezionata); si legge fuori dal centro, lontano dal giorno.
            var empty = DayOf(view, new DateOnly(2026, 3, 10));
            w.RequestedThemeVariant = ThemeVariant.Light;
            Pump();
            Near(Pixel(w, empty, 8, 8), "#E4E4E7").Should().BeTrue("sfondo chiaro della cella");
            w.RequestedThemeVariant = ThemeVariant.Dark;
            Pump();
            Near(Pixel(w, empty, 8, 8), "#3F3F46").Should().BeTrue("la cella si ritinge senza riaprire la vista");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Navigation_goes_back_a_month_and_returns_and_week_mode_shows_seven_cells()
    {
        using var culture = TestCulture.Pin();
        var (vm, view, w) = await OpenAsync(Noon);
        try
        {
            Days(view).Should().HaveCount(42);
            vm.Calendar.DisplayMonth.Should().Be(3);
            var prev = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PrevButton");
            var next = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NextButton");

            w.MouseDown(Center(w, prev), MouseButton.Left); w.MouseUp(Center(w, prev), MouseButton.Left);
            await UntilAsync(() => vm.Calendar.DisplayMonth == 2);
            Days(view).Select(b => ((CalendarDayCellViewModel)b.DataContext!).Date)
                .Should().Contain(new DateOnly(2026, 2, 10)).And.NotContain(new DateOnly(2026, 3, 10));

            w.MouseDown(Center(w, next), MouseButton.Left); w.MouseUp(Center(w, next), MouseButton.Left);
            await UntilAsync(() => vm.Calendar.DisplayMonth == 3);
            Days(view).Should().Contain(b => b.Classes.Contains("today"));

            var week = view.GetVisualDescendants().OfType<RadioButton>().Single(b => b.Name == "WeekMode");
            w.MouseDown(Center(w, week), MouseButton.Left); w.MouseUp(Center(w, week), MouseButton.Left);
            await UntilAsync(() => vm.Calendar.Cells.Count == 7);
            Days(view).Should().HaveCount(7);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public async Task Chevrons_and_day_cells_have_accessible_names()
    {
        using var culture = TestCulture.Pin();
        var (_, view, w) = await OpenAsync(Noon);
        try
        {
            var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
            foreach (var name in new[] { "PrevButton", "NextButton" })
            {
                var b = buttons.Single(x => x.Name == name);
                Avalonia.Automation.AutomationProperties.GetName(b).Should().NotBeNullOrWhiteSpace(name);
                ToolTip.GetTip(b).Should().Be(Avalonia.Automation.AutomationProperties.GetName(b));
            }
            var cell = DayOf(view, new DateOnly(2026, 3, 14));
            Avalonia.Automation.AutomationProperties.GetName(cell).Should()
                .Be(new DateOnly(2026, 3, 14).ToString("D", System.Globalization.CultureInfo.CurrentCulture));
        }
        finally { w.Close(); }
    }
}
