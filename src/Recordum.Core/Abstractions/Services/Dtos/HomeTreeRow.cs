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
    bool IsCompletedToday);
