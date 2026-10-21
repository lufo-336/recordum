using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Today;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

public sealed class TodayStripTests : IDisposable
{
    // Le finestre si chiudono sempre (anche a test fallito) a fine test: xunit chiama Dispose in ogni caso.
    private readonly List<Window> _windows = [];
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var w in _windows) w.Close();
        foreach (var d in _disposables) d.Dispose();
    }

    // DB privato (Ruling R8): scrive abitudini, completamenti e un periodo di astinenza aperto.
    private static readonly PrivateDbHost Host = new("recordum-ui-today");

    private static readonly Guid Mood5 = Guid.Parse("00000002-0000-0000-0000-000000000007");

    private static T Svc<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    private static async Task<Guid> CreateAsync(HabitType? type, string name)
    {
        using var scope = Host.Provider.CreateScope();
        var id = await Svc<IHabitNodeService>(scope).CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = name, Color = "#E11D48",
        });
        if (type == HabitType.State)
            await Svc<IMetricDefinitionService>(scope).CreateAsync(new MetricDefinition
            {
                HabitNodeId = id, Name = "Umore", Type = MetricType.Integer, ScaleTemplateId = Mood5,
            });
        return id;
    }

    private async Task<(HabitScreenViewModel Vm, HabitScreenView View, Window Window)> OpenAsync(Guid id)
    {
        var vm = Host.Provider.CreateScope().ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 800, Height = 600, Content = view };
        _windows.Add(w);
        w.Show();
        Pump();
        return (vm, view, w);
    }

    private static void Pump() => PrivateDbHost.Pump();

    private static T Find<T>(Visual root, string name) where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static IEnumerable<Control> ContentViews(Visual root) =>
        root.GetVisualDescendants().OfType<Control>().Where(c => c is ActionTodayView or CounterTodayView
            or ResistanceTodayView or StateTodayView);

    private static async Task<IReadOnlyList<Completion>> CompletionsAsync(Guid habit)
    {
        using var scope = Host.Provider.CreateScope();
        return await Svc<ICompletionService>(scope).ListByHabitAsync(habit);
    }

    [AvaloniaTheory]
    [InlineData(HabitType.Action, typeof(ActionTodayView))]
    [InlineData(HabitType.Counter, typeof(CounterTodayView))]
    [InlineData(HabitType.Resistance, typeof(ResistanceTodayView))]
    [InlineData(HabitType.State, typeof(StateTodayView))]
    public async Task Each_type_builds_its_own_today_view(HabitType type, Type expected)
    {
        var (_, view, _) = await OpenAsync(await CreateAsync(type, $"Oggi-{type}"));
        ContentViews(view).Select(c => c.GetType()).Should().Equal(expected);
        view.GetVisualDescendants().OfType<TodayStripView>().Single().IsVisible.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task A_journal_builds_its_own_button_in_the_strip()
    {
        var (_, view, _) = await OpenAsync(await CreateAsync(HabitType.Journal, "Oggi-Diario"));
        ContentViews(view).Should().BeEmpty("nessuna delle quattro view per tipo");
        var strip = view.GetVisualDescendants().OfType<TodayStripView>().Single();
        strip.IsVisible.Should().BeTrue();
        strip.GetVisualDescendants().OfType<JournalTodayView>().Should().ContainSingle();
    }

    [AvaloniaFact]
    public void The_template_builds_the_journal_view_and_returns_null_for_unknown_content()
    {
        using var scope = Host.Provider.CreateScope();
        TodayContentTemplate.Instance.Build(new JournalTodayContent(Svc<IHabitUiPrefsService>(scope))).Should().BeOfType<JournalTodayView>();
        TodayContentTemplate.Instance.Build(new object()).Should().BeNull();
        TodayContentTemplate.Instance.Build(null).Should().BeNull();
    }

    [AvaloniaFact]
    public async Task Recording_from_the_strip_saves_through_the_service()
    {
        // Azione: la spunta registra il completamento di oggi.
        var action = await CreateAsync(HabitType.Action, "Reg-Azione");
        var (_, v1, _) = await OpenAsync(action);
        Find<CheckBox>(v1, "DoneToday").Command!.Execute(null);   // cio' che fa il clic
        for (var i = 0; i < 100 && (await CompletionsAsync(action)).Count == 0; i++) { await Task.Delay(20); Pump(); }
        (await CompletionsAsync(action)).Should().HaveCount(1);

        // Contatore: "+ Registra" non scrive, chiede alla schermata di aprire il modulo di registrazione.
        var counter = await CreateAsync(HabitType.Counter, "Reg-Contatore");
        var (vm2, v2, _) = await OpenAsync(counter);
        var requested = new List<TodayContentAction>();
        vm2.TodayStrip.ActionRequested += (_, a) => requested.Add(a);
        Find<Button>(v2, "RecordButton").Command!.Execute(null);
        requested.Should().Equal(TodayContentAction.OpenRecordModule);
        (await CompletionsAsync(counter)).Should().BeEmpty();

        // Stato: slider a 4 e Salva -> l'intero 5 (1-based), non l'etichetta.
        var state = await CreateAsync(HabitType.State, "Reg-Stato");
        var (_, v3, _) = await OpenAsync(state);
        var slider = Find<Slider>(v3, "ValueSlider");
        // Dalla fascia Oggi: il pannello di registrazione della Cronologia ha un SaveButton omonimo.
        var save = Find<Button>(Find<Border>(v3, "TodayHost"), "SaveButton");
        save.IsEnabled.Should().BeTrue();
        slider.Value = 4;
        Pump();
        save.Command!.Execute(null);
        for (var i = 0; i < 100 && (await CompletionsAsync(state)).Count == 0; i++) { await Task.Delay(20); Pump(); }
        var saved = (await CompletionsAsync(state)).Should().ContainSingle().Subject;
        saved.Values.Should().ContainSingle().Which.ValueInteger.Should().Be(5);
        saved.Values[0].ValueText.Should().BeNull();
    }

    [AvaloniaFact]
    public async Task Opening_a_state_habit_does_not_write_a_value()
    {
        var id = await CreateAsync(HabitType.State, "Apri-Stato");
        using (var scope = Host.Provider.CreateScope())
        {
            var metric = (await Svc<IMetricDefinitionService>(scope).ListByHabitAsync(id)).Single();
            var clock = Svc<IClock>(scope);
            await Svc<ICompletionService>(scope).RecordAsync(
                new Completion { HabitNodeId = id, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = clock.UtcNowMs },
                [new CompletionValue { MetricDefinitionId = metric.Id, SeriesIndex = 0, ValueInteger = 4 }]);
        }
        var (vm, view, _) = await OpenAsync(id);
        var content = (StateTodayContent)vm.TodayStrip.Content!;
        content.SliderValue.Should().Be(3, "valore registrato 4 = indice 3");
        Find<Slider>(view, "ValueSlider").Value.Should().Be(3);
        Pump();
        content.SliderValue.Should().Be(3, "lo slider non riscrive nulla all'avvio");
        (await CompletionsAsync(id)).Should().ContainSingle("aprire non registra");

        // Anche senza valore registrato: resta al minimo e non scrive.
        var empty = await CreateAsync(HabitType.State, "Apri-Stato-Vuoto");
        var (vm2, _, _) = await OpenAsync(empty);
        ((StateTodayContent)vm2.TodayStrip.Content!).SliderValue.Should().Be(0);
        (await CompletionsAsync(empty)).Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task The_abstinence_clock_advances_one_second_per_fake_tick()
    {
        var id = await CreateAsync(HabitType.Resistance, "Orologio");
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var ticks = new FakeTicks();
        long startMs;
        using (var scope = Host.Provider.CreateScope())
        {
            await Svc<IAbstinenceService>(scope).StartAsync(id);
            startMs = (await Svc<IAbstinenceService>(scope).GetStateAsync(id)).CurrentStartedAtMs!.Value;
        }
        clock.Now = DateTimeOffset.FromUnixTimeMilliseconds(startMs).AddSeconds(5);
        ResistanceTodayContent content;
        using (var scope = Host.Provider.CreateScope())
        {
            content = new ResistanceTodayContent(Svc<IAbstinenceService>(scope), clock, ticks);
            await content.LoadAsync(new HabitNode { Id = id, HabitType = HabitType.Resistance }, DayBoundary.Utc);
        }
        var view = new ResistanceTodayView { DataContext = content };
        var w = new Window { Content = view };
        _windows.Add(w);
        _disposables.Add(content);
        w.Show();
        Pump();
        var text = Find<TextBlock>(view, "ClockText");
        string Expected(int s) => DurationFormat.FormatStopwatch(TimeSpan.FromSeconds(s));
        text.Text.Should().Be(Expected(5));
        text.IsVisible.Should().BeTrue();

        clock.Now = clock.Now.AddSeconds(1);
        ticks.Tick();
        Pump();
        text.Text.Should().Be(Expected(6));
        clock.Now = clock.Now.AddSeconds(1);
        ticks.Tick();
        Pump();
        text.Text.Should().Be(Expected(7));
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; set; } = now;
        public DateTimeOffset UtcNow => Now;
        public long UtcNowMs => Now.ToUnixTimeMilliseconds();
        public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeTicks : ITickSourceFactory
    {
        private readonly List<Source> _sources = [];
        public ITickSource Create(TimeSpan interval) { var s = new Source(); _sources.Add(s); return s; }
        public void Tick() { foreach (var s in _sources) s.Raise(); }

        private sealed class Source : ITickSource
        {
            private bool _running;
            public event EventHandler? Tick;
            public void Start() => _running = true;
            public void Stop() => _running = false;
            public void Raise() { if (_running) Tick?.Invoke(this, EventArgs.Empty); }
            public void Dispose() => _running = false;
        }
    }
}
