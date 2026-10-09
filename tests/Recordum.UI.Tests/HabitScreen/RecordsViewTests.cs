using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Calendar;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Records;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>Le registrazioni del giorno nella Cronologia (F2a task 7). DB SQLCipher PRIVATO, orologio finto,
/// conferma di cancellazione finta che risponde si' o no.</summary>
public class RecordsViewTests
{
    private static readonly Confirm Answer = new();
    private static readonly PrivateDbHost Host = new("recordum-ui-records",
        s => s.AddSingleton<IConfirmationDialog>(Answer));
    private static readonly DateTimeOffset Noon = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed class Confirm : IConfirmationDialog
    {
        public bool Yes { get; set; }
        public int Asked { get; set; }
        public Task<bool> ConfirmAsync(string title, string message) { Asked++; return Task.FromResult(Yes); }
    }

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id);

    private static async Task<Opened> OpenAsync(HabitType type, Func<IServiceScope, Guid, Task>? seed = null)
    {
        Host.Clock.Now = Noon;
        Answer.Yes = false; Answer.Asked = 0;
        var scope = Host.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"RC-{Guid.NewGuid():N}", Color = "#E11D48",
        });
        if (seed is not null) await seed(scope, id);
        var vm = sp.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 800, Height = 900, Content = view };
        w.Show();
        Pump();
        return new Opened(scope, vm, view, w, id);
    }

    private static Task RecordAtAsync(IServiceScope scope, Guid id, DateTimeOffset at) =>
        scope.ServiceProvider.GetRequiredService<ICompletionService>().RecordAsync(new Completion
        {
            HabitNodeId = id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
        }).AsTask();

    private static void Pump() => PrivateDbHost.Pump();

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    private static Button Day(Visual root, DateOnly d) =>
        root.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Classes.Contains("day") && ((CalendarDayCellViewModel)b.DataContext!).Date == d);

    private static Border RecordsHost(Opened o) => o.View.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "RecordsHost");

    private static List<string> Lines(Opened o) =>
        [.. RecordsHost(o).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];

    private static List<Button> Buttons(Opened o) => [.. RecordsHost(o).GetVisualDescendants().OfType<Button>()];

    private static async Task SeedTwoDays(IServiceScope s, Guid id)
    {
        await RecordAtAsync(s, id, Noon);                       // 15 marzo 12:00
        await RecordAtAsync(s, id, Noon.AddDays(-1));           // 14 marzo 12:00
    }

    [AvaloniaFact]
    public async Task The_rows_are_those_of_the_selected_day_and_change_with_it()
    {
        var o = await OpenAsync(HabitType.Action, SeedTwoDays);
        try
        {
            await UntilAsync(() => Lines(o).Contains("2026-03-15 12:00"));
            Lines(o).Should().NotContain("2026-03-14 12:00");

            Click(o.Window, Day(o.View, new DateOnly(2026, 3, 14)));
            await UntilAsync(() => Lines(o).Contains("2026-03-14 12:00"));
            Lines(o).Should().NotContain("2026-03-15 12:00");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Rows_have_only_a_delete_button_no_edit()
    {
        var o = await OpenAsync(HabitType.Action, SeedTwoDays);
        try
        {
            await UntilAsync(() => Buttons(o).Count == 1);
            var b = Buttons(o).Single();
            b.Name.Should().Be("DeleteButton");
            Avalonia.Automation.AutomationProperties.GetName(b).Should().NotBeNullOrEmpty();
            ToolTip.GetTip(b).Should().NotBeNull();
            Lines(o).Should().NotContain("✎");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Cancelling_the_confirmation_keeps_the_record()
    {
        var o = await OpenAsync(HabitType.Action, SeedTwoDays);
        try
        {
            await UntilAsync(() => Buttons(o).Count == 1);
            Answer.Yes = false;
            Click(o.Window, Buttons(o).Single());
            await UntilAsync(() => Answer.Asked == 1);
            Pump();
            Buttons(o).Should().HaveCount(1);
            var cs = o.Scope.ServiceProvider.GetRequiredService<ICompletionService>();
            (await cs.ListByHabitAsync(o.Id)).Should().HaveCount(2);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Confirming_deletes_it()
    {
        var o = await OpenAsync(HabitType.Action, SeedTwoDays);
        try
        {
            await UntilAsync(() => Buttons(o).Count == 1);
            o.View.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "RecordsEmpty").IsVisible.Should().BeFalse();
            Answer.Yes = true;
            Click(o.Window, Buttons(o).Single());
            await UntilAsync(() => Buttons(o).Count == 0);
            var cs = o.Scope.ServiceProvider.GetRequiredService<ICompletionService>();
            (await cs.ListByHabitAsync(o.Id)).Should().HaveCount(1, "resta solo quella di ieri");
            var empty = o.View.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "RecordsEmpty");
            empty.IsVisible.Should().BeTrue("il giorno non ha piu' righe: compare lo stato vuoto");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task An_abstinence_habit_lists_its_periods_and_deletes_behind_the_confirmation()
    {
        var o = await OpenAsync(HabitType.Resistance, async (s, id) =>
            await s.ServiceProvider.GetRequiredService<IAbstinenceService>().StartAsync(id));
        try
        {
            await UntilAsync(() => RecordsHost(o).Child is AbstinencePeriodListView && Buttons(o).Count == 1);
            Answer.Yes = false;
            Click(o.Window, Buttons(o).Single());
            await UntilAsync(() => Answer.Asked == 1);
            Buttons(o).Should().HaveCount(1, "un annullo lascia la riga");

            Answer.Yes = true;
            Click(o.Window, Buttons(o).Single());
            await UntilAsync(() => Buttons(o).Count == 0);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public void The_template_builds_the_list_of_its_type_only()
    {
        var t = RecordsTemplate.Instance;
        t.Match(new object()).Should().BeFalse();
        t.Build(null).Should().BeNull();
        t.Build(new object()).Should().BeNull();
    }
}
