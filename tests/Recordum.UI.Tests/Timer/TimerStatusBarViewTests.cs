using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.Timer;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.HabitScreen;
using Xunit;

namespace Recordum.UI.Tests.Timer;

/// <summary>La barra dei timer e il cronometro della Cronologia. DB SQLCipher PRIVATO e orologio finto (R3/R8).</summary>
public class TimerStatusBarViewTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-timer");
    private static readonly DateTimeOffset Noon = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private static void Pump() => PrivateDbHost.Pump();

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static async Task<Guid> CreateHabitAsync(IServiceScope scope, string name, bool integratedTimer = false)
    {
        var id = await scope.ServiceProvider.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = name, Color = "#0EA5E9",
        });
        if (integratedTimer)
            await scope.ServiceProvider.GetRequiredService<IHabitConfigService>().UpsertAsync(id, c =>
            {
                c.HasIntegratedTimer = true;
                c.TimerMode = TimerMode.Stopwatch;
            });
        return id;
    }

    private static Border Bar(Window w) => w.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Bar");

    private static T RowControl<T>(Window w, string name, string habitName) where T : Control =>
        w.GetVisualDescendants().OfType<T>()
            .Single(c => c.Name == name && c.DataContext is ActiveTimerRowViewModel r && r.HabitName == habitName);

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    [AvaloniaFact]
    public async Task A_timer_already_in_the_db_shows_in_the_bar_at_startup_and_stop_records_the_session()
    {
        Host.Clock.Now = Noon;
        var name = $"Run-{Guid.NewGuid():N}";
        using var scope = Host.Provider.CreateScope();
        var id = await CreateHabitAsync(scope, name);
        await scope.ServiceProvider.GetRequiredService<IActiveTimerService>().StartAsync(id, TimerMode.Stopwatch);
        Host.Clock.Now = Noon.AddSeconds(125);   // l'app "riaperta" due minuti e cinque dopo

        var (window, _) = await ShellFixture.OpenMainWindowAsync(Host.Provider.GetRequiredService<MainWindowViewModel>());
        try
        {
            Bar(window).IsVisible.Should().BeTrue();
            var elapsed = RowControl<TextBlock>(window, "RowElapsed", name);
            elapsed.Text.Should().Be("02:05");
            // Il tempo e' la cifra della barra: 15/600 nella variante "testo" del colore (cornice, Task 8).
            elapsed.FontSize.Should().Be(15);
            elapsed.FontWeight.Should().Be(Avalonia.Media.FontWeight.SemiBold);
            var text = ((Avalonia.Media.ISolidColorBrush)elapsed.Foreground!).Color;
            text.Should().Be(((Avalonia.Media.ISolidColorBrush)Recordum.UI.Converters.HabitTextBrushConverter.BrushFor("#0EA5E9", window.ActualThemeVariant)).Color);
            window.TryFindResource("Brush.Pane", window.ActualThemeVariant, out var paneBrush).Should().BeTrue();
            var pane = ((Avalonia.Media.ISolidColorBrush)paneBrush!).Color;
            Recordum.UI.Theming.HabitColors.Contrast(text, Recordum.UI.Theming.HabitColors.Blend(Avalonia.Media.Color.Parse("#0EA5E9"), 0.08, pane))
                .Should().BeGreaterThanOrEqualTo(4.5);
            // Direzione visiva §5: la riga del timer è tinta nel colore dell'abitudine che corre.
            var tint = RowControl<Border>(window, "RowTint", name);
            ((Avalonia.Media.ISolidColorBrush)tint.Background!).Color.Should().Be(Avalonia.Media.Color.Parse("#0EA5E9"));
            tint.Opacity.Should().BeApproximately(0.08, 0.001);

            Click(window, RowControl<Button>(window, "RowStop", name));

            await UntilAsync(() => !Bar(window).IsVisible);
            using var db = await Host.Provider.GetRequiredService<IDbContextFactory<RecordumDbContext>>().CreateDbContextAsync();
            var completion = await db.Completions.AsNoTracking().SingleAsync(c => c.HabitNodeId == id);
            completion.DurationSeconds.Should().Be(125);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Starting_the_stopwatch_from_the_cronologia_shows_the_bar_without_reopening_anything()
    {
        Host.Clock.Now = Noon;
        var name = $"Sleep-{Guid.NewGuid():N}";
        using var scope = Host.Provider.CreateScope();
        var id = await CreateHabitAsync(scope, name, integratedTimer: true);

        var (main, _) = await ShellFixture.OpenMainWindowAsync(Host.Provider.GetRequiredService<MainWindowViewModel>());
        var vm = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var screen = new Window { Width = 800, Height = 900, Content = new HabitScreenView { DataContext = vm } };
        screen.Show();
        Pump();
        try
        {
            Bar(main).IsVisible.Should().BeFalse("nessun timer attivo all'apertura");

            Click(screen, screen.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "StartButton"));

            await UntilAsync(() => Bar(main).IsVisible);
            RowControl<TextBlock>(main, "RowName", name).Text.Should().Be(name);
            vm.TimerControl.IsActive.Should().BeTrue();
        }
        finally
        {
            await scope.ServiceProvider.GetRequiredService<IActiveTimerService>().StopWithoutRecordingAsync(id);
            screen.Close();
            main.Close();
        }
    }
}
