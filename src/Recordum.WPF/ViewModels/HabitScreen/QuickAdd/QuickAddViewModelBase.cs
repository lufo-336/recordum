using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Time;
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

    /// <summary>Il confine di giornata dell'abitudine selezionata, impostato dallo screen subito dopo
    /// aver costruito il quick-add. Default <see cref="DayBoundary.Utc"/> per i test che non lo
    /// impostano — con fuso UTC e reset a mezzanotte è l'identità, cioè il comportamento pre-R9.</summary>
    public DayBoundary DayBoundary { get; set; } = DayBoundary.Utc;

    protected QuickAddViewModelBase(ICompletionService completions, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(clock);
        Completions = completions;
        Clock = clock;
        // A57: la data iniziale è il giorno del confine. Lo screen assegna DayBoundary dopo la
        // costruzione, quindi qui vale ancora il default: la riassegna insieme al confine.
        CompletionDate = DayBoundary.Today(clock).ToDateTime(TimeOnly.MinValue);
    }

    public abstract Task SaveAsync(CancellationToken ct = default);

    /// <summary>Shows a localized message for an unexpected save failure and traces the real exception,
    /// so a raw EF/SQLite message (e.g. "UNIQUE constraint failed") never leaks into the UI (CR21).</summary>
    protected void SetUnexpectedError(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"QuickAdd save failed: {ex}");
        ErrorMessage = Strings.QuickAdd_Error_Unexpected;
    }

    /// <summary>Costruisce (istante, ms, retroattiva) per <see cref="CompletionDate"/>.
    /// <para>Una registrazione per oggi si stampiglia <b>adesso</b>. Una retroattiva per il giorno D si
    /// stampiglia a <b>mezzogiorno locale di D</b>: sta dentro il giorno per qualunque reset plausibile
    /// (00:00–06:00) e regge il cambio d'ora, mentre la vecchia mezzanotte UTC poteva cadere nel giorno
    /// prima non appena il confine è diventato locale (spec R9 §5).</para></summary>
    protected (DateTime Timestamp, long Ms, bool IsRetroactive) ResolveCompletionTimestamp()
    {
        var date = DateOnly.FromDateTime(CompletionDate.Date);
        var today = DayBoundary.Today(Clock);                                  // A58
        if (date == today)
        {
            var now = Clock.UtcNow;
            return (now.UtcDateTime, now.ToUnixTimeMilliseconds(), false);
        }

        var noon = DayBoundary.NoonOf(date);
        return (noon.UtcDateTime, noon.ToUnixTimeMilliseconds(), true);
    }
}
