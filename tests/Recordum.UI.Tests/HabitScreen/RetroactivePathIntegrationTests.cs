using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;
using Recordum.Presentation.ViewModels.Workspace;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.HabitScreen;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>Il percorso end to end sul DB SQLCipher vero (F2a task 8): Home + schermata dell'abitudine insieme, aperta
/// dalla Home, orologio finto a cavallo del reset. DB PRIVATO (Ruling R8/R3).</summary>
public class RetroactivePathIntegrationTests
{
    private static readonly Yes Answer = new();
    private static readonly PrivateDbHost Host = new("recordum-ui-retro", s => s.AddSingleton<IConfirmationDialog>(Answer));

    private sealed class Yes : IConfirmationDialog
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
    }

    // Lunedi' 16 marzo 2026 02:30 locale (il fuso del clock finto e' UTC), reset 04:00: per il confine "oggi" e'
    // domenica 15, "ieri" e' sabato 14. Il 14 e il 15 sono dentro la striscia di 7 giorni; il 16 e' futuro.
    private static readonly DateTimeOffset Now = new(2026, 3, 16, 2, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly D = new(2026, 3, 15);

    private sealed record Opened(MainWindow Window, MainWindowViewModel Vm, Guid Id, HabitScreenView View);

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); PrivateDbHost.Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        PrivateDbHost.Pump();
    }

    private static async Task<Opened> OpenAsync(Func<IServiceProvider, Guid, Task>? seed = null)
    {
        Host.Clock.Now = Now;
        var sp = Host.Provider;
        using var scope = sp.CreateScope();
        var s = scope.ServiceProvider;
        await s.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "04:00");
        await s.GetRequiredService<IAppSettingService>().SetAsync("ui.workspace", new WorkspaceState().ToJson());
        var id = await s.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = $"Y-{Guid.NewGuid():N}", Color = "#E11D48",
        });
        if (seed is not null) await seed(s, id);

        var (window, vm) = await ShellFixture.OpenMainWindowAsync(sp.GetRequiredService<MainWindowViewModel>());
        vm.Home.SelectedRow = vm.Home.Rows.Single(r => r.HabitNodeId == id);
        await UntilAsync(() => window.GetVisualDescendants().OfType<HabitScreenView>().Any());
        var view = window.GetVisualDescendants().OfType<HabitScreenView>().Single();
        await UntilAsync(() => view.GetVisualDescendants().OfType<Button>().Any(b => b.Classes.Contains("day"))
            && view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Name == "HabitName"));
        return new Opened(window, vm, id, view);
    }

    private static CalendarDayCellViewModel Cell(Opened o, DateOnly d) =>
        o.View.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("day")).Select(b => (CalendarDayCellViewModel)b.DataContext!)
            .Single(c => c.Date == d);

    private static Button DayButton(Opened o, DateOnly d) =>
        o.View.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Classes.Contains("day") && ((CalendarDayCellViewModel)b.DataContext!).Date == d);

    private static HomeRowViewModel Row(Opened o) => o.Vm.Home.Rows.Single(r => r.HabitNodeId == o.Id);

    private static IDisposable PinCulture()
    {
        var (c, u) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        var it = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = it; CultureInfo.CurrentUICulture = it;
        return new Restore(() => { CultureInfo.CurrentCulture = c; CultureInfo.CurrentUICulture = u; });
    }

    private sealed class Restore(Action undo) : IDisposable { public void Dispose() => undo(); }

    [AvaloniaFact]
    public async Task Yesterday_after_midnight_lands_on_yesterday()
    {
        using var culture = PinCulture();
        var o = await OpenAsync();
        try
        {
            var yesterday = D.AddDays(-1);
            Cell(o, D).IsToday.Should().BeTrue("per il confine delle 04:00 oggi e' ancora il 15");
            Click(o.Window, DayButton(o, yesterday));
            Click(o.Window, o.View.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SaveButton"));

            var cs = Host.Provider.CreateScope().ServiceProvider.GetRequiredService<ICompletionService>();
            await UntilAsync(() => cs.ListByHabitAsync(o.Id).AsTask().GetAwaiter().GetResult().Count == 1);
            var saved = (await cs.ListByHabitAsync(o.Id)).Single();
            saved.IsRetroactive.Should().BeTrue();
            saved.CompletedAt.Should().Be(new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc), "NoonOf(D-1)");

            await UntilAsync(() => Cell(o, yesterday).TintStep > 0);
            Cell(o, D).TintStep.Should().Be(0, "la registrazione di ieri non finisce su oggi");

            // D-1 e D sono dentro la striscia di 7 giorni (ultimo = oggi): la riga Home la mostra al penultimo posto.
            await UntilAsync(() => Row(o).CompletionsLast7Days.Sum() == 1);
            Row(o).CompletionsLast7Days[^2].Should().Be(1);
            Row(o).CompletionsLast7Days[^1].Should().Be(0);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Deleting_from_the_history_reaches_the_db_and_the_home_row()
    {
        using var culture = PinCulture();
        var o = await OpenAsync(async (s, id) =>
            await s.GetRequiredService<ICompletionService>().RecordAsync(new Completion
            {
                HabitNodeId = id, CompletedAt = Now.UtcDateTime.AddHours(-1), CompletedAtMs = Now.AddHours(-1).ToUnixTimeMilliseconds(),
            }));
        try
        {
            await UntilAsync(() => Row(o).CompletionsLast7Days.Sum() == 1);
            var records = o.View.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "RecordsHost");
            await UntilAsync(() => records.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "DeleteButton"));
            // Comando e non clic: nella finestra intera il pulsante puo' stare fuori dall'area visibile (il clic e'
            // gia' provato in RecordsViewTests); qui conta che la cancellazione arrivi al DB e alla riga Home.
            var del = records.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "DeleteButton");
            del.Command!.Execute(del.CommandParameter);

            var cs = Host.Provider.CreateScope().ServiceProvider.GetRequiredService<ICompletionService>();
            await UntilAsync(() => cs.ListByHabitAsync(o.Id).AsTask().GetAwaiter().GetResult().Count == 0);
            await UntilAsync(() => Row(o).CompletionsLast7Days.Sum() == 0);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Renaming_from_the_home_updates_the_open_habit_screen_header()
    {
        using var culture = PinCulture();
        var o = await OpenAsync();
        try
        {
            var header = o.View.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "HabitName");
            var row = Row(o);
            row.BeginRenameCommand.Execute(null);
            row.PendingName = "Rinominata";
            await row.CommitRenameCommand.ExecuteAsync(null);

            await UntilAsync(() => header.Text == "Rinominata");
            Row(o).Name.Should().Be("Rinominata", "il ricarico della Home non rompe la riga");
            o.Vm.Home.SelectedRow?.HabitNodeId.Should().Be(o.Id, "la selezione sopravvive al ricarico");
        }
        finally { o.Window.Close(); }
    }
}
