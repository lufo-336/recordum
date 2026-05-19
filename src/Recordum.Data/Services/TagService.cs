using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class TagService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<TagService> logger) : ITagService
{
    public async ValueTask<Guid> CreateAsync(Tag draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Tags.Add(draft);
        await db.SaveChangesAsync(ct);
        _ = clock; _ = logger;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<Tag> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.Tags.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        mutate(t);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.HabitNodeTags.Where(x => x.TagId == id).ExecuteDeleteAsync(ct);
        await db.Tags.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async ValueTask AttachAsync(Guid habitNodeId, Guid tagId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var exists = await db.HabitNodeTags.AnyAsync(x => x.HabitNodeId == habitNodeId && x.TagId == tagId, ct);
        if (exists) return;
        db.HabitNodeTags.Add(new HabitNodeTag { HabitNodeId = habitNodeId, TagId = tagId });
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DetachAsync(Guid habitNodeId, Guid tagId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.HabitNodeTags
            .Where(x => x.HabitNodeId == habitNodeId && x.TagId == tagId)
            .ExecuteDeleteAsync(ct);
    }

    public async ValueTask<IReadOnlyList<Tag>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Tags.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
    }
}
