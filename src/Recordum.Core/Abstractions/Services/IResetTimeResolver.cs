namespace Recordum.Core.Abstractions.Services;

public interface IResetTimeResolver
{
    ValueTask<TimeOnly> GetEffectiveResetTimeAsync(Guid habitNodeId, CancellationToken ct = default);
}
