using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.Data.Services;

public sealed partial class DiaryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<DiaryService> logger,
    IEventBus eventBus) : IDiaryService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Diary entry {NoteId} saved for habit {HabitId}")]
    private partial void LogSaved(Guid noteId, Guid habitId);

    public async ValueTask<Guid> SaveEntryAsync(Guid habitNodeId, DateOnly day, string text, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var habit = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == habitNodeId, ct)
            ?? throw new HabitNodeNotFoundException(habitNodeId);
        _ = habit;

        var now = clock.UtcNow.UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        bool retro = day != today;
        var ts = retro ? DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : now;
        var ms = new DateTimeOffset(ts, TimeSpan.Zero).ToUnixTimeMilliseconds();

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
}
