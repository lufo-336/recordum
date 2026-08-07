using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>
/// Base for the per-type "quick add" panels shown in the Cronologia column — the fast path for
/// recording against the currently-selected <see cref="Node"/> on <see cref="CompletionDate"/>.
/// Each habit type has its own subclass (action tap, counter, measured series, abstinence
/// Inizio/Ricaduta, journal); the host picks the right one from the habit type and binds it via a
/// template selector. Subclasses implement <see cref="SaveAsync"/>; the screen refreshes itself after
/// a save rather than listening to the quick-add. Shared here: the target node, the date,
/// and the saving/error state.
/// </summary>
public abstract partial class QuickAddViewModelBase : ObservableObject
{
    protected ICompletionService Completions { get; }
    protected IClock Clock { get; }

    [ObservableProperty] private HabitNode? _node;
    [ObservableProperty] private DateTime _completionDate;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isSaving;

    protected QuickAddViewModelBase(ICompletionService completions, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(clock);
        Completions = completions;
        Clock = clock;
        CompletionDate = clock.UtcNow.UtcDateTime.Date;
    }

    public abstract Task SaveAsync(CancellationToken ct = default);

    /// <summary>Shows a localized message for an unexpected save failure and traces the real exception,
    /// so a raw EF/SQLite message (e.g. "UNIQUE constraint failed") never leaks into the UI (CR21).</summary>
    protected void SetUnexpectedError(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"QuickAdd save failed: {ex}");
        ErrorMessage = Strings.QuickAdd_Error_Unexpected;
    }

    /// <summary>Builds (timestamp, isRetroactive) for the current CompletionDate vs clock.Today.</summary>
    protected (DateTime Timestamp, long Ms, bool IsRetroactive) ResolveCompletionTimestamp()
    {
        var date = CompletionDate.Date;
        var today = Clock.UtcNow.UtcDateTime.Date;
        var ts = date == today
            ? Clock.UtcNow.UtcDateTime
            : DateTime.SpecifyKind(date, DateTimeKind.Utc);
        var ms = new DateTimeOffset(ts, TimeSpan.Zero).ToUnixTimeMilliseconds();
        return (ts, ms, date != today);
    }
}
