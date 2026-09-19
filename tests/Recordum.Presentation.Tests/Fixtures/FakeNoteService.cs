using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeNoteService : INoteService
{
    public Dictionary<Guid, Note> Store { get; } = new();
    public List<Guid> DeleteCalls { get; } = [];
    public List<(Guid Id, Action<Note> Mutator)> UpdateCalls { get; } = [];
    public Exception? NextException { get; set; }
    public Dictionary<Guid, List<NoteVersion>> Versions { get; } = new();
    public List<(Guid NoteId, Guid VersionId)> RestoreCalls { get; } = [];

    /// <summary>One-shot delay consumed by the next <see cref="ListByHabitAsync"/> call, then cleared —
    /// lets a test hold one call in flight (e.g. to simulate two overlapping reloads racing) while a
    /// later call proceeds unblocked.</summary>
    public Func<Task>? NextListByHabitDelay { get; set; }

    public ValueTask<Guid> CreateAsync(Note draft, CancellationToken ct = default)
    {
        Throw();
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Id == Guid.Empty) draft.Id = Guid.NewGuid();
        Store[draft.Id] = draft;
        return new ValueTask<Guid>(draft.Id);
    }

    public ValueTask UpdateAsync(Guid id, Action<Note> mutate, CancellationToken ct = default)
    {
        Throw();
        ArgumentNullException.ThrowIfNull(mutate);
        UpdateCalls.Add((id, mutate));
        if (Store.TryGetValue(id, out var n)) mutate(n);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        DeleteCalls.Add(id);
        Store.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask<Note?> GetAsync(Guid id, CancellationToken ct = default)
    {
        Throw();
        return new ValueTask<Note?>(Store.TryGetValue(id, out var n) ? n : null);
    }

    public async ValueTask<IReadOnlyList<Note>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        Throw();
        if (NextListByHabitDelay is { } delay)
        {
            NextListByHabitDelay = null;
            await delay().ConfigureAwait(true);
        }
        IReadOnlyList<Note> list = Store.Values
            .Where(n => n.HabitNodeId == habitNodeId)
            .OrderByDescending(n => n.UpdatedAtMs)
            .ToList();
        return list;
    }

    public ValueTask<IReadOnlyList<Note>> ListByHabitAndDateAsync(Guid habitNodeId, DateOnly targetDate, CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<Note> r = Store.Values.Where(n => n.HabitNodeId == habitNodeId && n.TargetDate == targetDate).OrderByDescending(n => n.UpdatedAtMs).ToList();
        return new(r);
    }

    public ValueTask<IReadOnlyList<DateOnly>> ListNoteDaysInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<DateOnly> r = Store.Values
            .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate is { } t && t.Year == year && t.Month == month)
            .Select(n => n.TargetDate!.Value).Distinct().OrderBy(d => d).ToList();
        return new(r);
    }

    public ValueTask<IReadOnlyList<DayNoteCount>> ListNoteCountsInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<DayNoteCount> r = Store.Values
            .Where(n => n.HabitNodeId == habitNodeId && n.TargetDate is { } t && t.Year == year && t.Month == month)
            .GroupBy(n => n.TargetDate!.Value)
            .Select(g => new DayNoteCount(g.Key, g.Count()))
            .OrderBy(d => d.Date).ToList();
        return new(r);
    }

    public ValueTask<IReadOnlyList<NoteVersion>> ListVersionsAsync(Guid noteId, CancellationToken ct = default)
    {
        Throw();
        IReadOnlyList<NoteVersion> r = Versions.TryGetValue(noteId, out var v)
            ? v.OrderByDescending(x => x.CreatedAt).ToList()
            : [];
        return new(r);
    }

    public ValueTask RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken ct = default)
    {
        Throw();
        RestoreCalls.Add((noteId, versionId));
        if (Versions.TryGetValue(noteId, out var v)
            && v.FirstOrDefault(x => x.Id == versionId) is { } ver
            && Store.TryGetValue(noteId, out var n))
        {
            n.BodyMd = ver.BodyMd;
        }
        return ValueTask.CompletedTask;
    }

    private void Throw()
    {
        if (NextException is { } e) { NextException = null; throw e; }
    }
}
