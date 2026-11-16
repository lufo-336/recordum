using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

/// <summary>Task 5 (2b): ogni voce di Configura ha il suo riassunto di una riga e la riga Home di anteprima.</summary>
public class SectionSummaryTests : IDisposable
{
    private readonly CultureInfo _savedCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _savedUi = CultureInfo.CurrentUICulture;

    public SectionSummaryTests()
    {
        var it = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = it;
        CultureInfo.CurrentUICulture = it;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _savedCulture;
        CultureInfo.CurrentUICulture = _savedUi;
        GC.SuppressFinalize(this);
    }

    private static HabitNode Habit(HabitType type = HabitType.Counter, TimeOnly? reset = null) =>
        new() { Id = Guid.NewGuid(), Name = "h", NodeType = NodeType.Habit, HabitType = type, ResetTime = reset };

    [Fact]
    public async Task Scheduling_describes_the_schedule_and_says_custom_when_empty()
    {
        var sut = new SchedulingSectionViewModel(new FakeHabitConfigService());
        await sut.LoadAsync(Habit(), new HabitConfig { ScheduleType = ScheduleType.WeeklyX, ScheduleWeeklyTarget = 3 });
        sut.Summary.Should().Be("3 volte a settimana");

        await sut.LoadAsync(Habit(), new HabitConfig { ScheduleType = ScheduleType.SpecificDays, ScheduleDaysJson = null });
        sut.Summary.Should().Be("Personalizzato");
    }

    [Fact]
    public void Summary_follows_a_change()
    {
        var sut = new SchedulingSectionViewModel(new FakeHabitConfigService());
        sut.LoadAsync(Habit(), null).GetAwaiter().GetResult();
        sut.Summary.Should().Be("Ogni giorno");
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.ScheduleType = ScheduleType.Monthly;

        raised.Should().Contain(nameof(SectionViewModelBase.Summary));
        sut.Summary.Should().Be("Ogni mese");
    }

    [Fact]
    public async Task Streak_gives_the_mode_and_the_recovery_when_there_is_one()
    {
        var sut = new StreakSectionViewModel(new FakeHabitConfigService());
        await sut.LoadAsync(Habit(), new HabitConfig { StreakMode = StreakMode.Weekly, StreakRecovery = StreakRecovery.None });
        sut.Summary.Should().Be("Settimanale");

        await sut.LoadAsync(Habit(), new HabitConfig { StreakMode = StreakMode.Daily, StreakRecovery = StreakRecovery.GraceDay });
        sut.Summary.Should().Be("Giornaliera · Giorno di grazia");
        sut.HasAdvanced.Should().BeFalse("GIO-21: la sola cosa avanzata era la tolleranza, nascosta finché non funziona");
    }

    [Fact]
    public async Task Goal_gives_name_target_and_period_or_says_there_is_none()
    {
        var goals = new FakeGoalService();
        var node = Habit();
        var sut = new GoalSectionViewModel(goals);
        await sut.LoadAsync(node);
        sut.Summary.Should().Be("Nessun obiettivo");

        sut.GoalName = "Camminare";
        sut.TargetValue = 30m;
        sut.PeriodType = GoalPeriodType.Rolling;
        sut.RollingDays = 7;
        sut.Summary.Should().Be("Camminare · 30 Ultimi 7 giorni");
        sut.HasAdvanced.Should().BeTrue();
    }

    [Fact]
    public async Task Goal_summary_includes_the_metric_unit_and_no_dangling_space_without()
    {
        var node = Habit();
        var refData = new FakeReferenceData();
        var unit = refData.Units[0];
        var metrics = new FakeMetricDefinitionService();
        var metricId = Guid.NewGuid();
        await metrics.CreateAsync(new MetricDefinition { Id = metricId, HabitNodeId = node.Id, Name = "Acqua", UnitOfMeasureId = unit.Id });
        var goals = new FakeGoalService();
        await goals.UpsertActiveAsync(node.Id, g => { g.Name = "Bere"; g.TargetValue = 2m; g.PeriodType = GoalPeriodType.Open; g.MetricDefinitionId = metricId; });
        var sut = new GoalSectionViewModel(goals, metrics, refData);
        await sut.LoadAsync(node);
        var label = string.IsNullOrEmpty(unit.Symbol) ? unit.Name : unit.Symbol;
        sut.Summary.Should().Be($"Bere · 2 {label} Aperto");

        var bare = new FakeGoalService();
        await bare.UpsertActiveAsync(node.Id, g => { g.Name = "Bere"; g.TargetValue = 2m; g.PeriodType = GoalPeriodType.Open; });
        var noMetric = new GoalSectionViewModel(bare, metrics, refData);
        await noMetric.LoadAsync(node);
        noMetric.Summary.Should().Be("Bere · 2 Aperto");
    }

    [Fact]
    public async Task Reset_time_load_survives_a_throwing_resolver()
    {
        var sut = new ResetTimeSectionViewModel(new FakeHabitNodeService(), new ThrowingResolver());
        await sut.LoadAsync(Habit());
        sut.Summary.Should().Be("Ereditato");
    }

    private sealed class ThrowingResolver : Recordum.Core.Abstractions.Services.IResetTimeResolver
    {
        public ValueTask<TimeOnly> GetEffectiveResetTimeAsync(Guid id, CancellationToken ct = default) => throw new InvalidOperationException("boom");
        public ValueTask<Recordum.Core.Time.DayBoundary> GetDayBoundaryAsync(Guid id, CancellationToken ct = default) => throw new InvalidOperationException();
        public ValueTask<IReadOnlyDictionary<Guid, Recordum.Core.Time.DayBoundary>> GetDayBoundariesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => throw new InvalidOperationException();
        public ValueTask<Recordum.Core.Time.DayBoundary> GetAppDayBoundaryAsync(CancellationToken ct = default) => throw new InvalidOperationException();
    }

    [Fact]
    public async Task Reset_time_shows_own_value()
    {
        var sut = new ResetTimeSectionViewModel(new FakeHabitNodeService());
        await sut.LoadAsync(Habit(reset: new TimeOnly(5, 0)));
        sut.Summary.Should().Be("5:00");
    }

    [Fact]
    public async Task Reset_time_says_inherited()
    {
        var sut = new ResetTimeSectionViewModel(new FakeHabitNodeService(), new FakeResetTimeResolver(new Recordum.Core.Time.DayBoundary(TimeZoneInfo.Utc, new TimeOnly(4, 0))));
        await sut.LoadAsync(Habit());
        sut.Summary.Should().Be("4:00 (ereditato)");
    }

    [Fact]
    public async Task Metrics_lists_the_field_names_or_says_none()
    {
        var node = Habit();
        var metrics = new FakeMetricDefinitionService();
        var sut = new MetricsSectionViewModel(metrics, new FakeReferenceData());
        await sut.LoadAsync(node);
        sut.Summary.Should().Be("Nessun campo");

        await metrics.CreateAsync(new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "Tazze", SortOrder = 0 });
        await metrics.CreateAsync(new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "Peso", SortOrder = 1 });
        await sut.LoadAsync(node);
        sut.Summary.Should().Be("Tazze, Peso");
    }

    [Fact]
    public async Task Scales_names_the_selected_scale()
    {
        var node = Habit(HabitType.State);
        var refData = new FakeReferenceData();
        var sut = new ScalesSectionViewModel(new FakeMetricDefinitionService(), refData);
        await sut.LoadAsync(node);
        var scale = new ScaleTemplate { Id = Guid.NewGuid(), Name = "Umore" };
        sut.AvailableScales.Add(scale);
        sut.Summary.Should().BeEmpty();
        sut.SelectedScaleTemplateId = scale.Id;
        sut.Summary.Should().Be("Umore");
    }

    [Fact]
    public async Task Scales_summary_translates_an_official_scale_like_its_menu()
    {
        var sut = new ScalesSectionViewModel(new FakeMetricDefinitionService(), new FakeReferenceData());
        await sut.LoadAsync(Habit(HabitType.State));
        var scale = new ScaleTemplate { Id = Guid.NewGuid(), Code = "mood-5", Name = "Mood Scale (1–5)", IsOfficial = true };
        sut.AvailableScales.Add(scale);
        sut.SelectedScaleTemplateId = scale.Id;
        sut.Summary.Should().Be("Scala dell'umore (1–5)", "il riassunto dice la scala come la dice il menu (onda 3a, Task 7)");
    }

    [Fact]
    public async Task Registration_says_how_often_and_the_journal_gesture()
    {
        var configs = new FakeHabitConfigService();
        var prefs = new FakeHabitUiPrefsService();
        var counter = Habit();
        await configs.UpsertAsync(counter.Id, c => c.Repeatable = true);
        var sut = new RegistrationSectionViewModel(configs, prefs);
        await sut.LoadAsync(counter);
        sut.Summary.Should().Be("Più volte al giorno");

        sut.Repeatable = false;
        sut.Summary.Should().Be("Una volta al giorno");

        await sut.LoadAsync(Habit(HabitType.Journal));
        sut.Summary.Should().Be("Scrivi la voce di oggi");
    }

    [Fact]
    public async Task Timer_is_off_or_says_mode_and_target()
    {
        var sut = new TimerSectionViewModel(new FakeHabitConfigService());
        await sut.LoadAsync(Habit(), new HabitConfig { HasIntegratedTimer = false });
        sut.Summary.Should().Be("Spento");
        sut.HasAdvanced.Should().BeTrue();

        await sut.LoadAsync(Habit(), new HabitConfig { HasIntegratedTimer = true, TimerMode = TimerMode.Countdown, TimerTargetSeconds = 1500 });
        sut.Summary.Should().Be("Conto alla rovescia · 25:00");
    }

    [Fact]
    public async Task Home_row_says_the_figure_and_the_strip()
    {
        var sut = new HomeRowSectionViewModel(new FakeHabitUiPrefsService(), null);
        await sut.LoadAsync(Habit());
        sut.Summary.Should().Be("Totale di oggi · Ultimi giorni");
    }

    [Fact]
    public async Task Notes_summary_is_empty()
    {
        var sut = new NotesSectionViewModel(new FakeHabitConfigService(), new Recordum.Presentation.Services.RecentColorStore(new FakeAppSettingService()), new Recordum.Presentation.Services.FavoriteColorStore(new FakeAppSettingService()), new FakeScreenColorPicker());
        await sut.LoadAsync(Habit(), null);
        sut.Summary.Should().BeEmpty();
    }

    [Fact]
    public async Task Tags_lists_the_attached_names_or_says_none()
    {
        var tags = new FakeTagService();
        var node = Habit();
        var sut = new TagsSectionViewModel(tags);
        await sut.LoadAsync(node);
        sut.Summary.Should().Be("Nessun tag");

        var tagId = await tags.CreateAsync(new Tag { Id = Guid.NewGuid(), Name = "Salute" });
        await tags.SetTagsAsync(node.Id, [tagId]);
        await sut.LoadAsync(node);
        sut.Summary.Should().Be("Salute");
    }

    [Fact]
    public void AiSharing_says_coming_soon()
        => new AiSharingSectionViewModel(new FakeHabitNodeService()).Summary.Should().Be("In arrivo");

    [Fact]
    public async Task Home_row_preview_follows_the_choice()
    {
        var node = Habit();
        var configs = new FakeHabitConfigService();
        var sut = new HomeRowSectionViewModel(new FakeHabitUiPrefsService(), null);
        await sut.LoadAsync(node);
        sut.Preview.Should().NotBeNull();
        sut.Preview!.Fractions.Should().HaveCount(14);

        sut.SelectedStrip = sut.StripOptions.First(o => o.Strip == HomeStrip.None);
        sut.Preview!.Fractions.Should().BeEmpty();

        sut.SetIdentity("Nuovo nome", "#112233", "star");
        sut.Preview!.Name.Should().Be("Nuovo nome");
        sut.Preview.Color.Should().Be("#112233");
    }

    // Revisione del Task 6: in Configura l'abitudine e' vera, quindi l'anteprima non inventa numeri (anti-obiettivo della
    // spec: "numeri che sembrano scoperte senza dati dietro"). Resta la forma della striscia scelta, a celle vuote.
    [Theory]
    [InlineData(HabitType.Action)]
    [InlineData(HabitType.Counter)]
    [InlineData(HabitType.Resistance)]
    [InlineData(HabitType.Journal)]
    [InlineData(HabitType.State)]
    public async Task Home_row_preview_has_the_strip_shape_but_no_invented_value(HabitType type)
    {
        var sut = new HomeRowSectionViewModel(new FakeHabitUiPrefsService(), null);
        await sut.LoadAsync(Habit(type));
        foreach (var kf in sut.KeyFigureOptions)
        {
            sut.SelectedKeyFigure = kf;
            sut.Preview!.KeyFigure.Should().BeNull($"nessun valore per {kf.Kind}");
        }
        foreach (var strip in sut.StripOptions)
        {
            sut.SelectedStrip = strip;
            var expected = strip.Strip == HomeStrip.None ? 0 : 14;
            sut.Preview!.Fractions.Should().HaveCount(expected);
            sut.Preview.Fractions.Where(f => f != 0).Should().BeEmpty("celle vuote");
            sut.Preview.Counts.Should().HaveCount(expected);
            sut.Preview.Counts.Where(c => c != 0).Should().BeEmpty("celle vuote");
            sut.Preview.StripStyle.Should().Be(HomeRowPresentation.ResolveStripStyle(type), "la forma resta quella del tipo");
        }
    }
}
