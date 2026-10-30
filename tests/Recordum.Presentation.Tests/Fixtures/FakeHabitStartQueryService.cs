using Recordum.Core.Abstractions.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeHabitStartQueryService : IHabitStartQueryService
{
    public DateOnly? Start { get; set; }

    public ValueTask<DateOnly> GetStartDayAsync(Guid habitNodeId, CancellationToken ct = default)
        => new(Start ?? DateOnly.FromDayNumber(0));
}
