using System.Collections.Concurrent;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeHabitUiPrefsService : IHabitUiPrefsService
{
    public ConcurrentDictionary<Guid, HabitUiPrefs> Store { get; } = new();

    public ValueTask<HabitUiPrefs> GetAsync(Guid habitNodeId, CancellationToken ct = default)
        => new(Store.TryGetValue(habitNodeId, out var p) ? p : new HabitUiPrefs());

    public ValueTask SetAsync(Guid habitNodeId, HabitUiPrefs prefs, CancellationToken ct = default)
    {
        Store[habitNodeId] = prefs;
        return ValueTask.CompletedTask;
    }
}
