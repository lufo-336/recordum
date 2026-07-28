using System.Collections.Immutable;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;

namespace Recordum.Core.Abstractions.Services.Dtos;

public sealed record HomeTreeRow(
    Guid HabitNodeId,
    Guid? ParentId,
    string Name,
    string ColorHex,
    HabitType? HabitType,
    DateTime? LastCompletionAt,
    int CurrentStreakCount,
    bool IsCompletedToday,
    PinScope PinScope,
    int ChildCount,
    bool IsArchived,
    ImmutableArray<int> CompletionsLast7Days,
    ImmutableArray<Guid> TagIds,
    ImmutableArray<int> CompletionsLast14Days = default,
    decimal? TodayCounterTotal = null,
    string? TodayCounterUnitCode = null,
    int? TodayStateValue = null,
    string? TodayStateLabel = null,
    AbstinenceStatus AbstinenceStatus = AbstinenceStatus.NotStarted,
    long? AbstinenceOpenSinceMs = null,
    int? JournalEntriesThisWeek = null,
    string? Icon = null,
    int SortOrder = 0,
    // 2.1-bis: per-day primary-metric value over the 14-day window (index 0 = 13 days ago,
    // 13 = today; null = no value that day). Counter = daily sum; State = last value of the
    // day (point-in-time reading, last-wins); other types leave it default/empty.
    ImmutableArray<decimal?> ValuesLast14Days = default,
    // True when the counter's primary metric is a duration (Seconds). Paired with a null/non-time
    // TodayCounterUnitCode it means TodayCounterTotal is still RAW SECONDS — the presentation layer
    // renders those as h:mm:ss so the row agrees with the records list instead of printing "720".
    bool TodayCounterIsDuration = false);
