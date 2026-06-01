using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
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

    public ValueTask<Guid> CreateAsync(Note draft, CancellationToken ct = default)
        => CreateAsync(draft, attachments: null, ct);

    public async ValueTask<Guid> CreateAsync(Note draft, IReadOnlyList<Attachment>? attachments, CancellationToken ct = default)
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
        LogCreated(draft.Id, draft.HabitNodeId);
        eventBus.Publish(new NoteCreatedPayload(draft.Id, draft.HabitNodeId));
        _ = clock;
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
        eventBus.Publish(new NoteUpdatedPayload(n.Id, n.HabitNodeId));
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var n = await db.Notes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return;
        await db.Notes.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        eventBus.Publish(new NoteDeletedPayload(n.Id, n.HabitNodeId));
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
            .ToListAsync(ct);
    }
}
