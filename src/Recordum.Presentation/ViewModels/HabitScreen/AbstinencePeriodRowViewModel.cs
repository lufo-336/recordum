using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Core.Time;

namespace Recordum.Presentation.ViewModels.HabitScreen;

/// <summary>One abstinence period as a Cronologia record. Open period = live clock + "since {date}";
/// closed period = the frozen elapsed interval ("2g 6:14:09" / "3:20:00") + "Relapsed on {date}".
/// Delete asks confirmation, then removes.</summary>
public sealed partial class AbstinencePeriodRowViewModel : ObservableObject, IDisposable
{
    private readonly AbstinencePeriod _period;
    private readonly IAbstinenceService _abstinence;
    private readonly IConfirmationDialog _confirm;
    private readonly Action<AbstinencePeriodRowViewModel>? _onDeleted;

    public LiveElapsedClock Clock { get; }
    public bool IsCurrent => _period.EndedAtMs is null;
    public Guid Id => _period.Id;
    public string DurationDisplay { get; }
    public string SinceDisplay { get; }
    public string RelapsedOnDisplay { get; }

    public event EventHandler? Changed;

    public AbstinencePeriodRowViewModel(
        AbstinencePeriod period,
        IAbstinenceService abstinence,
        IConfirmationDialog confirm,
        IClock clock,
        DayBoundary day,
        Action<AbstinencePeriodRowViewModel>? onDeleted = null,
        ITickSourceFactory? ticks = null)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(abstinence);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(clock);
        _period = period;
        _abstinence = abstinence;
        _confirm = confirm;
        _onDeleted = onDeleted;
        Clock = new LiveElapsedClock(clock, ticks);

        var start = day.DayOf(period.StartedAtMs);
        if (IsCurrent)
        {
            Clock.Start(period.StartedAtMs);
            DurationDisplay = string.Empty; // shown live via Clock.Display
#pragma warning disable CA1863 // Ctor not called repeatedly
            SinceDisplay = string.Format(CultureInfo.CurrentCulture, Strings.Abstinence_Since, start.ToString("d", CultureInfo.CurrentCulture));
#pragma warning restore CA1863
            RelapsedOnDisplay = string.Empty;
        }
        else
        {
            var startInstant = DateTimeOffset.FromUnixTimeMilliseconds(period.StartedAtMs);
            var endInstant = DateTimeOffset.FromUnixTimeMilliseconds(period.EndedAtMs!.Value);
            var end = day.DayOf(endInstant);
            // The frozen elapsed interval (start→relapse), same stopwatch format as the live clock —
            // a same-day attempt reads "3:20:00", a multi-day one "2g 6:14:09" (not a calendar-day count).
            DurationDisplay = DurationFormat.FormatStopwatch(endInstant - startInstant);
#pragma warning disable CA1863 // Ctor not called repeatedly
            RelapsedOnDisplay = string.Format(CultureInfo.CurrentCulture, Strings.Abstinence_RelapsedOn, end.ToString("d", CultureInfo.CurrentCulture));
#pragma warning restore CA1863
            SinceDisplay = $"{start.ToString("d", CultureInfo.CurrentCulture)} → {end.ToString("d", CultureInfo.CurrentCulture)}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var ok = await _confirm.ConfirmAsync(
            Strings.Abstinence_DeleteConfirm_Title, Strings.Abstinence_DeleteConfirm_Message).ConfigureAwait(true);
        if (!ok) return;
        Clock.Stop();
        await _abstinence.DeletePeriodAsync(_period.Id).ConfigureAwait(true);
        _onDeleted?.Invoke(this);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops the (possibly running) live clock so a discarded row can't keep ticking/leaking.</summary>
    public void Dispose() => Clock.Dispose();
}
