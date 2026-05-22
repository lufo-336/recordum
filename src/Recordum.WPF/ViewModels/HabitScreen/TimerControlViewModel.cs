using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class TimerControlViewModel : ObservableObject
{
    private readonly IActiveTimerService _timers;
    private readonly IHabitConfigService _configs;
    private readonly IClock _clock;

    private HabitNode? _node;
    private HabitConfig? _config;
    private ActiveTimer? _activeSnapshot;

    public ObservableCollection<SegmentDefinition> Segments { get; } = [];

    [ObservableProperty] private bool _isApplicable;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private int _elapsedSeconds;
    [ObservableProperty] private int? _segmentIndex;

    public TimerMode? Mode => _config?.TimerMode;
    public string ElapsedDisplay
    {
        get
        {
            var ts = TimeSpan.FromSeconds(ElapsedSeconds);
            return ElapsedSeconds < 3600
                ? ts.ToString(@"mm\:ss", System.Globalization.CultureInfo.InvariantCulture)
                : ts.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public TimerControlViewModel(IActiveTimerService timers, IHabitConfigService configs, IClock clock)
    {
        _timers = timers;
        _configs = configs;
        _clock = clock;
    }

    public async Task SetContextAsync(HabitNode? node, HabitConfig? config, CancellationToken ct = default)
    {
        _node = node;
        _config = config;
        IsApplicable = config?.HasIntegratedTimer == true && config.TimerMode is not null;
        Segments.Clear();
        if (node is not null && config?.TimerMode == TimerMode.Session)
        {
            var segs = await _configs.ListSegmentsAsync(node.Id, ct).ConfigureAwait(true);
            foreach (var s in segs) Segments.Add(s);
        }
        _activeSnapshot = node is null ? null : await _timers.GetActiveAsync(node.Id, ct).ConfigureAwait(true);
        IsActive = _activeSnapshot is not null;
        IsPaused = _activeSnapshot?.PausedAtMs is not null;
        SegmentIndex = _activeSnapshot?.SegmentIndex;
        UpdateElapsed(_activeSnapshot);
        UpdateCommandsCanExecute();
    }

    /// <summary>
    /// Recompute elapsed display from the cached ActiveTimer snapshot — no DB call.
    /// State transitions (Start/Pause/Resume/Stop) refresh the snapshot in their handlers.
    /// </summary>
    public void Tick()
    {
        if (!IsActive || _node is null) return;
        UpdateElapsed(_activeSnapshot);
    }

    private void UpdateElapsed(ActiveTimer? active)
    {
        if (active is null) { ElapsedSeconds = 0; return; }
        var nowMs = _clock.UtcNowMs;
        var endMs = active.PausedAtMs ?? nowMs;
        var ms = endMs - active.StartedAtMs - active.AccumulatedPausedMs;
        if (ms < 0) ms = 0;
        ElapsedSeconds = (int)(ms / 1000);
        OnPropertyChanged(nameof(ElapsedDisplay));
    }

    private bool CanStart() => _node is not null && IsApplicable && !IsActive;
    private bool CanStop() => _node is not null && IsActive;
    private bool CanPause() => _node is not null && IsActive && !IsPaused;
    private bool CanResume() => _node is not null && IsActive && IsPaused;
    private bool CanNextSegment() => _node is not null && IsActive && Mode == TimerMode.Session;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync(CancellationToken ct)
    {
        if (_node is null || Mode is null) return;
        _activeSnapshot = await _timers.StartAsync(_node.Id, Mode.Value, ct).ConfigureAwait(true);
        IsActive = true;
        IsPaused = false;
        SegmentIndex = _activeSnapshot.SegmentIndex;
        UpdateCommandsCanExecute();
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync(CancellationToken ct)
    {
        if (_node is null) return;
        await _timers.StopAsync(_node.Id, ct).ConfigureAwait(true);
        _activeSnapshot = null;
        IsActive = false;
        IsPaused = false;
        ElapsedSeconds = 0;
        SegmentIndex = null;
        UpdateCommandsCanExecute();
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync(CancellationToken ct)
    {
        if (_node is null || _activeSnapshot is null) return;
        await _timers.PauseAsync(_node.Id, ct).ConfigureAwait(true);
        _activeSnapshot.PausedAtMs = _clock.UtcNowMs;
        IsPaused = true;
        UpdateCommandsCanExecute();
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync(CancellationToken ct)
    {
        if (_node is null || _activeSnapshot is null) return;
        await _timers.ResumeAsync(_node.Id, ct).ConfigureAwait(true);
        if (_activeSnapshot.PausedAtMs is { } pAt)
            _activeSnapshot.AccumulatedPausedMs += _clock.UtcNowMs - pAt;
        _activeSnapshot.PausedAtMs = null;
        IsPaused = false;
        UpdateCommandsCanExecute();
    }

    [RelayCommand(CanExecute = nameof(CanNextSegment))]
    private async Task NextSegmentAsync(CancellationToken ct)
    {
        if (_node is null || _activeSnapshot is null || Segments.Count == 0) return;
        var current = SegmentIndex ?? 0;
        if (current >= Segments.Count - 1)
        {
            await StopAsync(ct).ConfigureAwait(true);
            return;
        }
        await _timers.NextSegmentAsync(_node.Id, ct).ConfigureAwait(true);
        _activeSnapshot.SegmentIndex = current + 1;
        SegmentIndex = current + 1;
        UpdateCommandsCanExecute();
    }

    private void UpdateCommandsCanExecute()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        NextSegmentCommand.NotifyCanExecuteChanged();
    }
}
