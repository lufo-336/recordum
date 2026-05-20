using System.Collections.Immutable;
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
    ImmutableArray<Guid> TagIds);
