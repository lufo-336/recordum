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
        var active = node is null ? null : await _timers.GetActiveAsync(node.Id, ct).ConfigureAwait(true);
        IsActive = active is not null;
        IsPaused = active?.PausedAtMs is not null;
        SegmentIndex = active?.SegmentIndex;
        UpdateElapsed(active);
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        NextSegmentCommand.NotifyCanExecuteChanged();
    }

    public void Tick()
    {
        if (!IsActive || _node is null) return;
        _ = TickAsync();
    }

    private async Task TickAsync()
    {
        if (_node is null) return;
        var active = await _timers.GetActiveAsync(_node.Id).ConfigureAwait(true);
        UpdateElapsed(active);
    }

    private void UpdateElapsed(ActiveTimer? active)
    {
        if (active is null) { ElapsedSeconds = 0; return; }
        var nowMs = _clock.UtcNow.ToUnixTimeMilliseconds();
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
        await _timers.StartAsync(_node.Id, Mode.Value, ct).ConfigureAwait(true);
        IsActive = true;
        IsPaused = false;
        SegmentIndex = Mode == TimerMode.Session ? 0 : null;
        UpdateCommandsCanExecute();
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync(CancellationToken ct)
    {
        if (_node is null) return;
        await _timers.StopAsync(_node.Id, ct).ConfigureAwait(true);
        IsActive = false;
        IsPaused = false;
        ElapsedSeconds = 0;
        SegmentIndex = null;
        UpdateCommandsCanExecute();
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync(CancellationToken ct)
    {
        if (_node is null) return;
        await _timers.PauseAsync(_node.Id, ct).ConfigureAwait(true);
        IsPaused = true;
        UpdateCommandsCanExecute();
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync(CancellationToken ct)
    {
        if (_node is null) return;
        await _timers.ResumeAsync(_node.Id, ct).ConfigureAwait(true);
        IsPaused = false;
        UpdateCommandsCanExecute();
    }

    [RelayCommand(CanExecute = nameof(CanNextSegment))]
    private async Task NextSegmentAsync(CancellationToken ct)
    {
        if (_node is null || Segments.Count == 0) return;
        var current = SegmentIndex ?? 0;
        if (current >= Segments.Count - 1)
        {
            await StopAsync(ct).ConfigureAwait(true);
            return;
        }
        await _timers.NextSegmentAsync(_node.Id, ct).ConfigureAwait(true);
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
