using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>Onda "Giornata", Task 8b: il log rapido di un'azione già fatta chiede conferma e toglie la spunta.
/// Contenitore vero, DB SQLCipher privato, orologio finto, conferma finta che risponde sì o no.</summary>
public class HomeUncheckTests
{
    private static readonly Confirm Answer = new();
    private static readonly PrivateDbHost Host = new("recordum-ui-uncheck",
        s => s.AddSingleton<IConfirmationDialog>(Answer));
    private static readonly DateTimeOffset Noon = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed class Confirm : IConfirmationDialog
    {
        public bool Yes { get; set; }
        public int Asked { get; set; }
        public Task<bool> ConfirmAsync(string title, string message) { Asked++; return Task.FromResult(Yes); }
    }

    private sealed record Opened(IServiceScope Scope, HomeViewModel Vm, HomeView View, Window Window, Guid Id);

    private static async Task WaitAsync(Func<bool> until)
    {
        for (var i = 0; i < 200 && !until(); i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Un'azione già fatta oggi (un'ora fa), e la Home aperta su di lei.</summary>
    private static async Task<Opened> OpenDoneAsync(bool answer)
    {
        Host.Clock.Now = Noon;
        Answer.Yes = answer; Answer.Asked = 0;
        var scope = Host.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = $"UN-{Guid.NewGuid():N}", Color = "#E11D48",
        });
        var at = Noon.AddHours(-1);
        await sp.GetRequiredService<ICompletionService>().RecordAsync(new Completion
        {
            HabitNodeId = id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
        });
        var vm = sp.GetRequiredService<HomeViewModel>();
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = 900, Height = 700, Content = view };
        w.Show();
        vm.LoadCommand.Execute(null);
        await WaitAsync(() => vm.Rows.Any(r => r.HabitNodeId == id));
        return new Opened(scope, vm, view, w, id);
    }

    private static Button QuickLog(Opened o) =>
        o.View.GetVisualDescendants().OfType<HabitRow>().Single(r => (r.DataContext as HomeRowViewModel)?.HabitNodeId == o.Id)
            .GetVisualDescendants().OfType<Button>().Single(b => b.Name == "QuickLog");

    private static async Task<int> CountAsync(Opened o) =>
        (await o.Scope.ServiceProvider.GetRequiredService<ICompletionService>().ListByHabitAsync(o.Id)).Count;

    [AvaloniaFact]
    public async Task Quick_log_on_a_done_action_asks_once_and_yes_unchecks_the_row()
    {
        var o = await OpenDoneAsync(answer: true);
        o.Vm.Rows.Single(r => r.HabitNodeId == o.Id).IsDoneOnDay.Should().BeTrue("la semina l'ha fatta oggi");

        QuickLog(o).Command!.Execute(null);
        await WaitAsync(() => !o.Vm.Rows.Single(r => r.HabitNodeId == o.Id).IsDoneOnDay);

        Answer.Asked.Should().Be(1);
        o.Vm.Rows.Single(r => r.HabitNodeId == o.Id).IsDoneOnDay.Should().BeFalse("la riga torna non fatta");
        (await CountAsync(o)).Should().Be(0);
        o.Window.Close();
        o.Scope.Dispose();
    }

    [AvaloniaFact]
    public async Task Quick_log_on_a_done_action_answered_no_leaves_the_row_done()
    {
        var o = await OpenDoneAsync(answer: false);

        QuickLog(o).Command!.Execute(null);
        await WaitAsync(() => Answer.Asked > 0);
        PrivateDbHost.Pump();

        Answer.Asked.Should().Be(1);
        o.Vm.Rows.Single(r => r.HabitNodeId == o.Id).IsDoneOnDay.Should().BeTrue();
        (await CountAsync(o)).Should().Be(1, "il no non scrive niente: né toglie né aggiunge");
        o.Window.Close();
        o.Scope.Dispose();
    }
}
