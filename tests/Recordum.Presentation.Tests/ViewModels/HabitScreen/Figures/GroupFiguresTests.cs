using System.Collections.Immutable;
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;
using Recordum.Presentation.ViewModels.HabitScreen.Figures;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Figures;

/// <summary>I numeri della testa del gruppo. Orologio al 28/10/2026 alle 10:00 UTC, cultura it-IT.</summary>
public sealed class GroupFiguresTests : IDisposable
{
    private static readonly DayBoundary Boundary = DayBoundary.Utc;
    private static readonly DateOnly Today = new(2026, 10, 28);
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;
    private readonly FakeGroupHistoryQueryService _history = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
    private readonly HabitNode _group = new()
    {
        Id = Guid.NewGuid(), NodeType = NodeType.Group, Name = "G",
        CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
    };

    public GroupFiguresTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private static DateOnly D(int day) => new(2026, 10, day);

    private static ImmutableHashSet<DateOnly> Set(params int[] days) => days.Select(D).ToImmutableHashSet();

    private static ImmutableHashSet<DateOnly> Range(int from, int to) => Set(Enumerable.Range(from, to - from + 1).ToArray());

    private void Child(string name, int startsOn, ImmutableHashSet<DateOnly> scheduled, ImmutableHashSet<DateOnly> done)
        => _history.Children.Add(new GroupChildHistory(
            Guid.NewGuid(), name, "#112233", HabitType.Action, 0, D(startsOn), scheduled, done));

    private async Task<GroupFiguresViewModel> LoadAsync(GoalProgressCardViewModel? goal = null)
    {
        var vm = new GroupFiguresViewModel(_history, _clock);
        await vm.LoadAsync(_group, Boundary, goal ?? new GoalProgressCardViewModel(
            new FakeGoalService(), new FakeCompletionService(), new FakeMetricDefinitionService(), new FakeReferenceData(), _clock) { IsEmpty = true });
        return vm;
    }

    [Fact]
    public async Task Complete_run_counts_back_from_today()
    {
        // 23–27 completi, oggi (28) a metà: la giornata è aperta, non interrompe.
        Child("a", 1, Range(20, 28), Range(23, 27));
        Child("b", 1, Range(20, 28), Range(23, 27));

        var vm = await LoadAsync();

        vm.Items[0].Value.Should().Be("5");
        vm.Items[0].Caption.Should().Be("giorni completi di fila");
    }

    [Fact]
    public async Task Complete_run_includes_today_and_stops_at_a_past_miss()
    {
        Child("a", 1, Range(20, 28), Set(22, 23, 24, 25, 26, 27, 28));

        var vm = await LoadAsync();

        vm.Items[0].Value.Should().Be("7");   // 22–28; il 21 è saltato
    }

    [Fact]
    public async Task A_day_without_scheduled_children_is_skipped()
    {
        // Nessuna figlia prevista il 25 e il 26: non interrompono né contano.
        Child("a", 1, Set(23, 24, 27, 28), Set(23, 24, 27));

        var vm = await LoadAsync();

        vm.Items[0].Value.Should().Be("3");
    }

    [Fact]
    public async Task Complete_days_ignore_days_before_a_child_existed()
    {
        // B parte il 25: il 20 è completo con la sola A.
        Child("a", 1, Range(20, 28), Range(20, 28));
        Child("b", 25, Range(25, 28), Range(25, 28));

        var vm = await LoadAsync();

        vm.Items[0].Value.Should().Be("9");
    }

    [Fact]
    public async Task Pct30_is_on_days_with_something_scheduled()
    {
        // 4 giorni previsti (21, 22, 27, 28), 2 completi, oggi incluso come in Action.
        Child("a", 1, Set(21, 22, 27, 28), Set(21, 27));

        var vm = await LoadAsync();

        vm.Items[1].Value.Should().Be("50%");
        vm.Items[1].Caption.Should().Be("30 giorni");
    }

    [Fact]
    public async Task Pct30_window_starts_at_the_group_start()
    {
        // Gruppo creato il 1/10: la finestra dei 30 giorni parte dal 29/9, ma niente è previsto prima dell'inizio.
        Child("a", 1, Range(1, 28), Range(1, 28));

        var vm = await LoadAsync();

        vm.Items[1].Value.Should().Be("100%");
    }

    [Fact]
    public async Task A_child_older_than_the_group_extends_the_start_with_a_second_read()
    {
        Child("a", 20, Set(20, 21), Set(20, 21));
        _history.Children[0] = _history.Children[0] with { StartsOn = new DateOnly(2026, 9, 20), Scheduled = Set(28), Done = Set(28) };
        _group.CreatedAt = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

        await LoadAsync();

        _history.Calls.Should().HaveCount(2);
        _history.Calls[1].From.Should().Be(new DateOnly(2026, 9, 20));
    }

    [Fact]
    public async Task Goal_figure_only_with_a_goal()
    {
        Child("a", 1, Set(28), Set(28));
        var none = await LoadAsync();
        none.Items.Should().HaveCount(2);

        var goal = new GoalProgressCardViewModel(new FakeGoalService(), new FakeCompletionService(),
            new FakeMetricDefinitionService(), new FakeReferenceData(), _clock)
        { IsEmpty = false, Name = "Allenamenti", Actual = 3, Target = 5 };
        var with = await LoadAsync(goal);

        with.Items.Should().HaveCount(3);
        with.Items[2].Value.Should().Be("3 di 5");
        with.Items[2].Caption.Should().Be("Allenamenti");
    }

    [Fact]
    public async Task Goal_figure_falls_back_to_the_generic_caption()
    {
        var goal = new GoalProgressCardViewModel(new FakeGoalService(), new FakeCompletionService(),
            new FakeMetricDefinitionService(), new FakeReferenceData(), _clock)
        { IsEmpty = false, Name = "", Actual = 1, Target = 2 };

        var vm = await LoadAsync(goal);

        vm.Items[2].Caption.Should().Be("obiettivo del gruppo");
    }

    [Fact]
    public async Task Most_skipped_is_the_child_with_most_misses()
    {
        Child("a", 1, Range(20, 28), Set(20, 21, 22, 23, 24, 25, 26, 27));
        Child("b", 1, Range(20, 28), Set(20, 21, 22, 23, 24));   // 25, 26, 27 mancati; oggi escluso
        Child("c", 1, Range(20, 28), Set(20, 21, 22, 23, 24, 25, 26));

        var vm = await LoadAsync();

        vm.Items.Should().HaveCount(3);
        vm.Items[2].Value.Should().Be("b");
        vm.Items[2].Caption.Should().Be("la più saltata");
    }

    [Fact]
    public async Task Most_skipped_absent_when_nothing_missed()
    {
        Child("a", 1, Range(20, 28), Range(20, 27));   // oggi mancante ma escluso

        var vm = await LoadAsync();

        vm.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Empty_group_has_no_nan()
    {
        var vm = await LoadAsync();

        vm.Items.Select(i => i.Value).Should().Equal("0", "0%");
    }
}
