using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.WPF.Resources;
using Recordum.WPF.Services;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>One abstinence period as a Cronologia record. Open period = live clock + "since {date}";
/// closed period = "{n} clean days" + "Relapsed on {date}". Delete asks confirmation, then removes.</summary>
public sealed partial class AbstinencePeriodRowViewModel : ObservableObject
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
        Action<AbstinencePeriodRowViewModel>? onDeleted = null)
    {
        ArgumentNullException.ThrowIfNull(period);
        _period = period;
        _abstinence = abstinence;
        _confirm = confirm;
        _onDeleted = onDeleted;
        Clock = new LiveElapsedClock(clock);

        var start = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(period.StartedAtMs).UtcDateTime);
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
            var end = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(period.EndedAtMs!.Value).UtcDateTime);
            int cleanDays = end.DayNumber - start.DayNumber + 1; // inclusive; pauses not subtracted (see spec)
#pragma warning disable CA1863 // Ctor not called repeatedly
            DurationDisplay = string.Format(CultureInfo.CurrentCulture, Strings.Abstinence_CleanDays, cleanDays);
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
}
