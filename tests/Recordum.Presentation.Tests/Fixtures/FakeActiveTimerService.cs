using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeActiveTimerService(IClock? clock = null) : IActiveTimerService
{
    private readonly IClock _clock = clock ?? new FakeClock();

    public List<ActiveTimer> Active { get; } = [];
    public List<Guid> PauseCalls { get; } = [];
    public List<Guid> ResumeCalls { get; } = [];
    public List<Guid> StopCalls { get; } = [];
    public List<Guid> StopWithoutRecordingCalls { get; } = [];
    public List<Guid> NextSegmentCalls { get; } = [];
    public List<(Guid HabitNodeId, TimerMode Mode)> StartCalls { get; } = [];

    public Func<Guid, Completion>? StopFactory { get; set; }
    public Func<Guid, int>? StopWithoutRecordingFactory { get; set; }

    public ValueTask<ActiveTimer> StartAsync(Guid habitNodeId, TimerMode mode, CancellationToken ct = default)
    {
        StartCalls.Add((habitNodeId, mode));
        if (Active.Exists(t => t.HabitNodeId == habitNodeId))
            throw new TimerAlreadyActiveException(habitNodeId);
        var t = new ActiveTimer
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitNodeId,
            StartedAtMs = 0,
            TimerMode = mode,
            SegmentIndex = mode == TimerMode.Session ? 0 : null,
        };
        Active.Add(t);
        return new ValueTask<ActiveTimer>(t);
    }

    public ValueTask PauseAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        PauseCalls.Add(habitNodeId);
        return ValueTask.CompletedTask;
    }

    public ValueTask ResumeAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        ResumeCalls.Add(habitNodeId);
        return ValueTask.CompletedTask;
    }

    public ValueTask<Completion> StopAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        StopCalls.Add(habitNodeId);
        Active.RemoveAll(t => t.HabitNodeId == habitNodeId);
        var c = StopFactory?.Invoke(habitNodeId)
            ?? new Completion { Id = Guid.NewGuid(), HabitNodeId = habitNodeId, CompletedAt = _clock.UtcNow.UtcDateTime, CompletedAtMs = _clock.UtcNowMs };
        return new ValueTask<Completion>(c);
    }

    public ValueTask<int> StopWithoutRecordingAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        StopWithoutRecordingCalls.Add(habitNodeId);
        Active.RemoveAll(t => t.HabitNodeId == habitNodeId);
        return new ValueTask<int>(StopWithoutRecordingFactory?.Invoke(habitNodeId) ?? 0);
    }

    public ValueTask NextSegmentAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        NextSegmentCalls.Add(habitNodeId);
        var t = Active.Find(x => x.HabitNodeId == habitNodeId)
            ?? throw new ActiveTimerNotFoundException(habitNodeId);
        t.SegmentIndex = (t.SegmentIndex ?? 0) + 1;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ActiveTimer?> GetActiveAsync(Guid habitNodeId, CancellationToken ct = default)
        => new(Active.Find(x => x.HabitNodeId == habitNodeId));

    public ValueTask<IReadOnlyList<ActiveTimer>> ListActiveAsync(CancellationToken ct = default)
        => new((IReadOnlyList<ActiveTimer>)Active.ToList());
}
