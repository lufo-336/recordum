using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>GIO-23 Task 5: lo Stato e la cella di scala registrano il valore vero, mai una posizione. Cultura fissata
/// per prova (it-IT / en-US): il separatore decimale decide cosa significa "1,37".</summary>
[Collection(ProcessGlobalCultureCollection.Name)]
public sealed class ScaleRecordingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);
    private readonly CultureInfo? _strings = Strings.Culture;
    private readonly CultureInfo _current = CultureInfo.CurrentCulture;
    private readonly CultureInfo? _defaultThread = CultureInfo.DefaultThreadCurrentCulture;

    public ScaleRecordingTests() => Use("it-IT");

    public void Dispose()
    {
        Strings.Culture = _strings;
        CultureInfo.CurrentCulture = _current;
        CultureInfo.DefaultThreadCurrentCulture = _defaultThread;
    }

    private static void Use(string name)
    {
        var c = CultureInfo.GetCultureInfo(name);
        Strings.Culture = c;
        CultureInfo.CurrentCulture = c;
        CultureInfo.DefaultThreadCurrentCulture = c;
    }

    private static ScaleTemplate Umore(int min = 1, int max = 100, ScalePrecision p = ScalePrecision.Integer) => new()
    {
        Id = Guid.NewGuid(), Code = "umore", Name = "Umore", MinValue = min, MaxValue = max, Precision = p,
        Anchors =
        [
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 1, Label = "Malissimo" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 25, Label = "Male" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 50, Label = "Così così" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 75, Label = "Bene" },
            new ScaleAnchor { Id = Guid.NewGuid(), Value = 100, Label = "Benissimo" },
        ],
    };

    private static async Task<(StateTodayContent Sut, FakeCompletionService Completions, MetricDefinition Metric, HabitNode Node)>
        OpenAsync(ScaleTemplate scale)
    {
        var completions = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var refData = new FakeReferenceData();
        refData.Scales.Clear();
        refData.Scales.Add(scale);
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "umore" };
        var metric = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "Umore", Type = MetricType.Decimal, ScaleTemplateId = scale.Id };
        metrics.Store[metric.Id] = metric;
        var sut = new StateTodayContent(completions, metrics, refData, new FakeClock(Now));
        await sut.LoadAsync(node, DayBoundary.Utc);
        return (sut, completions, metric, node);
    }

    [Fact]
    public async Task Recording_saves_the_true_value_as_a_decimal_not_a_position()
    {
        var (sut, completions, metric, _) = await OpenAsync(Umore());

        sut.SliderValue = 37;
        await sut.RecordCommand.ExecuteAsync(null);

        var v = completions.LastValues!.Single(x => x.MetricDefinitionId == metric.Id);
        v.ValueDecimal.Should().Be(37m);
        v.ValueInteger.Should().BeNull();
    }

    [Fact]
    public async Task A_zero_on_a_scale_that_starts_at_zero_is_recorded_as_zero()
    {
        var (sut, completions, _, _) = await OpenAsync(Umore(0, 10));

        sut.SliderValue = 0;
        await sut.RecordCommand.ExecuteAsync(null);

        completions.LastValues!.Single().ValueDecimal.Should().Be(0m);
    }

    [Fact]
    public async Task Reloading_the_day_puts_the_cursor_back_on_the_recorded_value_and_reads_it_in_full()
    {
        var (sut, completions, _, node) = await OpenAsync(Umore());
        completions.MagnitudeByHabit[node.Id] = [new DailyMagnitude(DateOnly.FromDateTime(Now.UtcDateTime), 37m)];

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.SliderValue.Should().Be(37);
        sut.CurrentLabel.Should().Be("37, fra Male e Così così");
    }

    [Theory]
    [InlineData("it-IT", "1,37", 1.37)]
    [InlineData("en-US", "1.37", 1.37)]
    public async Task The_exact_field_reads_the_decimal_separator_of_the_culture(string culture, string typed, double expected)
    {
        Use(culture);
        var (sut, completions, _, _) = await OpenAsync(Umore(0, 10, ScalePrecision.Decimal));

        sut.ValueText = typed;
        await sut.RecordCommand.ExecuteAsync(null);

        sut.SliderValue.Should().BeApproximately(expected, 1e-9);
        completions.LastValues!.Single().ValueDecimal.Should().Be((decimal)expected);
    }

    [Fact]
    public async Task The_exact_field_rounds_clamps_and_ignores_garbage()
    {
        var (sut, _, _, _) = await OpenAsync(Umore());

        sut.ValueText = "4,6";
        sut.SliderValue.Should().Be(5, "una scala a interi arrotonda");
        sut.ValueText = "150";
        sut.SliderValue.Should().Be(100, "si taglia all'estremo");
        sut.ValueText.Should().Be("100", "il campo mostra il valore preso");
        sut.ValueText = "abc";
        sut.SliderValue.Should().Be(100, "un testo non valido lascia il valore di prima");
        sut.ValueText.Should().Be("100");
    }

    [Fact]
    public async Task Typing_the_same_clamped_number_twice_still_rewrites_the_field()
    {
        var (sut, _, _, _) = await OpenAsync(Umore());
        sut.ValueText = "150";
        sut.ValueText = "200";   // anche qui lo slider non si muove (100 → 100)
        sut.ValueText.Should().Be("100");
    }

    [Fact]
    public async Task Step_follows_the_precision()
    {
        // Parametro muto: il default (1) risponde diverso da una scala a decimali.
        (await OpenAsync(Umore(0, 10, ScalePrecision.Decimal))).Sut.Step.Should().Be(0.01);
        (await OpenAsync(Umore())).Sut.Step.Should().Be(1);
    }

    [Fact]
    public async Task The_cursor_carries_the_named_marks_and_a_tick_per_integer_only_on_short_scales()
    {
        var wide = (await OpenAsync(Umore())).Sut;
        wide.Points.Select(p => p.Value).Should().Equal(1m, 25m, 50m, 75m, 100m);
        wide.Points[1].Label.Should().Be("Male");

        var shortScale = Umore(1, 5);
        shortScale.Anchors = shortScale.Anchors.Take(1).ToList();
        var narrow = (await OpenAsync(shortScale)).Sut;
        narrow.Points.Select(p => p.Value).Should().Equal(1m, 2m, 3m, 4m, 5m);
        narrow.Points[0].Label.Should().Be("Malissimo");
    }

    [Fact]
    public async Task A_state_without_a_scale_cannot_record()
    {
        var completions = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "s" };
        var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = node.Id, Name = "n", Type = MetricType.Integer };
        metrics.Store[m.Id] = m;   // una metrica senza scala: il caso morto del §3
        var sut = new StateTodayContent(completions, metrics, new FakeReferenceData(), new FakeClock(Now));
        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.CanRecord.Should().BeFalse();
        await sut.RecordCommand.ExecuteAsync(null);
        completions.LastValues.Should().BeNull();
    }

    [Fact]
    public void The_scale_cell_records_the_true_value_on_the_same_cursor()
    {
        var scale = Umore();
        var metric = new MetricDefinition { Id = Guid.NewGuid(), Name = "Umore", Type = MetricType.Decimal, ScaleTemplateId = scale.Id };
        var cell = new SeriesCellViewModel(metric, scale: new ScaleInfo(scale));

        cell.Min.Should().Be(1);
        cell.Max.Should().Be(100);
        cell.BuildValue(0)!.ValueDecimal.Should().Be(1m, "il cursore riposa sul minimo");
        cell.ValueText = "62";
        var v = cell.BuildValue(0)!;
        v.ValueDecimal.Should().Be(62m);
        v.ValueInteger.Should().BeNull();
        cell.CurrentLabel.Should().Be("62, fra Così così e Bene");
    }
}
