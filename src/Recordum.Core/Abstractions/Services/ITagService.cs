using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface ITagService
{
    ValueTask<Guid> CreateAsync(Tag draft, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<Tag> mutate, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask AttachAsync(Guid habitNodeId, Guid tagId, CancellationToken ct = default);
    ValueTask DetachAsync(Guid habitNodeId, Guid tagId, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Tag>> ListAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<Tag>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default);
    ValueTask SetTagsAsync(Guid habitNodeId, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default);
}
