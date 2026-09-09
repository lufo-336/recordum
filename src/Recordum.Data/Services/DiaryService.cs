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

    public async ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default)
    {
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

    public async ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == noteId, ct);
        if (note is null) return;
        if (note.BodyMd == text) return; // no-op, no spurious version
        note.BodyMd = text;
        note.UpdatedAtMs = clock.UtcNowMs;
        db.NoteHistories.Add(new NoteHistory { Id = Guid.NewGuid(), NoteId = noteId, BodyMd = text, CreatedAt = clock.UtcNow.UtcDateTime });
        await db.SaveChangesAsync(ct);
        eventBus.Publish(new NoteUpdatedPayload(note.Id, note.HabitNodeId));
    }

    public async ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.CompletionId == completionId, ct);
    }

    public async ValueTask<IReadOnlyDictionary<Guid, string>> GetEntryBodiesByCompletionsAsync(
        IReadOnlyCollection<Guid> completionIds, CancellationToken ct = default)
    {
        if (completionIds.Count == 0) return new Dictionary<Guid, string>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = completionIds.ToHashSet();
        return await db.Notes.AsNoTracking()
            .Where(n => n.CompletionId != null && ids.Contains(n.CompletionId.Value))
            .ToDictionaryAsync(n => n.CompletionId!.Value, n => n.BodyMd, ct);
    }
}
