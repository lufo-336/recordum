using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
namespace Recordum.Data.Services;

public sealed partial class NoteService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<NoteService> logger,
    IEventBus eventBus) : INoteService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Note {Id} created for habit {HabitId}")]
    private partial void LogCreated(Guid id, Guid habitId);

    private void AddVersion(RecordumDbContext db, Guid noteId, string bodyMd)
        => db.NoteHistories.Add(new NoteHistory
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            BodyMd = bodyMd,
            CreatedAt = clock.UtcNow.UtcDateTime,
        });

    public async ValueTask<Guid> CreateAsync(Note draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Notes.Add(draft);
        if (!string.IsNullOrEmpty(draft.BodyMd)) AddVersion(db, draft.Id, draft.BodyMd);
        await db.SaveChangesAsync(ct);
        LogCreated(draft.Id, draft.HabitNodeId);
        eventBus.Publish(new NoteCreatedPayload(draft.Id, draft.HabitNodeId));
        return draft.Id;
    }

    public async ValueTask UpdateAsync(Guid id, Action<Note> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var n = await db.Notes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return;
        var oldBody = n.BodyMd;
        mutate(n);
        if (n.BodyMd != oldBody) AddVersion(db, n.Id, n.BodyMd);
        await db.SaveChangesAsync(ct);
        eventBus.Publish(new NoteUpdatedPayload(n.Id, n.HabitNodeId));
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var n = await db.Notes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return;
        // attachment.note_id is FK Restrict (AttachmentConfiguration) — must clear child rows
        // before the parent Note delete, mirroring PurgeService's subtree-delete ordering.
        // DB-row-only: the physical files under %LocalAppData%\Recordum\attachments\{noteId}\
        // are not removed here (a separate, pre-existing gap — orphan-file cleanup is tracked
        // elsewhere, not part of this fix).
        await db.Attachments.Where(a => a.NoteId == id).ExecuteDeleteAsync(ct);
        await db.NoteHistories.Where(h => h.NoteId == id).ExecuteDeleteAsync(ct);
        await db.Notes.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        eventBus.Publish(new NoteDeletedPayload(n.Id, n.HabitNodeId));
    }

    public async ValueTask<Note?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async ValueTask<IReadOnlyList<Note>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.HabitNodeId == habitNodeId)
            .ToListAsync(ct);
        // Stable anchor: assigned calendar day, else creation day. The tiebreak is creation
        // order (NOT UpdatedAtMs) so editing a note never moves it within its day. Client-side
        // to avoid translating the DateOnly null-coalesce into SQLite (note sets per habit are small).
        return notes
            .OrderByDescending(n => n.TargetDate ?? DateOnly.FromDateTime(n.CreatedAt))
            .ThenByDescending(n => n.CreatedAt)
            .ToList();
    }

    public async ValueTask<IReadOnlyList<Note>> ListByHabitAndDateAsync(Guid habitNodeId, DateOnly targetDate, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.AsNoTracking()
            .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate == targetDate)
            .OrderByDescending(n => n.UpdatedAtMs)
            .ToListAsync(ct);
    }

    public async ValueTask<IReadOnlyList<DateOnly>> ListNoteDaysInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default)
    {
        var first = new DateOnly(year, month, 1);
        var next = first.AddMonths(1);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.AsNoTracking()
            .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate != null
                        && n.TargetDate >= first && n.TargetDate < next)
            .Select(n => n.TargetDate!.Value)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);
    }

    public async ValueTask<IReadOnlyList<DayNoteCount>> ListNoteCountsInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default)
    {
        var first = new DateOnly(year, month, 1);
        var next = first.AddMonths(1);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Notes.AsNoTracking()
            .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate != null
                        && n.TargetDate >= first && n.TargetDate < next)
            .Select(n => n.TargetDate!.Value)
            .ToListAsync(ct);
        return rows
            .GroupBy(d => d)
            .Select(g => new DayNoteCount(g.Key, g.Count()))
            .OrderBy(c => c.Date)
            .ToList();
    }

    public async ValueTask<IReadOnlyList<NoteVersion>> ListVersionsAsync(Guid noteId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.NoteHistories.AsNoTracking()
            .Where(h => h.NoteId == noteId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new NoteVersion(h.Id, h.BodyMd, h.CreatedAt))
            .ToListAsync(ct);
    }

    public async ValueTask RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken ct = default)
    {
        // Read the version body in its own context and dispose it BEFORE opening the write
        // context in UpdateAsync. Overlapping connections on the same SQLCipher file can stall
        // the write (SQLITE_BUSY) — the in-memory test connection masks this, the real file does not.
        string body;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var version = await db.NoteHistories.AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == versionId && h.NoteId == noteId, ct);
            if (version is null) return;
            body = version.BodyMd;
        }
        await UpdateAsync(noteId, n => n.BodyMd = body, ct);
    }
}
