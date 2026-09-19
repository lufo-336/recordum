using Recordum.Core.Abstractions.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeComposedHabitService : IComposedHabitService
{
    public ComposedHabitRequest? LastRequest { get; private set; }

    public ValueTask<Guid> CreateComposedHabitAsync(ComposedHabitRequest request, CancellationToken ct = default)
    {
        LastRequest = request;
        return new ValueTask<Guid>(request.Node.Id);
    }
}
