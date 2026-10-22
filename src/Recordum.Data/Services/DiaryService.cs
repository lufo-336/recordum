using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Services;

public sealed partial class DiaryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<DiaryService> logger,
    IEventBus eventBus,
    IResetTimeResolver resetTimes) : IDiaryService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Diary entry {NoteId} saved for habit {HabitId}")]
    private partial void LogSaved(Guid noteId, Guid habitId);

    public async ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, DiaryEntryDraft draft, CancellationToken ct = default)
    {
        var text = draft.Text;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var habit = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == habitNodeId, ct)
            ?? throw new HabitNodeNotFoundException(habitNodeId);
        _ = habit;

        // A11/B16: il giorno di "oggi" e l'istante di una voce retroattiva vengono dal confine
        // dell'abitudine. Mezzanotte UTC poteva cadere nel giorno prima appena il confine è locale.
        var boundary = await resetTimes.GetDayBoundaryAsync(habitNodeId, ct).ConfigureAwait(false);
        bool retro = day != boundary.Today(clock);
        var instant = retro ? boundary.NoonOf(day) : clock.UtcNow;
        var ts = instant.UtcDateTime;
        var ms = instant.ToUnixTimeMilliseconds();
        // La nota è stata scritta adesso anche quando la voce è datata ieri: la creazione è un fatto,
        // la data della voce un'intenzione. Restano due istanti diversi (famiglia C, spec §5).
        var now = clock.UtcNow.UtcDateTime;

        var completion = new Completion
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitNodeId,
            CompletedAt = ts,
            CompletedAtMs = ms,
            IsRetroactive = retro,
            // Notes intentionally null: for journals the text lives in Note.BodyMd (spec §3).
        };
        var note = new Note
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitNodeId,
            BodyMd = text,
            Category = NoteCategory.Diario,
            TargetDate = day,
            CompletionId = completion.Id,
            CreatedAt = now,
            UpdatedAtMs = clock.UtcNowMs,
        };

        db.Completions.Add(completion);
        db.Notes.Add(note);
        db.NoteHistories.Add(new NoteHistory { Id = Guid.NewGuid(), NoteId = note.Id, BodyMd = text, CreatedAt = now });
        await NoteTagWriter.SyncAsync(db, note.Id, text, draft.PropertyTags, ct);
        foreach (var photo in draft.Photos) db.Attachments.Add(AttachmentService.ToEntity(note.Id, photo));
        // Single SaveChanges = single atomic transaction for the whole pair.
        await db.SaveChangesAsync(ct);

        LogSaved(note.Id, habitNodeId);
        eventBus.Publish(new CompletionRecordedPayload(completion.Id, habitNodeId, completion.CompletedAt));
        eventBus.Publish(new NoteCreatedPayload(note.Id, habitNodeId));
        return note.Id;
    }

    public async ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Resolve the Diario note from either the note id or the paired completion id.
        var note = await db.Notes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteOrCompletionId || n.CompletionId == noteOrCompletionId, ct);
        if (note is null) return; // not a diary entry → nothing to do ("never delete the completion alone")

        DateTime? completedAt = null;
        if (note.CompletionId is { } cid)
            completedAt = await db.Completions.AsNoTracking()
                .Where(c => c.Id == cid).Select(c => (DateTime?)c.CompletedAt).FirstOrDefaultAsync(ct);

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Attachments.Where(a => a.NoteId == note.Id).ExecuteDeleteAsync(ct);
            await db.NoteHistories.Where(h => h.NoteId == note.Id).ExecuteDeleteAsync(ct);
            await db.Notes.Where(n => n.Id == note.Id).ExecuteDeleteAsync(ct);
            if (note.CompletionId is { } cid2)
                await db.Completions.Where(c => c.Id == cid2).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        }

        eventBus.Publish(new NoteDeletedPayload(note.Id, note.HabitNodeId));
        if (note.CompletionId is { } cid3)
            eventBus.Publish(new CompletionDeletedPayload(cid3, note.HabitNodeId, completedAt ?? default));
    }

    public async ValueTask EditEntryAsync(Guid noteId, DiaryEntryEdit edit, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == noteId, ct);
        if (note is null) return;
        if (note.BodyMd != edit.Text) // same text: no spurious version
        {
            note.BodyMd = edit.Text;
            note.UpdatedAtMs = clock.UtcNowMs;
            db.NoteHistories.Add(new NoteHistory { Id = Guid.NewGuid(), NoteId = noteId, BodyMd = edit.Text, CreatedAt = clock.UtcNow.UtcDateTime });
        }
        await NoteTagWriter.SyncAsync(db, noteId, edit.Text, edit.PropertyTags, ct);
        foreach (var photo in edit.AddedPhotos) db.Attachments.Add(AttachmentService.ToEntity(noteId, photo));
        if (edit.RemovedPhotoIds.Count > 0)
        {
            // Solo foto di QUESTA nota: un id di un'altra nota si ignora, mai cancellare foto altrui.
            var removed = edit.RemovedPhotoIds.ToList();
            db.Attachments.RemoveRange(await db.Attachments
                .Where(a => a.NoteId == noteId && removed.Contains(a.Id)).ToListAsync(ct));
        }
        if (!db.ChangeTracker.HasChanges()) return;
        note.UpdatedAtMs = clock.UtcNowMs; // anche per una modifica di soli tag/foto: e' il timbro LWW della sync
        await db.SaveChangesAsync(ct);
        eventBus.Publish(new NoteUpdatedPayload(note.Id, note.HabitNodeId));
    }

    public async ValueTask<IReadOnlyDictionary<Guid, IReadOnlyList<EntryTag>>> GetEntryTagsAsync(
        IReadOnlyCollection<Guid> noteIds, CancellationToken ct = default)
    {
        if (noteIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<EntryTag>>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = noteIds.ToHashSet();
        var rows = await (from nt in db.NoteTags.AsNoTracking()
                          join t in db.Tags.AsNoTracking() on nt.TagId equals t.Id
                          where ids.Contains(nt.NoteId)
                          select new { nt.NoteId, t.Name, nt.Source }).ToListAsync(ct);
        return rows.GroupBy(r => r.NoteId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<EntryTag>)g.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Source).Select(r => new EntryTag(r.Name, r.Source)).ToList());
    }

    public async ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.CompletionId == completionId, ct);
    }

    public async ValueTask<IReadOnlyDictionary<Guid, DiaryEntrySummary>> GetEntrySummariesByCompletionsAsync(
        IReadOnlyCollection<Guid> completionIds, CancellationToken ct = default)
    {
        if (completionIds.Count == 0) return new Dictionary<Guid, DiaryEntrySummary>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = completionIds.ToHashSet();
        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.CompletionId != null && ids.Contains(n.CompletionId.Value))
            .Select(n => new { n.Id, CompletionId = n.CompletionId!.Value, n.BodyMd }).ToListAsync(ct);
        var tags = await GetEntryTagsAsync(notes.Select(n => n.Id).ToList(), ct);
        return notes.ToDictionary(n => n.CompletionId,
            n => new DiaryEntrySummary(n.Id, n.BodyMd, tags.GetValueOrDefault(n.Id) ?? []));
    }
}
