using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed class NoteService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<NoteService> logger) : INoteService
{
    public async ValueTask<Guid> CreateAsync(Note draft, IReadOnlyList<Attachment>? attachments = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Notes.Add(draft);
        if (attachments is not null)
        {
            foreach (var a in attachments)
            {
                a.NoteId = draft.Id;
                if (a.Id == Guid.Empty) a.Id = Guid.NewGuid();
                db.Attachments.Add(a);
            }
        }
        await db.SaveChangesAsync(ct);
        _ = clock; _ = logger;
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<Note> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var n = await db.Notes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return;
        mutate(n);
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Notes.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    public async ValueTask<Note?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.Include(n => n.Attachments).AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async ValueTask<IReadOnlyList<Note>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.AsNoTracking()
            .Where(n => n.HabitNodeId == habitNodeId)
            .OrderByDescending(n => n.UpdatedAtMs)
            .ToListAsync(ct);
    }
}
