using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;
using Recordum.Data.Services.Aggregators;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public sealed class NaiveDailyStreakAggregatorTests
{
    private sealed class StubCompletionService(IReadOnlyList<HeatmapCell> cells) : ICompletionService
    {
        public ValueTask<IReadOnlyList<HeatmapCell>> GetHeatmapAsync(Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
            => new(cells);

        public ValueTask<Guid> RecordAsync(Completion draft, IReadOnlyList<CompletionValue>? values = null, IReadOnlyList<CompletionSegment>? segments = null, CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask UpdateAsync(Guid id, Action<Completion> mutate, CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask<Completion?> GetAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask<IReadOnlyList<Completion>> ListByHabitAsync(Guid habitNodeId, DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null, int? take = null, CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask<IReadOnlyList<DailyValue>> GetDailySumAsync(Guid habitNodeId, Guid metricDefinitionId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default) => throw new NotImplementedException();
    }

#pragma warning disable CA1859 // intentional: return interface so callers depend on the contract
    private static ICompletionService FakeCompletions(params (DateOnly date, int count)[] cells)
        => new StubCompletionService(cells.Select(c => new HeatmapCell(c.date, c.count)).ToList());
#pragma warning restore CA1859

    [Fact]
    public async Task Empty_range_returns_empty_list()
    {
        var aggr = new NaiveDailyStreakAggregator(FakeCompletions());
        var runs = await aggr.GetHistoryAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.Empty(runs);
    }

    [Fact]
    public async Task Single_active_day_returns_one_run_length_1()
    {
        var aggr = new NaiveDailyStreakAggregator(FakeCompletions((new DateOnly(2026, 1, 5), 1)));
        var runs = await aggr.GetHistoryAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var run = Assert.Single(runs);
        Assert.Equal(new DateOnly(2026, 1, 5), run.Start);
        Assert.Equal(new DateOnly(2026, 1, 5), run.End);
        Assert.Equal(1, run.Length);
    }

    [Fact]
    public async Task Five_consecutive_days_returns_one_run_length_5()
    {
        var cells = Enumerable.Range(0, 5)
            .Select(i => (new DateOnly(2026, 1, 5).AddDays(i), 1))
            .ToArray();
        var aggr = new NaiveDailyStreakAggregator(FakeCompletions(cells));
        var runs = await aggr.GetHistoryAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var run = Assert.Single(runs);
        Assert.Equal(5, run.Length);
        Assert.Equal(new DateOnly(2026, 1, 5), run.Start);
        Assert.Equal(new DateOnly(2026, 1, 9), run.End);
    }

    [Fact]
    public async Task Gap_of_one_day_splits_into_two_runs()
    {
        var aggr = new NaiveDailyStreakAggregator(FakeCompletions(
            (new DateOnly(2026, 1, 5), 1),
            (new DateOnly(2026, 1, 6), 2),
            (new DateOnly(2026, 1, 8), 1)));
        var runs = await aggr.GetHistoryAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.Equal(2, runs.Count);
        Assert.Equal(2, runs[0].Length);
        Assert.Equal(1, runs[1].Length);
    }

    [Fact]
    public async Task Multiple_completions_same_day_count_as_one_active_day()
    {
        var aggr = new NaiveDailyStreakAggregator(FakeCompletions(
            (new DateOnly(2026, 1, 5), 5)));
        var runs = await aggr.GetHistoryAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var run = Assert.Single(runs);
        Assert.Equal(1, run.Length);
    }

    [Fact]
    public async Task Runs_returned_in_ascending_start_order()
    {
        var aggr = new NaiveDailyStreakAggregator(FakeCompletions(
            (new DateOnly(2026, 1, 20), 1),
            (new DateOnly(2026, 1, 5), 1)));
        var runs = await aggr.GetHistoryAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.Equal(2, runs.Count);
        Assert.True(runs[0].Start < runs[1].Start);
    }
}
