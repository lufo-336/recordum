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

    public ValueTask DeleteEntryAsync(Guid noteOrCompletionId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask EditEntryAsync(Guid noteId, string text, CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask<Note?> GetEntryByCompletionAsync(Guid completionId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
