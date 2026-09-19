using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.HabitBuilder.Preview;

/// <summary>Inert stand-ins for the services the "Oggi" content view-models take in their
/// constructors. The preview never calls their <c>LoadAsync</c> — <see cref="SamplePreviewFactory"/>
/// feeds the bound properties by hand (spec §3, seam 2) — and the pane is hit-test-invisible, so no
/// command can reach these either. They answer empty rather than throwing: should a later change
/// start loading here, an empty preview beats a crash inside the builder dialog.
/// </summary>
internal static class SampleServices
{
    internal sealed class NoCompletions : ICompletionService
    {
        public ValueTask<Guid> RecordAsync(Completion draft, IReadOnlyList<CompletionValue>? values = null,
            IReadOnlyList<CompletionSegment>? segments = null, CancellationToken ct = default) => new(Guid.Empty);
        public ValueTask UpdateAsync(Guid id, Action<Completion> mutate, CancellationToken ct = default) => default;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => default;
        public ValueTask<Completion?> GetAsync(Guid id, CancellationToken ct = default) => new((Completion?)null);
        public ValueTask<IReadOnlyList<Completion>> ListByHabitAsync(Guid habitNodeId, DateTimeOffset? fromUtc = null,
            DateTimeOffset? toUtc = null, int? take = null, CancellationToken ct = default) => new([]);
        public ValueTask<IReadOnlyList<HeatmapCell>> GetHeatmapAsync(Guid habitNodeId, DateOnly fromDate,
            DateOnly toDate, CancellationToken ct = default) => new([]);
        public ValueTask<IReadOnlyList<DailyValue>> GetDailySumAsync(Guid habitNodeId, Guid metricDefinitionId,
            DateOnly fromDate, DateOnly toDate, CancellationToken ct = default) => new([]);
        public ValueTask<IReadOnlyList<DailyMagnitude>> GetDailyMagnitudeAsync(Guid habitNodeId, int year,
            int month, CancellationToken ct = default) => new([]);
    }

    internal sealed class NoStreaks : IStreakAggregator
    {
        public ValueTask<IReadOnlyList<StreakRun>> GetHistoryAsync(Guid habitNodeId, DateOnly fromDate,
            DateOnly toDate, CancellationToken ct = default) => new([]);
    }

    internal sealed class NoHourly : IHourlyDistributionQueryService
    {
        public ValueTask<IReadOnlyList<int>> GetHourlyAsync(Guid habitNodeId, DateOnly fromDate,
            DateOnly toDate, CancellationToken ct = default) => new([]);
    }

    internal sealed class NoMetrics : IMetricDefinitionService
    {
        public ValueTask<Guid> CreateAsync(MetricDefinition draft, CancellationToken ct = default) => new(Guid.Empty);
        public ValueTask UpdateAsync(Guid id, Action<MetricDefinition> mutate, CancellationToken ct = default) => default;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => default;
        public ValueTask<IReadOnlyList<MetricDefinition>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default) => new([]);
    }

    internal sealed class NoGoals : IGoalService
    {
        public ValueTask<Guid> CreateAsync(Goal draft, CancellationToken ct = default) => new(Guid.Empty);
        public ValueTask UpdateAsync(Guid id, Action<Goal> mutate, CancellationToken ct = default) => default;
        public ValueTask CloseAsync(Guid id, CancellationToken ct = default) => default;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct = default) => default;
        public ValueTask<IReadOnlyList<Goal>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default) => new([]);
        public ValueTask<Goal?> GetActiveAsync(Guid habitNodeId, CancellationToken ct = default) => new((Goal?)null);
        public ValueTask UpsertActiveAsync(Guid habitNodeId, Action<Goal> mutate, CancellationToken ct = default) => default;
    }

    internal sealed class NoReferenceData : IReferenceDataQueryService
    {
        public ValueTask<IReadOnlyList<UnitOfMeasure>> ListUnitsAsync(CancellationToken ct = default) => new([]);
        public ValueTask<IReadOnlyList<ScaleTemplate>> ListScalesAsync(CancellationToken ct = default) => new([]);
    }

    internal sealed class NoUiPrefs : IHabitUiPrefsService
    {
        public ValueTask<HabitUiPrefs> GetAsync(Guid habitNodeId, CancellationToken ct = default) => new(new HabitUiPrefs());
        public ValueTask SetAsync(Guid habitNodeId, HabitUiPrefs prefs, CancellationToken ct = default) => default;
        public ValueTask UpdateAsync(Guid habitNodeId, Func<HabitUiPrefs, HabitUiPrefs> mutate, CancellationToken ct = default) => default;
    }

    internal sealed class NoAbstinence : IAbstinenceService
    {
        public Task<AbstinenceState> GetStateAsync(Guid habitNodeId, CancellationToken ct = default) =>
            Task.FromResult(new AbstinenceState(AbstinenceStatus.NotStarted, null, null));
        public Task StartAsync(Guid habitNodeId, CancellationToken ct = default) => Task.CompletedTask;
        public Task LapseAsync(Guid habitNodeId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(Guid habitNodeId, DateOnly from, DateOnly to,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AbstinencePeriod>>([]);
        public Task DeletePeriodAsync(Guid periodId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
