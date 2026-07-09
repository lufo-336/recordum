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
    int SortOrder = 0);
