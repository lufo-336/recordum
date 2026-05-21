using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeTagService : ITagService
{
    public Dictionary<Guid, Tag> Tags { get; } = new();
    public HashSet<(Guid habit, Guid tag)> Attachments { get; } = new();
    public Exception? NextException { get; set; }

    public ValueTask<Guid> CreateAsync(Tag draft, CancellationToken ct = default)
    {
        Throw();
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Tags[draft.Id] = draft;
        return new(draft.Id);
    }

    public ValueTask UpdateAsync(Guid id, Action<Tag> mutate, CancellationToken ct = default)
    {
        Throw();
        if (Tags.TryGetValue(id, out var t)) mutate(t);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        Tags.Remove(id);
        Attachments.RemoveWhere(a => a.tag == id);
        return ValueTask.CompletedTask;
    }

    public ValueTask AttachAsync(Guid habitNodeId, Guid tagId, CancellationToken ct = default)
    {
        Throw();
        Attachments.Add((habitNodeId, tagId));
        return ValueTask.CompletedTask;
    }

    public ValueTask DetachAsync(Guid habitNodeId, Guid tagId, CancellationToken ct = default)
    {
        Throw();
        Attachments.Remove((habitNodeId, tagId));
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<Tag>> ListAsync(CancellationToken ct = default)
    {
        Throw();
        return new(Tags.Values.OrderBy(t => t.Name).ToList());
    }

    public ValueTask<IReadOnlyList<Tag>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        return new(Attachments
            .Where(a => a.habit == habitNodeId)
            .Select(a => Tags[a.tag])
            .OrderBy(t => t.Name)
            .ToList());
    }

    public ValueTask SetTagsAsync(Guid habitNodeId, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default)
    {
        Throw();
        Attachments.RemoveWhere(a => a.habit == habitNodeId);
        foreach (var id in tagIds.Distinct())
            Attachments.Add((habitNodeId, id));
        return ValueTask.CompletedTask;
    }

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
