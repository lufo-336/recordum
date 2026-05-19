using Recordum.Core.Abstractions.Services.Dtos;

namespace Recordum.Core.Abstractions.Services;

public interface IHabitTreeQueryService
{
    ValueTask<IReadOnlyList<HomeTreeRow>> GetHomeTreeAsync(CancellationToken ct = default);
}
