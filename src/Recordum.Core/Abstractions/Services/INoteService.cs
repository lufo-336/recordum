using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface INoteService
{
    ValueTask<Guid> CreateAsync(Note draft, CancellationToken ct = default);
    ValueTask UpdateAsync(Guid id, Action<Note> mutate, CancellationToken ct = default);
    ValueTask DeleteAsync(Guid id, CancellationToken ct = default);
    ValueTask<Note?> GetAsync(Guid id, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Note>> ListByHabitAsync(Guid habitNodeId, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Note>> ListByHabitAndDateAsync(Guid habitNodeId, DateOnly targetDate, CancellationToken ct = default);
    ValueTask<IReadOnlyList<DateOnly>> ListNoteDaysInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default);
    ValueTask<IReadOnlyList<DayNoteCount>> ListNoteCountsInMonthAsync(Guid habitNodeId, int year, int month, CancellationToken ct = default);
}
