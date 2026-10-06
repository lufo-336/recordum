using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Data.Services;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Recordum.UI.Bootstrap;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>I pannelli di registrazione della Cronologia. DB SQLCipher PRIVATO, orologio finto e un
/// ICompletionService che puo' trattenere il salvataggio (Ruling R8).</summary>
public class QuickAddViewTests
{
    private static readonly FakeClock Clock = new();
    private static readonly HoldingCompletions Holder = new();
    private static readonly Lazy<ServiceProvider> Provider = new(() => Build().GetAwaiter().GetResult());
    private static readonly DateTimeOffset Noon = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private static async Task<ServiceProvider> Build()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"recordum-ui-qa-{Guid.NewGuid():N}");
        var options = RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, dir], _ => null);
        var services = new ServiceCollection();
        services.AddRecordumCore(options);
        services.AddRecordumAvaloniaShell();
        services.AddSingleton<IDbKeyProvider>(new FixedKey());
        services.AddSingleton<IClock>(Clock);
        services.AddScoped<ICompletionService>(sp => Holder.Wrap(ActivatorUtilities.CreateInstance<CompletionService>(sp)));
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<CoreInitializer>().InitializeAsync();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
#pragma warning disable CA1031 // best effort a fine processo
            try { SqliteConnection.ClearAllPools(); if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (Exception) { }
#pragma warning restore CA1031
        };
        return provider;
    }

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id)
    {
        public ICompletionService Completions => Scope.ServiceProvider.GetRequiredService<ICompletionService>();
    }

    private static async Task<Guid> CreateAsync(IServiceScope scope, HabitType type) =>
        await scope.ServiceProvider.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"QA-{Guid.NewGuid():N}", Color = "#E11D48",
        });

    private static async Task<Opened> OpenAsync(HabitType type = HabitType.Action)
    {
        Clock.Now = Noon;
        Holder.Reset();
        var scope = Provider.Value.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await CreateAsync(scope, type);
        var vm = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 800, Height = 900, Content = view };
        w.Show();
        Pump();
        return new Opened(scope, vm, view, w, id);
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Avalonia.Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    private static Button Day(Visual root, DateOnly d) =>
        root.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Classes.Contains("day") && ((Recordum.Presentation.ViewModels.HabitScreen.Calendar.CalendarDayCellViewModel)b.DataContext!).Date == d);

    [AvaloniaFact]
    public void Each_quick_add_type_gets_its_own_view()
    {
        var t = QuickAddTemplate.Instance;
        var cs = Holder.Wrap(null!);
        t.Match(new object()).Should().BeFalse();
        t.Build(new object()).Should().BeNull("un ViewModel sconosciuto non costruisce niente");
        t.Build(null).Should().BeNull();
        t.Build(new ActionQuickAddViewModel(cs, Clock)).Should().BeOfType<ActionQuickAddView>();
        t.Build(new CounterTapQuickAddViewModel(cs, Clock)).Should().BeOfType<CounterTapQuickAddView>();
        t.Build(new AbstinenceQuickAddViewModel(cs, Provider.Value.CreateScope().ServiceProvider.GetRequiredService<IAbstinenceService>(), Clock, null)).Should().BeOfType<AbstinenceQuickAddView>();
        // Task 5: misurato e serie non hanno ancora un ramo.
        t.Build(RuntimeHelpers.GetUninitializedObject(typeof(MeasuredQuickAddViewModel))).Should().BeNull();
        t.Build(RuntimeHelpers.GetUninitializedObject(typeof(CounterSeriesQuickAddViewModel))).Should().BeNull();
    }

    [AvaloniaFact]
    public async Task The_host_shows_the_panel_of_the_habit_type()
    {
        var o = await OpenAsync(HabitType.Counter);
        try
        {
            // Un contatore senza metriche e' il contatore a tap.
            Named<Button>(o.View, "TapButton").Should().NotBeNull();
            Named<TextBlock>(o.View, "Count").Text.Should().NotBeNullOrEmpty();
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_tap_on_yesterday_records_a_retroactive_completion()
    {
        var o = await OpenAsync();
        try
        {
            var yesterday = new DateOnly(2026, 3, 14);
            Click(o.Window, Day(o.View, yesterday));
            Click(o.Window, Named<Button>(o.View, "SaveButton"));
            await UntilAsync(() => Holder.Recorded.Count == 1);
            var retro = Holder.Recorded[0];
            retro.HabitNodeId.Should().Be(o.Id);
            retro.IsRetroactive.Should().BeTrue();
            retro.CompletedAtMs.Should().Be(new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                "NoonOf(ieri)");

            // Il caso del parametro muto: con oggi selezionato la risposta e' l'altra.
            Click(o.Window, Day(o.View, new DateOnly(2026, 3, 15)));
            await UntilAsync(() => !((ActionQuickAddViewModel)o.Vm.QuickAdd!).IsSaving);
            Click(o.Window, Named<Button>(o.View, "SaveButton"));
            await UntilAsync(() => Holder.Recorded.Count == 2);
            Holder.Recorded[1].IsRetroactive.Should().BeFalse();
            Holder.Recorded[1].CompletedAtMs.Should().Be(Noon.ToUnixTimeMilliseconds(), "l'ora dell'orologio finto");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_double_click_on_save_records_once()
    {
        var o = await OpenAsync();
        try
        {
            var gate = new TaskCompletionSource();
            Holder.Gate = gate.Task;
            var save = Named<Button>(o.View, "SaveButton");
            Click(o.Window, save);
            Click(o.Window, save);
            gate.SetResult();
            await UntilAsync(() => !((ActionQuickAddViewModel)o.Vm.QuickAdd!).IsSaving);
            Holder.Recorded.Should().HaveCount(1);
            (await o.Completions.ListByHabitAsync(o.Id)).Should().HaveCount(1);
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task Switching_habit_during_a_save_records_on_the_first()
    {
        var o = await OpenAsync();
        try
        {
            var second = await CreateAsync(o.Scope, HabitType.Action);
            var gate = new TaskCompletionSource();
            Holder.Gate = gate.Task;
            var first = (ActionQuickAddViewModel)o.Vm.QuickAdd!;
            Click(o.Window, Named<Button>(o.View, "SaveButton"));
            first.IsSaving.Should().BeTrue("il salvataggio e' in volo");

            await o.Vm.LoadAsync(second);
            Pump();
            o.Vm.Node!.Id.Should().Be(second);
            o.Vm.QuickAdd.Should().NotBeSameAs(first);

            gate.SetResult();
            await UntilAsync(() => Holder.Recorded.Count == 1);
            Holder.Recorded[0].HabitNodeId.Should().Be(o.Id, "la registrazione va all'abitudine su cui e' partita");
            o.Vm.Node!.Id.Should().Be(second, "la schermata mostra la seconda");
            (await o.Completions.ListByHabitAsync(second)).Should().BeEmpty();
        }
        finally { o.Window.Close(); }
    }

    private sealed class HoldingCompletions
    {
        public Task? Gate { get; set; }
        public List<Completion> Recorded { get; } = [];
        public void Reset() { Gate = null; Recorded.Clear(); }
        public ICompletionService Wrap(ICompletionService inner) => new Decorator(this, inner);

        private sealed class Decorator(HoldingCompletions h, ICompletionService i) : ICompletionService
        {
            public async ValueTask<Guid> RecordAsync(Completion draft, IReadOnlyList<CompletionValue>? values = null,
                IReadOnlyList<CompletionSegment>? segments = null, CancellationToken ct = default)
            {
                var gate = h.Gate;
                if (gate is not null) await gate;
                h.Recorded.Add(draft);
                return await i.RecordAsync(draft, values, segments, ct);
            }
            public ValueTask UpdateAsync(Guid id, Action<Completion> mutate, CancellationToken ct = default) => i.UpdateAsync(id, mutate, ct);
            public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => i.DeleteAsync(id, ct);
            public ValueTask<Completion?> GetAsync(Guid id, CancellationToken ct = default) => i.GetAsync(id, ct);
            public ValueTask<IReadOnlyList<Completion>> ListByHabitAsync(Guid habitNodeId, DateTimeOffset? fromUtc = null,
                DateTimeOffset? toUtc = null, int? take = null, CancellationToken ct = default) => i.ListByHabitAsync(habitNodeId, fromUtc, toUtc, take, ct);
            public ValueTask<IReadOnlyList<HeatmapCell>> GetHeatmapAsync(Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default) => i.GetHeatmapAsync(habitNodeId, fromDate, toDate, ct);
            public ValueTask<IReadOnlyList<DailyValue>> GetDailySumAsync(Guid habitNodeId, Guid metricDefinitionId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default) => i.GetDailySumAsync(habitNodeId, metricDefinitionId, fromDate, toDate, ct);
            public ValueTask<IReadOnlyList<DailyMagnitude>> GetDailyMagnitudeAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default) => i.GetDailyMagnitudeAsync(habitNodeId, year, month, ct);
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow => Now;
        public long UtcNowMs => Now.ToUnixTimeMilliseconds();
        public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FixedKey : IDbKeyProvider
    {
        private readonly byte[] _key = Enumerable.Range(0, 32).Select(i => (byte)(i * 7 + 1)).ToArray();
        public string ProviderId => "fixed-test-key";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) => ValueTask.FromResult(new KeyMaterial([.. _key]));
    }
}
