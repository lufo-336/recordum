using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Resources;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

/// <summary>Quick-add panel for Resistance (abstinence) habits.
/// Shows the live clean-streak elapsed time and offers Inizio / Ricaduta actions.
/// Does not record completions — all state is owned by <see cref="IAbstinenceService"/>.</summary>
public sealed partial class AbstinenceQuickAddViewModel : QuickAddViewModelBase, IDisposable
{
    private readonly IAbstinenceService _abstinence;
    private readonly ITickSource? _tick;
    private long? _startedMs;

    public AbstinenceQuickAddViewModel(
        ICompletionService completions,
        IAbstinenceService abstinence,
        IClock clock,
        ITickSourceFactory? ticks)
        : base(completions, clock)
    {
        ArgumentNullException.ThrowIfNull(abstinence);
        _abstinence = abstinence;

        _tick = ticks?.Create(TimeSpan.FromSeconds(1));
        if (_tick is not null)
            _tick.Tick += (_, _) => RefreshElapsed();
    }

    // ---- observable state ----

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private AbstinenceStatus _status = AbstinenceStatus.NotStarted;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _elapsedDisplay = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private DateOnly? _relapsedOn;

    // ---- derived properties (re-raised when Status or RelapsedOn change in ReloadAsync) ----

    public bool CanStart => Status != AbstinenceStatus.Abstaining;
    public bool CanLapse  => Status == AbstinenceStatus.Abstaining;

    public string RelapsedOnDisplay
    {
        get
        {
            if (RelapsedOn is not { } d) return string.Empty;
#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
            return string.Format(CultureInfo.CurrentCulture, Strings.Abstinence_RelapsedOn, d.ToString("d", CultureInfo.CurrentCulture));
#pragma warning restore CA1863
        }
    }

    // ---- events ----

    /// <summary>Raised after a successful Start or Lapse so the host can refresh the streak summary
    /// and calendar markers without waiting for a completion event (abstinence writes no completions).</summary>
    public event EventHandler? Changed;

    // ---- load ----

    /// <summary>Il confine ricevuto dallo screen: il giorno dell'ultima ricaduta si nomina secondo il
    /// giorno dell'abitudine, non secondo la data UTC.</summary>
    private DayBoundary _day = DayBoundary.Utc;

    /// <summary>Populates the VM from the service. Call after <see cref="QuickAddViewModelBase.Node"/>
    /// is set.</summary>
    public async Task LoadAsync(DayBoundary day, CancellationToken ct = default)
    {
        _day = day;
        await ReloadAsync(ct).ConfigureAwait(true);
    }

    private async Task ReloadAsync(CancellationToken ct = default)
    {
        var state = await _abstinence.GetStateAsync(Node!.Id, ct).ConfigureAwait(true);
        Status      = state.Status;
        _startedMs  = state.CurrentStartedAtMs;
        RelapsedOn  = state.LastEndedAtMs is { } e
            ? _day.DayOf(e)
            : null;

        // CanStart / CanLapse / RelapsedOnDisplay are computed; notify bindings.
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanLapse));
        OnPropertyChanged(nameof(RelapsedOnDisplay));

        RefreshElapsed();

        // Drive the 1-second refresh tick only while actively abstaining.
        if (Status == AbstinenceStatus.Abstaining) _tick?.Start(); else _tick?.Stop();
    }

    private void RefreshElapsed()
        => ElapsedDisplay = _startedMs is { } s
            ? DurationFormat.FormatStopwatch(Clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(s))
            : string.Empty;

    // ---- commands ----

    [RelayCommand]
    private async Task StartAsync()
    {
        await _abstinence.StartAsync(Node!.Id).ConfigureAwait(true);
        await ReloadAsync().ConfigureAwait(true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task LapseAsync()
    {
        await _abstinence.LapseAsync(Node!.Id).ConfigureAwait(true);
        await ReloadAsync().ConfigureAwait(true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ---- QuickAddViewModelBase contract ----

    /// <summary>Abstinence has no "save completion" step — Start/Lapse are the actions.</summary>
    public override Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>Ferma e dispone la sorgente del battito. Senza questo, una scheda chiusa mentre
    /// l'abitudine sta astenendo lascia il timer nativo acceso per sempre — <see cref="_tick"/> resta
    /// avviato da <see cref="ReloadAsync"/>, e nessuno lo fermava mai perché questa classe non era
    /// <see cref="IDisposable"/> (review Task 4, giro 1: né <c>HabitScreenViewModel</c> 495 né 625,
    /// che già cercano <c>is IDisposable</c> sul quick-add uscente, potevano trovare qualcosa da
    /// fermare). <see cref="QuickAddViewModelBase"/> non implementa <see cref="IDisposable"/>, quindi
    /// non c'è una base da incatenare.</summary>
    public void Dispose() => _tick?.Dispose();
}
