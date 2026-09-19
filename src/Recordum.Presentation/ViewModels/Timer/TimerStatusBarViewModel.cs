using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;

namespace Recordum.Presentation.ViewModels.Timer;

public sealed partial class TimerStatusBarViewModel : ObservableObject
{
    private readonly IActiveTimerService _timers;
    private readonly IClock _clock;
    private readonly IHabitNodeService _habits;

    public ObservableCollection<ActiveTimerRowViewModel> Rows { get; } = [];

    public bool IsVisible => Rows.Count > 0;

    public TimerStatusBarViewModel(IActiveTimerService timers, IClock clock, IHabitNodeService habits)
    {
        _timers = timers;
        _clock = clock;
        _habits = habits;
        Rows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsVisible));
    }

    public ValueTask LoadAsync(CancellationToken ct = default) => RefreshAsync(ct);

    public async ValueTask RefreshAsync(CancellationToken ct = default)
    {
        var active = await _timers.ListActiveAsync(ct).ConfigureAwait(true);
        Rows.Clear();
        foreach (var t in active)
        {
            var node = await _habits.GetAsync(t.HabitNodeId, ct).ConfigureAwait(true);
            Rows.Add(new ActiveTimerRowViewModel(t, node?.Name ?? "?", _timers, _clock));
        }
    }

    public void Tick()
    {
        foreach (var r in Rows) r.Tick();
    }
}
